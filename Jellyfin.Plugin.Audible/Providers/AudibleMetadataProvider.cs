using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.Audible.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Audible.Providers
{
    public class AudibleMetadataProvider : IRemoteMetadataProvider<AudioBook, SongInfo>
    {
        public const string HttpClientName = "AudibleMetadataProvider";
        private const string AudnexBase = "https://api.audnex.us";
        private const string AudibleApiBase = "https://api.audible";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AudibleMetadataProvider> _logger;

        public AudibleMetadataProvider(IHttpClientFactory httpClientFactory, ILogger<AudibleMetadataProvider> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public string Name => "Audible";

        public async Task<MetadataResult<AudioBook>> GetMetadata(SongInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<AudioBook> { Item = new AudioBook() };

            var region = Plugin.Instance?.Configuration.Region ?? "fr";

            // Try ASIN from provider IDs first
            var asin = info.GetProviderId("Audible");

            AudnexBook? book = null;

            if (!string.IsNullOrEmpty(asin))
            {
                book = await GetByAsin(asin, region, cancellationToken).ConfigureAwait(false);
                // Reject if language doesn't match the expected region language
                var expectedLang = RegionToLanguage(region);
                if (book != null && !string.IsNullOrEmpty(book.Language) &&
                    !book.Language.Equals(expectedLang, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Rejecting ASIN {Asin}: language={Lang}, expected={Expected}. Will search again.",
                        asin, book.Language, expectedLang);
                    book = null;
                }
            }

            if (book == null)
            {
                // Search by title + album artist (author)
                var title = info.Name;
                var author = info.AlbumArtists?.Count > 0 ? info.AlbumArtists[0] : null;
                if (string.IsNullOrEmpty(author) && info.Artists?.Count > 0)
                    author = info.Artists[0];

                if (!string.IsNullOrEmpty(title))
                {
                    var searchAsin = await SearchAsin(title, author, region, cancellationToken).ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(searchAsin))
                        book = await GetByAsin(searchAsin, region, cancellationToken).ConfigureAwait(false);
                }
            }

            if (book == null)
                return result;

            result.HasMetadata = true;
            var item = result.Item;

            if (!string.IsNullOrEmpty(book.Title))
                item.Name = book.Title;

            var rawDesc = book.Summary ?? book.Description ?? string.Empty;
            item.Overview = StripHtml(rawDesc);

            if (book.ReleaseDate.HasValue)
                item.PremiereDate = book.ReleaseDate;

            if (!string.IsNullOrEmpty(book.PublisherName))
                item.AddStudio(book.PublisherName);

            if (float.TryParse(book.Rating, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var rating))
                item.CommunityRating = rating;

            if (book.SeriesPrimary != null)
            {
                item.SeriesName = book.SeriesPrimary.Name;
                if (!string.IsNullOrEmpty(book.SeriesPrimary.Position) &&
                    float.TryParse(book.SeriesPrimary.Position.Replace(',', '.'),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var idx))
                    item.IndexNumber = (int)idx;
            }

            if (book.Genres != null)
            {
                item.Genres = book.Genres
                    .Where(g => g.Type == "genre" && !string.IsNullOrEmpty(g.Name))
                    .Select(g => g.Name!)
                    .ToArray();
            }

            if (!string.IsNullOrEmpty(book.Asin))
                item.SetProviderId("Audible", book.Asin);

            // Authors
            if (book.Authors != null)
            {
                foreach (var a in book.Authors.Where(a => !string.IsNullOrEmpty(a.Name)))
                {
                    result.AddPerson(new PersonInfo
                    {
                        Name = a.Name!,
                        Type = PersonKind.Author
                    });
                }
            }

            // Narrators
            if (book.Narrators != null)
            {
                foreach (var n in book.Narrators.Where(n => !string.IsNullOrEmpty(n.Name)))
                {
                    result.AddPerson(new PersonInfo
                    {
                        Name = n.Name!,
                        Type = PersonKind.Unknown,
                        Role = "Narrator"
                    });
                }
            }

            return result;
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SongInfo searchInfo, CancellationToken cancellationToken)
        {
            var results = new List<RemoteSearchResult>();
            var region = Plugin.Instance?.Configuration.Region ?? "fr";

            var asin = searchInfo.GetProviderId("Audible");
            if (!string.IsNullOrEmpty(asin))
            {
                var book = await GetByAsin(asin, region, cancellationToken).ConfigureAwait(false);
                if (book != null)
                {
                    results.Add(BookToSearchResult(book));
                    return results;
                }
            }

            // Search Audible catalog
            var title = searchInfo.Name;
            var author = searchInfo.AlbumArtists?.Count > 0 ? searchInfo.AlbumArtists[0] : null;
            if (!string.IsNullOrEmpty(title))
            {
                var products = await SearchCatalog(title, author, region, cancellationToken).ConfigureAwait(false);
                foreach (var p in products)
                {
                    results.Add(new RemoteSearchResult
                    {
                        Name = p.Title ?? string.Empty,
                        Overview = StripHtml(p.PublisherSummary ?? string.Empty),
                        ProviderIds = new Dictionary<string, string> { ["Audible"] = p.Asin ?? string.Empty }
                    });
                }
            }

            return results;
        }

        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            return client.GetAsync(new Uri(url), cancellationToken);
        }

        private async Task<AudnexBook?> GetByAsin(string asin, string region, CancellationToken ct)
        {
            try
            {
                var url = $"{AudnexBase}/books/{Uri.EscapeDataString(asin.ToUpperInvariant())}?region={region}";
                var client = _httpClientFactory.CreateClient(HttpClientName);
                var resp = await client.GetAsync(url, ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) return null;
                var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return JsonSerializer.Deserialize<AudnexBook>(json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "audnex.us lookup failed for ASIN {Asin}", asin);
                return null;
            }
        }

        private async Task<string?> SearchAsin(string title, string? author, string region, CancellationToken ct)
        {
            var products = await SearchCatalog(title, author, region, ct).ConfigureAwait(false);
            var titleNorm = Normalize(StripPrefix(title));

            foreach (var p in products)
            {
                if (Normalize(p.Title ?? string.Empty) == titleNorm)
                    return p.Asin;
            }
            // Partial match
            foreach (var p in products)
            {
                var pNorm = Normalize(p.Title ?? string.Empty);
                if (pNorm.StartsWith(titleNorm, StringComparison.Ordinal) ||
                    titleNorm.StartsWith(pNorm + " ", StringComparison.Ordinal))
                    return p.Asin;
            }
            return products.FirstOrDefault()?.Asin;
        }

        private async Task<List<AudibleProduct>> SearchCatalog(string title, string? author, string region, CancellationToken ct)
        {
            try
            {
                var tld = RegionToTld(region);
                var lang = RegionToLanguage(region);
                var q = $"{AudibleApiBase}.{tld}/1.0/catalog/products" +
                        $"?title={HttpUtility.UrlEncode(StripPrefix(title))}" +
                        $"&num_results=10" +
                        $"&response_groups=product_desc,product_extended_attrs,contributors" +
                        $"&language={lang}";
                if (!string.IsNullOrEmpty(author))
                    q += $"&author={HttpUtility.UrlEncode(author)}";

                var client = _httpClientFactory.CreateClient(HttpClientName);
                var resp = await client.GetAsync(q, ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) return new List<AudibleProduct>();
                var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var sr = JsonSerializer.Deserialize<AudibleSearchResult>(json);
                return sr?.Products ?? new List<AudibleProduct>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Audible catalog search failed for title {Title}", title);
                return new List<AudibleProduct>();
            }
        }

        private static RemoteSearchResult BookToSearchResult(AudnexBook book) => new()
        {
            Name = book.Title ?? string.Empty,
            Overview = StripHtml(book.Summary ?? book.Description ?? string.Empty),
            ImageUrl = book.Image,
            PremiereDate = book.ReleaseDate,
            ProviderIds = new Dictionary<string, string> { ["Audible"] = book.Asin ?? string.Empty }
        };

        private static string RegionToTld(string region) => region.ToLowerInvariant() switch
        {
            "fr" => "fr",
            "de" => "de",
            "uk" or "gb" => "co.uk",
            "au" => "com.au",
            "ca" => "ca",
            "jp" => "co.jp",
            "it" => "it",
            "es" => "es",
            _ => "com"
        };

        private static string RegionToLanguage(string region) => region.ToLowerInvariant() switch
        {
            "fr" => "french",
            "de" => "german",
            "jp" => "japanese",
            "it" => "italian",
            "es" => "spanish",
            _ => "english"
        };

        private static string StripHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return html;
            var text = Regex.Replace(html, "<[^>]+>", string.Empty);
            text = text.Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">")
                       .Replace("&quot;", "\"").Replace("&#39;", "'").Replace("&nbsp;", " ");
            return Regex.Replace(text, @"\s+", " ").Trim();
        }

        private static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = s.ToLowerInvariant();
            s = s.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder();
            foreach (var c in s)
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) !=
                    System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            s = sb.ToString();
            s = Regex.Replace(s, @"['\-,\.!?;:]", " ");
            return Regex.Replace(s, @"\s+", " ").Trim();
        }

        private static string StripPrefix(string s)
        {
            s = Regex.Replace(s, @"^(t|vol|tome|part|partie|book)\s*[\d\.]+\s*[-–]\s*", string.Empty,
                RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"^\d+\s*[-–]\s*", string.Empty);
            return s.Trim();
        }
    }
}
