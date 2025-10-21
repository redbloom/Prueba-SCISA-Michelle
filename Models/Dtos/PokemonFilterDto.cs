namespace Prueba_SCISA_Michelle.Models.Dtos
{
    public class PokemonFilterDto
    {
        public string? Name { get; set; }
        public int? SpeciesId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
