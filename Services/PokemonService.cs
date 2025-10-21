using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Prueba_SCISA_Michelle.Models.Dtos;
using Prueba_SCISA_Michelle.Models.PokeApi;
using Prueba_SCISA_Michelle.Services.Abstractions;

namespace Prueba_SCISA_Michelle.Services
{
    internal sealed class PokemonService : IPokemonService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;
        private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

        public PokemonService(IHttpClientFactory httpClientFactory, IMemoryCache cache)
        {
            _httpClientFactory = httpClientFactory;
            _cache = cache;
        }

        public async Task<PagedResult<PokemonListItemDto>> SearchAsync(PokemonFilterDto filter, CancellationToken ct = default)
        {
            var page = Math.Max(1, filter.Page);
            var size = Math.Clamp(filter.PageSize, 1, 100);
            var offset = (page - 1) * size;

            // 1) Búsqueda exacta por nombre
            if (!string.IsNullOrWhiteSpace(filter.Name))
            {
                var name = filter.Name!.Trim().ToLowerInvariant();
                PokemonDetailResponse? detail = null;
                try { detail = await GetPokemonDetailAsync(name, ct); } catch { /* ignore */ }

                var list = new List<PokemonListItemDto>();
                if (detail is not null)
                {
                    var speciesName = await GetSpeciesNameAsync(detail.Id, ct);
                    list.Add(new PokemonListItemDto
                    {
                        Id = detail.Id,
                        Name = detail.Name,
                        SpeciesName = speciesName,
                        ImageUrl = DefaultSprite(detail.Id)
                    });
                }

                return new PagedResult<PokemonListItemDto>
                {
                    Items = list,
                    Page = 1,
                    PageSize = size,
                    TotalCount = list.Count
                };
            }

            // 2) Filtro por especie (tomamos speciesId ~ pokemonId como mapeo simple para la prueba)
            if (filter.SpeciesId.HasValue)
            {
                var sid = filter.SpeciesId.Value;
                var speciesName = await GetSpeciesNameByIdAsync(sid, ct);
                PokemonDetailResponse? detail = null;
                try { detail = await GetPokemonDetailAsync(sid, ct); } catch { /* ignore */ }

                var list = new List<PokemonListItemDto>();
                if (detail is not null)
                {
                    list.Add(new PokemonListItemDto
                    {
                        Id = detail.Id,
                        Name = detail.Name,
                        SpeciesName = speciesName,
                        ImageUrl = DefaultSprite(detail.Id)
                    });
                }

                return new PagedResult<PokemonListItemDto>
                {
                    Items = list,
                    Page = 1,
                    PageSize = size,
                    TotalCount = list.Count
                };
            }

            // 3) Listado general con paginación
            var client = _httpClientFactory.CreateClient("pokeapi");
            var listResp = await client.GetFromJsonAsync<PokemonListResponse>($"pokemon?offset={offset}&limit={size}", _json, ct)
                           ?? new PokemonListResponse();

            var results = new List<PokemonListItemDto>(listResp.Results?.Count ?? 0);

            if (listResp.Results is not null)
            {
                foreach (var r in listResp.Results)
                {
                    ct.ThrowIfCancellationRequested();
                    var id = ExtractId(r.Url);
                    PokemonDetailResponse? detail = null;
                    try { detail = await GetPokemonDetailAsync(id, ct); } catch { continue; }

                    var speciesName = await GetSpeciesNameAsync(detail!.Id, ct);

                    results.Add(new PokemonListItemDto
                    {
                        Id = detail.Id,
                        Name = detail.Name,
                        SpeciesName = speciesName,
                        ImageUrl = DefaultSprite(detail.Id)
                    });
                }
            }

            return new PagedResult<PokemonListItemDto>
            {
                Items = results,
                Page = page,
                PageSize = size,
                TotalCount = listResp.Count
            };
        }

        public async Task<IReadOnlyList<(int id, string name)>> GetSpeciesAsync(CancellationToken ct = default)
        {
            if (_cache.TryGetValue("species_all", out IReadOnlyList<(int, string)>? cached) && cached is not null)
                return cached;

            var client = _httpClientFactory.CreateClient("pokeapi");
            var items = new List<(int id, string name)>();

            // Leemos páginas con JsonDocument para no depender de un DTO de "page"
            string? next = "pokemon-species?limit=500&offset=0";
            while (!string.IsNullOrEmpty(next))
            {
                ct.ThrowIfCancellationRequested();
                using var stream = await client.GetStreamAsync(next, ct);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

                var root = doc.RootElement;

                if (root.TryGetProperty("results", out var resultsEl) && resultsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in resultsEl.EnumerateArray())
                    {
                        var name = item.GetProperty("name").GetString() ?? "";
                        var url = item.GetProperty("url").GetString() ?? "";
                        var id = ExtractId(url);
                        items.Add((id, name));
                    }
                }

                next = root.TryGetProperty("next", out var nextEl) && nextEl.ValueKind == JsonValueKind.String
                    ? nextEl.GetString()
                    : null;
            }

            var ordered = items.OrderBy(x => x.id).ToList();
            _cache.Set("species_all", ordered, TimeSpan.FromHours(1));
            return ordered;
        }

        public async Task<PokemonDetailDto?> GetDetailsAsync(int id, CancellationToken ct = default)
        {
            PokemonDetailResponse? d;
            try { d = await GetPokemonDetailAsync(id, ct); } catch { return null; }
            if (d is null) return null;

            var speciesName = await GetSpeciesNameAsync(d.Id, ct);
            return new PokemonDetailDto
            {
                Id = d.Id,
                Name = d.Name,
                Height = d.Height,
                Weight = d.Weight,
                BaseExperience = d.BaseExperience,
                ImageUrl = DefaultSprite(d.Id),
                SpeciesName = speciesName
            };
        }

        // ----------------- Helpers -----------------

        private async Task<PokemonDetailResponse?> GetPokemonDetailAsync(object idOrName, CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient("pokeapi");
            return await client.GetFromJsonAsync<PokemonDetailResponse>($"pokemon/{idOrName}", _json, ct);
        }

        private async Task<string> GetSpeciesNameAsync(int pokemonId, CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient("pokeapi");
            var species = await client.GetFromJsonAsync<PokemonSpeciesResponse>($"pokemon-species/{pokemonId}", _json, ct);
            return species?.Name ?? "";
        }

        private async Task<string> GetSpeciesNameByIdAsync(int speciesId, CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient("pokeapi");
            var species = await client.GetFromJsonAsync<PokemonSpeciesResponse>($"pokemon-species/{speciesId}", _json, ct);
            return species?.Name ?? "";
        }

        private static int ExtractId(string url)
        {
            var parts = url.TrimEnd('/').Split('/');
            return int.Parse(parts[^1]);
        }

        private static string DefaultSprite(int id)
            => $"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/{id}.png";
    }
}
