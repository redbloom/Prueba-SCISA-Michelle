using System.Text.Json.Serialization;

namespace Prueba_SCISA_Michelle.Models.PokeApi
{

    public sealed class PokemonListResponse
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("results")]
        public List<PokemonRef> Results { get; set; } = new();
    }

    public sealed class PokemonRef
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("url")]
        public string Url { get; set; } = "";
    }
}
