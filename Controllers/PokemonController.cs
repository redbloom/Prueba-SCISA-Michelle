//using Microsoft.AspNetCore.Mvc;
//using Prueba_SCISA_Michelle.Models.Dtos;

//namespace Prueba_SCISA_Michelle.Controllers
//{
//    public class PokemonController : Controller
//    {
//        [HttpGet("/")] 
//        public IActionResult Index()
//        {
//            return View(new PokemonFilterDto());
//        }


//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public IActionResult List(PokemonFilterDto filter)
//        {
//            // Datos de prueba
//            var items = new List<PokemonListItemDto>
//            {
//                new() { Id = 1, Name = "bulbasaur", SpeciesName = "semilla", ImageUrl = "https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/1.png" },
//                new() { Id = 2, Name = "charmander", SpeciesName = "lagarto", ImageUrl = "https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/4.png" },
//                new() { Id = 3, Name = "squirtle", SpeciesName = "tortuga", ImageUrl = "https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/7.png" }
//            };

//            var paged = new PagedResult<PokemonListItemDto>
//            {
//                Items = items,
//                Page = filter.Page,
//                PageSize = filter.PageSize,
//                TotalCount = items.Count
//            };

//            return PartialView("_PokemonList", paged);
//        }

//        [HttpGet]
//        public IActionResult Species()
//        {
//            var species = new[]
//            {
//                new { id = 1, name = "semilla" },
//                new { id = 2, name = "lagarto" },
//                new { id = 3, name = "tortuga" }
//            };

//            return Json(species);
//        }

//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public IActionResult Export(PokemonFilterDto filter)
//        {
//            var bytes = System.Text.Encoding.UTF8.GetBytes("Demo de exportación Excel");
//            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "pokemon.xlsx");
//        }

//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public IActionResult SendOne(int id)
//        {
//            return Ok($"Correo enviado al Pokémon con ID {id}");
//        }

//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public IActionResult SendAll(PokemonFilterDto filter)
//        {
//            return Ok("Correos enviados a la lista actual");
//        }
//    }
//}
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
        public IActionResult Index()
        {
            return View(new PokemonFilterDto());
        }

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

        [HttpGet]
        public async Task<IActionResult> Species(CancellationToken ct)
        {
            var species = await _pokemon.GetSpeciesAsync(ct);
            return Json(species.Select(s => new { id = s.id, name = s.name }));
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id, CancellationToken ct)
        {
            var d = await _pokemon.GetDetailsAsync(id, ct);
            if (d is null) return NotFound();
            return View(d);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Export(PokemonFilterDto filter, CancellationToken ct)
        {
            var page = await _pokemon.SearchAsync(filter, ct);
            var bytes = await _excel.GenerateAsync(page.Items, ct);
            return File(bytes, "text/csv", "pokemon.csv");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendOne(int id, CancellationToken ct)
        {
            await _email.SendOneAsync(id, ct);
            return Ok($"Correo enviado al Pokémon con ID {id}");
        }

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

