using Microsoft.AspNetCore.Mvc;
using Prueba_SCISA_Michelle.Models.Dtos;
using Prueba_SCISA_Michelle.Services.Abstractions;

namespace Prueba_SCISA_Michelle.Controllers.Api
{
    [ApiController]
    [Route("api")]
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
        public async Task<ActionResult<PagedResult<PokemonListItemDto>>> GetList(
            [FromQuery] PokemonFilterDto filter,
            CancellationToken ct)
        {
            var result = await _pokemon.SearchAsync(filter, ct);
            return Ok(result);
        }

        // GET /api/pokemon/25
        [HttpGet("pokemon/{id:int}")]
        public async Task<ActionResult<PokemonDetailDto>> GetDetails([FromRoute] int id, CancellationToken ct)
        {
            var d = await _pokemon.GetDetailsAsync(id, ct);
            if (d is null) return NotFound();
            return Ok(d);
        }

        // GET /api/species
        [HttpGet("species")]
        public async Task<ActionResult<IEnumerable<object>>> GetSpecies(CancellationToken ct)
        {
            var list = await _pokemon.GetSpeciesAsync(ct);
            return Ok(list.Select(s => new { id = s.id, name = s.name }));
        }

        // POST /api/pokemon/export  (body: PokemonFilterDto) => CSV
        [HttpPost("pokemon/export")]
        public async Task<IActionResult> Export([FromBody] PokemonFilterDto filter, CancellationToken ct)
        {
            var page = await _pokemon.SearchAsync(filter, ct);
            var bytes = await _excel.GenerateAsync(page.Items, ct);
            return File(bytes, "text/csv", "pokemon.csv");
        }

        // POST /api/email/25
        [HttpPost("email/{id:int}")]
        public async Task<IActionResult> SendOne([FromRoute] int id, CancellationToken ct)
        {
            await _email.SendOneAsync(id, ct);
            return Ok(new { message = $"Correo enviado al Pokémon con ID {id}" });
        }

        // POST /api/email/bulk   (body: PokemonFilterDto)
        [HttpPost("email/bulk")]
        public async Task<IActionResult> SendBulk([FromBody] PokemonFilterDto filter, CancellationToken ct)
        {
            var page = await _pokemon.SearchAsync(filter, ct);
            await _email.SendBulkAsync(page.Items.Select(i => i.Id), ct);
            return Ok(new { message = "Correos enviados a la lista actual", count = page.Items.Count });
        }
    }
}
