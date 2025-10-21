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

        [JsonPropertyName("sprites")]
        public PokemonSprites? Sprites { get; set; }
    }

    public sealed class PokemonSprites
    {
        [JsonPropertyName("front_default")]
        public string? FrontDefault { get; set; }

        [JsonPropertyName("other")]
        public PokemonOtherSprites? Other { get; set; }
    }

    public sealed class PokemonOtherSprites
    {
        [JsonPropertyName("dream_world")]
        public PokemonDreamWorld? DreamWorld { get; set; }
    }

    public sealed class PokemonDreamWorld
    {
        [JsonPropertyName("front_default")]
        public string? FrontDefault { get; set; }
    }
}
