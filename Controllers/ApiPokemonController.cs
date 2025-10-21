using Microsoft.AspNetCore.Mvc;
using Prueba_SCISA_Michelle.Models.Dtos;
using Prueba_SCISA_Michelle.Services.Abstractions;

namespace Prueba_SCISA_Michelle.Controllers.Api
{
    [ApiController]
    [Route("api")]
    [Produces("application/json")]
    public class ApiPokemonController : ControllerBase
    {
        private readonly IPokemonService _pokemon;
        private readonly IExcelExportService _excel;
        private readonly IEmailService _email;

        public ApiPokemonController(IPokemonService pokemon, IExcelExportService excel, IEmailService email)
        {
            _pokemon = pokemon;
            _excel = excel;
            _email = email;
        }

        // GET /api/pokemon?name=&speciesId=&page=1&pageSize=20
        [HttpGet("pokemon")]
        [ProducesResponseType(typeof(PagedResult<PokemonListItemDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResult<PokemonListItemDto>>> GetList(
            [FromQuery] PokemonFilterDto filter,
            CancellationToken ct = default)
        {
            try
            {
                // Saneamos valores mínimos por si vienen 0/negativos desde Postman
                filter.Page = Math.Max(1, filter.Page);
                filter.PageSize = Math.Clamp(filter.PageSize <= 0 ? 20 : filter.PageSize, 1, 100);

                var result = await _pokemon.SearchAsync(filter, ct);
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                // 499 Client Closed Request (usado por Nginx); aquí 499 “custom”
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                return Problem(title: "Error al obtener la lista de Pokémon", detail: ex.Message, statusCode: 500);
            }
        }

        // GET /api/pokemon/25
        [HttpGet("pokemon/{id:int}")]
        [ProducesResponseType(typeof(PokemonDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PokemonDetailDto>> GetDetails([FromRoute] int id, CancellationToken ct = default)
        {
            try
            {
                var d = await _pokemon.GetDetailsAsync(id, ct);
                if (d is null) return NotFound();
                return Ok(d);
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                return Problem(title: "Error al obtener el detalle del Pokémon", detail: ex.Message, statusCode: 500);
            }
        }

        // GET /api/species
        // Nota: devuelve (id, name) tal como lo trae el catálogo de species de la API.
        [HttpGet("species")]
        [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<object>>> GetSpecies(CancellationToken ct = default)
        {
            try
            {
                var list = await _pokemon.GetSpeciesAsync(ct);
                return Ok(list.Select(s => new { id = s.id, name = s.name }));
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                return Problem(title: "Error al obtener el catálogo de especies", detail: ex.Message, statusCode: 500);
            }
        }

        // POST /api/pokemon/export  (body: PokemonFilterDto) => CSV
        [HttpPost("pokemon/export")]
        [Produces("text/csv")]
        public async Task<IActionResult> Export([FromBody] PokemonFilterDto filter, CancellationToken ct = default)
        {
            try
            {
                filter.Page = Math.Max(1, filter.Page);
                filter.PageSize = Math.Clamp(filter.PageSize <= 0 ? 20 : filter.PageSize, 1, 100);

                var page = await _pokemon.SearchAsync(filter, ct);

                // Si no hay datos, regresamos CSV vacío con encabezados de todos modos
                var bytes = await _excel.GenerateAsync(page.Items, ct);

                var fileName = $"pokemon_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
                return File(bytes, "text/csv; charset=utf-8", fileName);
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                return Problem(title: "Error al generar el CSV", detail: ex.Message, statusCode: 500);
            }
        }

        // POST /api/email/25
        [HttpPost("email/{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> SendOne([FromRoute] int id, CancellationToken ct = default)
        {
            try
            {
                await _email.SendOneAsync(id, ct);
                return Ok(new { message = $"Correo enviado al Pokémon con ID {id}" });
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                return Problem(title: "Error al enviar el correo individual", detail: ex.Message, statusCode: 500);
            }
        }

        // POST /api/email/bulk   (body: PokemonFilterDto)
        [HttpPost("email/bulk")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> SendBulk([FromBody] PokemonFilterDto filter, CancellationToken ct = default)
        {
            try
            {
                filter.Page = Math.Max(1, filter.Page);
                filter.PageSize = Math.Clamp(filter.PageSize <= 0 ? 20 : filter.PageSize, 1, 100);

                var page = await _pokemon.SearchAsync(filter, ct);

                if (page.Items.Count == 0)
                    return Ok(new { message = "No hay Pokémon en la lista actual para enviar correos.", count = 0 });

                await _email.SendBulkAsync(page.Items.Select(i => i.Id), ct);
                return Ok(new { message = "Correos enviados a la lista actual", count = page.Items.Count });
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                return Problem(title: "Error al enviar correos masivos", detail: ex.Message, statusCode: 500);
            }
        }
    }
}
