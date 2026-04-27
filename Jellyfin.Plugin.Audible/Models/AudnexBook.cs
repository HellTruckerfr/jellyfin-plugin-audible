using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Audible.Models
{
    public class AudnexBook
    {
        [JsonPropertyName("asin")]
        public string? Asin { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("subtitle")]
        public string? Subtitle { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("image")]
        public string? Image { get; set; }

        [JsonPropertyName("authors")]
        public List<AudnexPerson>? Authors { get; set; }

        [JsonPropertyName("narrators")]
        public List<AudnexPerson>? Narrators { get; set; }

        [JsonPropertyName("publisherName")]
        public string? PublisherName { get; set; }

        [JsonPropertyName("rating")]
        public string? Rating { get; set; }

        [JsonPropertyName("releaseDate")]
        public DateTime? ReleaseDate { get; set; }

        [JsonPropertyName("language")]
        public string? Language { get; set; }

        [JsonPropertyName("seriesPrimary")]
        public AudnexSeries? SeriesPrimary { get; set; }

        [JsonPropertyName("genres")]
        public List<AudnexGenre>? Genres { get; set; }
    }

    public class AudnexPerson
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("asin")]
        public string? Asin { get; set; }
    }

    public class AudnexSeries
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("position")]
        public string? Position { get; set; }

        [JsonPropertyName("asin")]
        public string? Asin { get; set; }
    }

    public class AudnexGenre
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }

    public class AudibleSearchResult
    {
        [JsonPropertyName("total_results")]
        public int TotalResults { get; set; }

        [JsonPropertyName("products")]
        public List<AudibleProduct>? Products { get; set; }
    }

    public class AudibleProduct
    {
        [JsonPropertyName("asin")]
        public string? Asin { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("authors")]
        public List<AudnexPerson>? Authors { get; set; }

        [JsonPropertyName("publisher_summary")]
        public string? PublisherSummary { get; set; }

        [JsonPropertyName("publisher_name")]
        public string? PublisherName { get; set; }
    }
}
