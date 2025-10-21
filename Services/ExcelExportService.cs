using System.Text;
using Prueba_SCISA_Michelle.Models.Dtos;
using Prueba_SCISA_Michelle.Services.Abstractions;

namespace Prueba_SCISA_Michelle.Services
{
    internal sealed class ExcelExportService : IExcelExportService
    {
        public Task<byte[]> GenerateAsync(IEnumerable<PokemonListItemDto> rows, CancellationToken ct = default)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Id,Nombre,Especie,Imagen");

            foreach (var r in rows)
            {
                ct.ThrowIfCancellationRequested();
                sb.AppendLine($"{r.Id},\"{r.Name}\",\"{r.SpeciesName}\",\"{r.ImageUrl}\"");
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return Task.FromResult(bytes);
        }
    }
}
