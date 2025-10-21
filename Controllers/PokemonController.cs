using Microsoft.AspNetCore.Mvc;
using Prueba_SCISA_Michelle.Models.Dtos;

namespace Prueba_SCISA_Michelle.Controllers
{
    public class PokemonController : Controller
    {
        [HttpGet("/")] 
        public IActionResult Index()
        {
            return View(new PokemonFilterDto());
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult List(PokemonFilterDto filter)
        {
            // Datos de prueba
            var items = new List<PokemonListItemDto>
            {
                new() { Id = 1, Name = "bulbasaur", SpeciesName = "semilla", ImageUrl = "https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/1.png" },
                new() { Id = 2, Name = "charmander", SpeciesName = "lagarto", ImageUrl = "https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/4.png" },
                new() { Id = 3, Name = "squirtle", SpeciesName = "tortuga", ImageUrl = "https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/7.png" }
            };

            var paged = new PagedResult<PokemonListItemDto>
            {
                Items = items,
                Page = filter.Page,
                PageSize = filter.PageSize,
                TotalCount = items.Count
            };

            return PartialView("_PokemonList", paged);
        }

        [HttpGet]
        public IActionResult Species()
        {
            var species = new[]
            {
                new { id = 1, name = "semilla" },
                new { id = 2, name = "lagarto" },
                new { id = 3, name = "tortuga" }
            };

            return Json(species);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Export(PokemonFilterDto filter)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes("Demo de exportación Excel");
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "pokemon.xlsx");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SendOne(int id)
        {
            return Ok($"Correo enviado al Pokémon con ID {id}");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SendAll(PokemonFilterDto filter)
        {
            return Ok("Correos enviados a la lista actual");
        }
    }
}
