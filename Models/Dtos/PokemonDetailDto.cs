namespace Prueba_SCISA_Michelle.Models.Dtos
{
    public sealed class PokemonDetailDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Height { get; set; }
        public int Weight { get; set; }
        public int BaseExperience { get; set; }
        public string ImageUrl { get; set; } = "";
        public string SpeciesName { get; set; } = "";
    }
}
