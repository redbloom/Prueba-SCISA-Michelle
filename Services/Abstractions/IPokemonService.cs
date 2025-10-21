using Prueba_SCISA_Michelle.Models.Dtos;

namespace Prueba_SCISA_Michelle.Services.Abstractions
{
    public interface IPokemonService
    {
        Task<PagedResult<PokemonListItemDto>> SearchAsync(PokemonFilterDto filter, CancellationToken ct = default);
        Task<IReadOnlyList<(int id, string name)>> GetSpeciesAsync(CancellationToken ct = default);
        Task<PokemonDetailDto?> GetDetailsAsync(int id, CancellationToken ct = default);
    }
}
