using System.Text.Json.Serialization;

namespace Prueba_SCISA_Michelle.Models.PokeApi
{
    // GET https://pokeapi.co/api/v2/pokemon/{id|name}
    public sealed class PokemonDetailResponse
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("weight")]
        public int Weight { get; set; }

        [JsonPropertyName("base_experience")]
        public int BaseExperience { get; set; }

        [JsonPropertyName("species")]
        public NamedApiResource? Species { get; set; }
    }
}
