using System.Text.Json.Serialization;

namespace Prueba_SCISA_Michelle.Models.PokeApi
{
    public sealed class PokemonSpeciesResponse
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("genera")]
        public List<PokemonGenus>? Genera { get; set; }
    }

    public sealed class PokemonGenus
    {
        [JsonPropertyName("genus")]
        public string Genus { get; set; } = "";

        [JsonPropertyName("language")]
        public NamedApiResource? Language { get; set; }
    }

    public sealed class NamedApiResource
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = ""; 
    }
}
