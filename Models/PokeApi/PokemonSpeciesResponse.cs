using System.Text.Json.Serialization;

namespace Prueba_SCISA_Michelle.Models.PokeApi
{
    public sealed class PokemonSpeciesResponse
    {
        [JsonPropertyName("genera")]
        public List<PokemonGenus>? Genera { get; set; }

        [JsonPropertyName("varieties")]
        public List<PokemonSpeciesVariety>? Varieties { get; set; }
    }

    public sealed class PokemonGenus
    {
        [JsonPropertyName("genus")]
        public string Genus { get; set; } = "";

        [JsonPropertyName("language")]
        public NamedApiResource? Language { get; set; }
    }

    public sealed class PokemonSpeciesVariety
    {
        [JsonPropertyName("is_default")]
        public bool IsDefault { get; set; }

        [JsonPropertyName("pokemon")]
        public NamedApiResource? Pokemon { get; set; }
    }

    public sealed class NamedApiResource
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("url")]
        public string Url { get; set; } = "";
    }
}
