using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Audible.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Jellyfin.Plugin.Audible.Providers
{
    public class AudibleImageProvider : IRemoteImageProvider
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AudibleImageProvider> _logger;

        public AudibleImageProvider(IHttpClientFactory httpClientFactory, ILogger<AudibleImageProvider> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public string Name => "Audible";

        public bool Supports(BaseItem item) => item is AudioBook;

        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            yield return ImageType.Primary;
        }

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            var results = new List<RemoteImageInfo>();
            var asin = item.GetProviderId("Audible");
            if (string.IsNullOrEmpty(asin)) return results;

            var region = Plugin.Instance?.Configuration.Region ?? "fr";

            try
            {
                var url = $"https://api.audnex.us/books/{Uri.EscapeDataString(asin.ToUpperInvariant())}?region={region}";
                var client = _httpClientFactory.CreateClient(AudibleMetadataProvider.HttpClientName);
                var resp = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) return results;
                var json = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var book = JsonSerializer.Deserialize<AudnexBook>(json);
                if (!string.IsNullOrEmpty(book?.Image))
                {
                    results.Add(new RemoteImageInfo
                    {
                        ProviderName = Name,
                        Type = ImageType.Primary,
                        Url = book.Image
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Image fetch failed for ASIN {Asin}", asin);
            }

            return results;
        }

        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient(AudibleMetadataProvider.HttpClientName);
            return client.GetAsync(new Uri(url), cancellationToken);
        }
    }
}
