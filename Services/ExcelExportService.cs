using System.IO;
using ClosedXML.Excel;
using Prueba_SCISA_Michelle.Models.Dtos;
using Prueba_SCISA_Michelle.Services.Abstractions;

namespace Prueba_SCISA_Michelle.Services
{
    internal sealed class ExcelExportService : IExcelExportService
    {
        public Task<byte[]> GenerateAsync(IEnumerable<PokemonListItemDto> rows, CancellationToken ct = default)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Pokémon");

            ws.Cell(1, 1).Value = "Id";
            ws.Cell(1, 2).Value = "Nombre";
            ws.Cell(1, 3).Value = "Especie";
            ws.Cell(1, 4).Value = "Imagen";

            var header = ws.Range(1, 1, 1, 4);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.LightGray;
            header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var r = 2;
            foreach (var item in rows)
            {
                ct.ThrowIfCancellationRequested();

                ws.Cell(r, 1).Value = item.Id;
                ws.Cell(r, 2).Value = item.Name;
                ws.Cell(r, 3).Value = item.SpeciesName;

                var c = ws.Cell(r, 4);

               
                if (!string.IsNullOrWhiteSpace(item.ImageUrl) &&
                    Uri.TryCreate(item.ImageUrl, UriKind.Absolute, out var uri))
                {
                    var url = EscapeForFormula(uri.ToString());
                    c.FormulaA1 = $"HYPERLINK(\"{url}\", \"{url}\")";
                    c.Style.Font.FontColor = XLColor.Blue;
                    c.Style.Font.Underline = XLFontUnderlineValues.Single;
                }
                else
                {
                    
                    c.Value = item.ImageUrl ?? string.Empty;
                }

                r++;
            }

            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return Task.FromResult(ms.ToArray());
        }

        private static string EscapeForFormula(string s)
            => s.Replace("\"", "\"\"");
    }
}
