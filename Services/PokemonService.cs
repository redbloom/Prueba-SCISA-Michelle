using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Prueba_SCISA_Michelle.Models.Dtos;
using Prueba_SCISA_Michelle.Models.PokeApi;
using Prueba_SCISA_Michelle.Services.Abstractions;
using Prueba_SCISA_Michelle.Services.Helpers;

namespace Prueba_SCISA_Michelle.Services
{
    internal sealed class PokemonService : IPokemonService
    {
        private readonly IHttpClientFactory _http;
        private readonly IMemoryCache _cache;
        private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

        public PokemonService(IHttpClientFactory httpClientFactory, IMemoryCache cache)
        {
            _http = httpClientFactory;
            _cache = cache;
        }

        public async Task<PagedResult<PokemonListItemDto>> SearchAsync(PokemonFilterDto filter, CancellationToken ct = default)
        {
            var page = Math.Max(1, filter.Page);
            var size = Math.Clamp(filter.PageSize, 1, 100);
            var offset = (page - 1) * size;
            var hasName = !string.IsNullOrWhiteSpace(filter.Name);
            var hasSpecies = filter.SpeciesId.HasValue;

            if (hasSpecies && !hasName)
            {
                var ids = await GetPokemonIdsBySpeciesAsync(filter.SpeciesId!.Value, ct);
                var pageIds = ids.Skip(offset).Take(size).ToList();
                var genus = await GetGenusAsync(filter.SpeciesId!.Value, ct);

                var items = pageIds.Select(id => new PokemonListItemDto
                {
                    Id = id,
                    Name = GetNameFromIndexSync(id),
                    ImageUrl = PokeApiHelper.Sprite(id),
                    SpeciesName = genus
                }).ToList();

                return new PagedResult<PokemonListItemDto> { Items = items, Page = page, PageSize = size, TotalCount = ids.Count };
            }

            if (hasName && !hasSpecies)
            {
                var all = await GetAllPokemonIndexAsync(ct);
                var needle = PokeApiHelper.Normalize(filter.Name!);
                var filtered = all.Where(t => t.norm.StartsWith(needle) || t.norm.Contains(needle))
                                  .Select(t => (t.id, t.name)).ToList();

                var total = filtered.Count;
                var pageRows = filtered.Skip(offset).Take(size).ToList();
                var genusMap = await PrefetchGenusForPageAsync(pageRows.Select(x => x.id), ct);

                var items = pageRows.Select(t => new PokemonListItemDto
                {
                    Id = t.id,
                    Name = t.name,
                    ImageUrl = PokeApiHelper.Sprite(t.id),
                    SpeciesName = genusMap.TryGetValue(t.id, out var g) ? g : ""
                }).ToList();

                return new PagedResult<PokemonListItemDto> { Items = items, Page = page, PageSize = size, TotalCount = total };
            }

            if (hasName && hasSpecies)
            {
                var ids = await GetPokemonIdsBySpeciesAsync(filter.SpeciesId!.Value, ct);
                var idSet = ids.Count < 4096 ? ids.ToHashSet() : new HashSet<int>(ids);
                var all = await GetAllPokemonIndexAsync(ct);
                var needle = PokeApiHelper.Normalize(filter.Name!);

                var filtered = all.Where(t => (t.norm.StartsWith(needle) || t.norm.Contains(needle)) && idSet.Contains(t.id))
                                  .Select(t => (t.id, t.name)).ToList();

                var total = filtered.Count;
                var pageRows = filtered.Skip(offset).Take(size).ToList();
                var genus = await GetGenusAsync(filter.SpeciesId!.Value, ct);

                var items = pageRows.Select(t => new PokemonListItemDto
                {
                    Id = t.id,
                    Name = t.name,
                    ImageUrl = PokeApiHelper.Sprite(t.id),
                    SpeciesName = genus
                }).ToList();

                return new PagedResult<PokemonListItemDto> { Items = items, Page = page, PageSize = size, TotalCount = total };
            }

            var client = _http.CreateClient("pokeapi");
            var list = await client.GetFromJsonAsync<PokemonListResponse>($"pokemon?offset={offset}&limit={size}", _json, ct) ?? new PokemonListResponse();
            var basic = (list.Results ?? new()).Select(r => (id: PokeApiHelper.ExtractId(r.Url), name: r.Name)).ToList();
            var genusMapDefault = await PrefetchGenusForPageAsync(basic.Select(b => b.id), ct);

            var results = basic.Select(b => new PokemonListItemDto
            {
                Id = b.id,
                Name = b.name,
                ImageUrl = PokeApiHelper.Sprite(b.id),
                SpeciesName = genusMapDefault.TryGetValue(b.id, out var g) ? g : ""
            }).ToList();

            return new PagedResult<PokemonListItemDto> { Items = results, Page = page, PageSize = size, TotalCount = list.Count };
        }

        public async Task<IReadOnlyList<(int id, string name)>> GetSpeciesAsync(CancellationToken ct = default)
        {
            if (_cache.TryGetValue("species_basic", out IReadOnlyList<(int, string)>? cached) && cached is not null)
                return cached;

            var client = _http.CreateClient("pokeapi");
            var list = new List<(int id, string name)>();
            string? next = "pokemon-species?limit=200&offset=0";

            while (!string.IsNullOrEmpty(next))
            {
                using var stream = await client.GetStreamAsync(next, ct);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                var root = doc.RootElement;

                if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
                {
                    var pageSpecies = results.EnumerateArray()
                        .Select(r =>
                        {
                            var url = r.GetProperty("url").GetString() ?? "";
                            var id = PokeApiHelper.ExtractId(url);
                            return id;
                        })
                        .ToList();

                    using var sem = new SemaphoreSlim(6);
                    var tasks = pageSpecies.Select(async id =>
                    {
                        await sem.WaitAsync(ct);
                        try
                        {
                            var genus = await GetGenusAsync(id, ct);
                            lock (list) list.Add((id, genus));
                        }
                        finally { sem.Release(); }
                    });
                    await Task.WhenAll(tasks);
                }

                next = root.TryGetProperty("next", out var nextEl) && nextEl.ValueKind == JsonValueKind.String
                    ? nextEl.GetString()
                    : null;
            }

            var ordered = list.OrderBy(x => x.name, StringComparer.OrdinalIgnoreCase).ToList();
            _cache.Set("species_basic", ordered, TimeSpan.FromHours(3));
            return ordered;
        }

        public async Task<PokemonDetailDto?> GetDetailsAsync(int id, CancellationToken ct = default)
        {
            var client = _http.CreateClient("pokeapi");
            PokemonDetailResponse? d;
            try { d = await client.GetFromJsonAsync<PokemonDetailResponse>($"pokemon/{id}", _json, ct); } catch { return null; }
            if (d is null) return null;

            var speciesId = d.Species?.Url is string su && !string.IsNullOrWhiteSpace(su) ? PokeApiHelper.ExtractId(su) : d.Id;
            var genus = await GetGenusAsync(speciesId, ct);

            return new PokemonDetailDto
            {
                Id = d.Id,
                Name = d.Name,
                Height = d.Height,
                Weight = d.Weight,
                BaseExperience = d.BaseExperience,
                ImageUrl = PokeApiHelper.Sprite(d.Id),
                SpeciesName = genus
            };
        }

        private async Task<IReadOnlyList<int>> GetPokemonIdsBySpeciesAsync(int speciesId, CancellationToken ct)
        {
            var key = $"species_varieties_ids_{speciesId}";
            if (_cache.TryGetValue(key, out IReadOnlyList<int>? cached) && cached is not null) return cached;

            var client = _http.CreateClient("pokeapi");
            PokemonSpeciesResponse? species = null;
            try { species = await client.GetFromJsonAsync<PokemonSpeciesResponse>($"pokemon-species/{speciesId}", _json, ct); } catch { }

            var list = new List<int>();
            if (species?.Varieties is { Count: > 0 })
            {
                foreach (var v in species.Varieties)
                {
                    var url = v.Pokemon?.Url ?? "";
                    if (!string.IsNullOrWhiteSpace(url)) list.Add(PokeApiHelper.ExtractId(url));
                }
            }

            list.Sort();
            _cache.Set(key, list, TimeSpan.FromHours(1));
            return list;
        }

        private async Task<string> GetGenusAsync(int speciesId, CancellationToken ct)
        {
            var key = $"species_genus_{speciesId}";
            if (_cache.TryGetValue(key, out string? cached) && !string.IsNullOrWhiteSpace(cached)) return cached!;

            var client = _http.CreateClient("pokeapi");
            PokemonSpeciesResponse? sp = null;
            try { sp = await client.GetFromJsonAsync<PokemonSpeciesResponse>($"pokemon-species/{speciesId}", _json, ct); } catch { }

            string result = string.Empty;
            if (sp?.Genera is { Count: > 0 })
            {
                var es = sp.Genera.FirstOrDefault(g => g.Language?.Name == "es")?.Genus;
                var en = sp.Genera.FirstOrDefault(g => g.Language?.Name == "en")?.Genus;
                result = !string.IsNullOrWhiteSpace(es) ? es! : (!string.IsNullOrWhiteSpace(en) ? en! : result);
            }

            _cache.Set(key, result, TimeSpan.FromHours(12));
            return result;
        }

        private async Task<IDictionary<int, string>> PrefetchGenusForPageAsync(IEnumerable<int> ids, CancellationToken ct)
        {
            var dict = new Dictionary<int, string>();
            var toFetch = new List<int>();

            foreach (var id in ids)
            {
                var key = $"species_genus_{id}";
                if (_cache.TryGetValue(key, out string? s) && !string.IsNullOrWhiteSpace(s))
                    dict[id] = s!;
                else
                    toFetch.Add(id);
            }

            if (toFetch.Count == 0) return dict;

            using var sem = new SemaphoreSlim(6);
            var tasks = toFetch.Select(async id =>
            {
                await sem.WaitAsync(ct);
                try { dict[id] = await GetGenusAsync(id, ct); }
                finally { sem.Release(); }
            });

            await Task.WhenAll(tasks);
            return dict;
        }

        private async Task<IReadOnlyList<(int id, string name, string norm)>> GetAllPokemonIndexAsync(CancellationToken ct)
        {
            if (_cache.TryGetValue("pokemon_index_slim", out IReadOnlyList<(int, string, string)>? cached) && cached is not null) return cached;

            var client = _http.CreateClient("pokeapi");
            var list = new List<(int id, string name, string norm)>();
            string? next = "pokemon?limit=500&offset=0";

            while (!string.IsNullOrEmpty(next))
            {
                using var stream = await client.GetStreamAsync(next, ct);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                var root = doc.RootElement;

                if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in results.EnumerateArray())
                    {
                        var name = item.GetProperty("name").GetString() ?? "";
                        var url = item.GetProperty("url").GetString() ?? "";
                        var id = PokeApiHelper.ExtractId(url);
                        list.Add((id, name, PokeApiHelper.Normalize(name)));
                    }
                }

                next = root.TryGetProperty("next", out var nextEl) && nextEl.ValueKind == JsonValueKind.String ? nextEl.GetString() : null;
            }

            var ordered = list.OrderBy(t => t.id).ToList();
            _cache.Set("pokemon_index_slim", ordered, TimeSpan.FromHours(1));
            return ordered;
        }

        private string GetNameFromIndexSync(int id)
        {
            if (_cache.TryGetValue("pokemon_index_slim", out IReadOnlyList<(int id, string name, string norm)>? idx) && idx is not null)
            {
                var found = idx.FirstOrDefault(t => t.id == id);
                if (found.id == id) return found.name;
            }
            return $"pokemon-{id}";
        }
    }
}
