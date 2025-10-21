using System.Text.Json.Serialization;

namespace Prueba_SCISA_Michelle.Models.PokeApi
{

    public sealed class PokemonSpeciesResponse
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
    }
}
