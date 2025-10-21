namespace Prueba_SCISA_Michelle.Models.Dtos
{
    public sealed class PokemonDetailDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Height { get; set; }          // dm
        public int Weight { get; set; }          // hg
        public int BaseExperience { get; set; }
        public string ImageUrl { get; set; } = "";
        public string SpeciesName { get; set; } = "";

        public decimal HeightMeters => Height / 10.0m;
        public decimal WeightKg => Weight / 10.0m;
    }

}
