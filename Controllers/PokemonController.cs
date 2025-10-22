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
            ViewBag.EmailAllUrl = Url.Action("SendAll", "Pokemon");
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
            const string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            return File(bytes, contentType, "pokemon.xlsx");
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
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendToEmail(int id, string toEmail, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
                return BadRequest("El correo destino es obligatorio.");

            var d = await _pokemon.GetDetailsAsync(id, ct);
            if (d is null) return NotFound();

            var name = d.Name;
            var subject = $"Información de {name}";

            byte[]? spriteBytes = null;

            if (!string.IsNullOrWhiteSpace(d.ImageUrl))
            {
                try
                {
                    using var http = new HttpClient();
                    spriteBytes = await http.GetByteArrayAsync(d.ImageUrl, ct);
                }
                catch
                {
                   
                }
            }

            var html = $@"
<div style=""font-family:Segoe UI,Arial,sans-serif;color:#0e1222"">
    <h2 style=""margin:0 0 8px"">¡Hola!</h2>
    <p style=""margin:0 0 16px"">Te comparto información de <strong style=""text-transform:capitalize"">{name}</strong>.</p>

    {(spriteBytes is null ? $"<p style='margin:0 0 12px;font-style:italic;color:#888'>Imagen no disponible</p>" : "")}

    <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0""
           style=""border-collapse:collapse;min-width:320px;background:#f6f8ff;border-radius:12px;overflow:hidden;margin-top:12px"">
        <tbody>
            <tr><td style=""padding:12px 16px;border-bottom:1px solid #e6e8f5;width:40%;color:#6b7280"">ID</td><td style=""padding:12px 16px"">{d.Id}</td></tr>
            <tr><td style=""padding:12px 16px;border-bottom:1px solid #e6e8f5;color:#6b7280"">Nombre</td><td style=""padding:12px 16px;text-transform:capitalize"">{d.Name}</td></tr>
            <tr><td style=""padding:12px 16px;border-bottom:1px solid #e6e8f5;color:#6b7280"">Especie</td><td style=""padding:12px 16px;text-transform:capitalize"">{d.SpeciesName}</td></tr>
            <tr><td style=""padding:12px 16px;border-bottom:1px solid #e6e8f5;color:#6b7280"">Altura</td><td style=""padding:12px 16px"">{d.HeightMeters:N1} m</td></tr>
            <tr><td style=""padding:12px 16px;border-bottom:1px solid #e6e8f5;color:#6b7280"">Peso</td><td style=""padding:12px 16px"">{d.WeightKg:N1} kg</td></tr>
            <tr><td style=""padding:12px 16px;color:#6b7280"">Experiencia base</td><td style=""padding:12px 16px"">{d.BaseExperience}</td></tr>
        </tbody>
    </table>

    <p style=""margin:16px 0 0;font-size:12px;color:#6b7280"">
        Sprite fuente: <a href=""{d.ImageUrl}"">{d.ImageUrl}</a>
    </p>
</div>";


            if (spriteBytes is not null)
            {
                var images = new Dictionary<string, (byte[] bytes, string mime, string fileName)>
        {
            { "sprite", (spriteBytes, "image/png", $"{name}.png") }
        };

                await _email.SendCustomAsync(toEmail, subject, html, images, ct);
            }
            else
            {
                await _email.SendCustomAsync(toEmail, subject, html, ct);
            }

            TempData["Toast"] = $"Correo enviado a {toEmail}";
            return RedirectToAction(nameof(Index));
        }
    }
  }
