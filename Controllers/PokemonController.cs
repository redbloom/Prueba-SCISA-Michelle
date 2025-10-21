using Microsoft.AspNetCore.Mvc;
using Prueba_SCISA_Michelle.Models.Dtos;
using Prueba_SCISA_Michelle.Services.Abstractions;

namespace Prueba_SCISA_Michelle.Controllers
{
    public class PokemonController : Controller
    {
        private readonly IPokemonService _pokemon;
        private readonly IExcelExportService _excel;
        private readonly IEmailService _email;

        public PokemonController(IPokemonService pokemon, IExcelExportService excel, IEmailService email)
        {
            _pokemon = pokemon;
            _excel = excel;
            _email = email;
        }

        [HttpGet("/")]
        public IActionResult Start()
        {
            return View(); 
        }

        // Vista principal con filtros + grid
        [HttpGet]
        public IActionResult Index()
        {
            return View(new PokemonFilterDto());
        }

        // Listado (partial) para el grid
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> List(PokemonFilterDto filter, CancellationToken ct)
        {
            try
            {
                var paged = await _pokemon.SearchAsync(filter, ct);
                return PartialView("_PokemonList", paged);
            }
            catch (OperationCanceledException)
            {
                // 499 = Client Closed Request (convención)
                return StatusCode(499);
            }
            catch
            {
                return PartialView("_PokemonList", new PagedResult<PokemonListItemDto>
                {
                    Items = new(),
                    Page = filter.Page,
                    PageSize = filter.PageSize,
                    TotalCount = 0
                });
            }
        }

        // Catálogo de especies (para llenar el select)
        [HttpGet]
        public async Task<IActionResult> Species(CancellationToken ct)
        {
            var species = await _pokemon.GetSpeciesAsync(ct);
            return Json(species.Select(s => new { id = s.id, name = s.name }));
        }

        // Detalle (opcional)
        [HttpGet]
        public async Task<IActionResult> Details(int id, CancellationToken ct)
        {
            var d = await _pokemon.GetDetailsAsync(id, ct);
            if (d is null) return NotFound();
            return View(d); // Views/Pokemon/Details.cshtml
        }

        // Exportación
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Export(PokemonFilterDto filter, CancellationToken ct)
        {
            var page = await _pokemon.SearchAsync(filter, ct);
            var bytes = await _excel.GenerateAsync(page.Items, ct);

            // ⬇️ Si generas XLSX (recomendado con IExcelExportService):
            const string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            return File(bytes, contentType, "pokemon.xlsx");

            // ⬇️ Si en realidad generas CSV, usa esto en su lugar:
            // return File(bytes, "text/csv", "pokemon.csv");
        }

        // Enviar correo a un Pokémon
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendOne(int id, CancellationToken ct)
        {
            await _email.SendOneAsync(id, ct);
            return Ok($"Correo enviado al Pokémon con ID {id}");
        }

        // Enviar correos a toda la página actual
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendAll(PokemonFilterDto filter, CancellationToken ct)
        {
            var page = await _pokemon.SearchAsync(filter, ct);
            await _email.SendBulkAsync(page.Items.Select(i => i.Id), ct);
            return Ok("Correos enviados a la lista actual");
        }
    }
}
