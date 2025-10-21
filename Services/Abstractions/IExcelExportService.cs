using Prueba_SCISA_Michelle.Models.Dtos;

namespace Prueba_SCISA_Michelle.Services.Abstractions
{
    public interface IExcelExportService
    {
        Task<byte[]> GenerateAsync(IEnumerable<PokemonListItemDto> rows, CancellationToken ct = default);
    }
}
