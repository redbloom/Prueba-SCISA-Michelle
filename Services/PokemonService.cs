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

            var hasName = !string.IsNullOrWhiteSpace(filter.Name);
            var hasSpecies = filter.SpeciesId.HasValue;

            if (hasName || hasSpecies)
            {
                var all = await GetAllPokemonIndexAsync(ct);
                IEnumerable<(int id, string name)> working = all;

                if (hasName)
                {
                    var needle = NormalizeKey(filter.Name);

                    var projected = all.Select(t => new
                    {
                        t.id,
                        name = t.name,
                        norm = NormalizeKey(t.name)
                    });

                    var starts = projected.Where(x => x.norm.StartsWith(needle, StringComparison.Ordinal));
                    var query = starts.Any()
                        ? starts
                        : projected.Where(x => x.norm.Contains(needle, StringComparison.Ordinal));

                    working = query
                        .OrderBy(x => x.name, StringComparer.OrdinalIgnoreCase)
                        .Select(x => (x.id, x.name));
                }

                if (hasSpecies)
                {
                    var targetGenus = await GetSpeciesNameByIdAsync(filter.SpeciesId!.Value, ct);

                    var tmp = new List<(int id, string name)>();
                    foreach (var item in working)
                    {
                        ct.ThrowIfCancellationRequested();
                        var genus = await GetSpeciesNameAsync(item.id, ct); // usa cache interno
                        if (string.Equals(genus?.Trim(), targetGenus?.Trim(), StringComparison.OrdinalIgnoreCase))
                            tmp.Add(item);
                    }
                    working = tmp;
                }

                var total = working.Count();
                var pageSlice = working.Skip(offset).Take(size).ToList();

                var items = new List<PokemonListItemDto>(pageSlice.Count);
                foreach (var (id, _) in pageSlice)
                {
                    ct.ThrowIfCancellationRequested();
                    PokemonDetailResponse? detail = null;
                    try { detail = await GetPokemonDetailAsync(id, ct); } catch { continue; }

                    var speciesName = await GetSpeciesNameAsync(detail!.Id, ct);

                    items.Add(new PokemonListItemDto
                    {
                        Id = detail.Id,
                        Name = detail.Name,
                        SpeciesName = speciesName,
                        ImageUrl = DefaultSprite(detail.Id)
                    });
                }

                return new PagedResult<PokemonListItemDto>
                {
                    Items = items,
                    Page = page,
                    PageSize = size,
                    TotalCount = total
                };
            }

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

        // ===== Catálogo de especies (deduplicado por genus) =====
        public async Task<IReadOnlyList<(int id, string name)>> GetSpeciesAsync(CancellationToken ct = default)
        {
            if (_cache.TryGetValue("species_all", out IReadOnlyList<(int, string)>? cached) && cached is not null)
                return cached;

            var client = _httpClientFactory.CreateClient("pokeapi");

            var ids = new List<int>();
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
                        var url = item.GetProperty("url").GetString() ?? "";
                        ids.Add(ExtractId(url));
                    }
                }

                next = root.TryGetProperty("next", out var nextEl) && nextEl.ValueKind == JsonValueKind.String
                    ? nextEl.GetString()
                    : null;
            }

            ids.Sort();

            const int batchSize = 25;
            var raw = new List<(int id, string name)>(ids.Count);

            for (int i = 0; i < ids.Count; i += batchSize)
            {
                ct.ThrowIfCancellationRequested();
                var batch = ids.Skip(i).Take(batchSize)
                    .Select(async id => (id, name: await GetSpeciesDisplayAsync(id, ct)));
                var mapped = await Task.WhenAll(batch);
                raw.AddRange(mapped);
            }

            var distinct = raw
                .GroupBy(x => x.name.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderBy(x => x.id).First())
                .OrderBy(x => x.name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _cache.Set("species_all", distinct, TimeSpan.FromHours(1));
            return distinct;
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

        private static string NormalizeKey(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;

            s = s.Trim().ToLowerInvariant();

            var d = s.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder(d.Length);
            foreach (var ch in d)
            {
                var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
                if (cat != System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }
            var norm = sb.ToString().Normalize(System.Text.NormalizationForm.FormC);

            norm = norm.Replace("-", "").Replace(" ", "").Replace("_", "");
            return norm;
        }

        private async Task<PokemonDetailResponse?> GetPokemonDetailAsync(object idOrName, CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient("pokeapi");
            return await client.GetFromJsonAsync<PokemonDetailResponse>($"pokemon/{idOrName}", _json, ct);
        }

        // Índice global (id, name) para LIKE por nombre — con caché de 1 hora
        private async Task<IReadOnlyList<(int id, string name)>> GetAllPokemonIndexAsync(CancellationToken ct)
        {
            if (_cache.TryGetValue("pokemon_index_all", out IReadOnlyList<(int, string)>? cached) && cached is not null)
                return cached;

            var client = _httpClientFactory.CreateClient("pokeapi");
            var list = new List<(int id, string name)>();

            string? next = "pokemon?limit=500&offset=0";
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
                        list.Add((id, name));
                    }
                }

                next = root.TryGetProperty("next", out var nextEl) && nextEl.ValueKind == JsonValueKind.String
                    ? nextEl.GetString()
                    : null;
            }

            // cachea por 1h
            var ordered = list.OrderBy(t => t.id).ToList();
            _cache.Set("pokemon_index_all", ordered, TimeSpan.FromHours(1));
            return ordered;
        }

        private async Task<string> GetSpeciesDisplayAsync(int id, CancellationToken ct)
        {
            var cacheKey = $"species_genus_{id}";
            if (_cache.TryGetValue(cacheKey, out string? cached) && !string.IsNullOrWhiteSpace(cached))
                return cached!;

            var client = _httpClientFactory.CreateClient("pokeapi");
            var species = await client.GetFromJsonAsync<PokemonSpeciesResponse>($"pokemon-species/{id}", _json, ct);

            string result = species?.Name ?? "";

            if (species?.Genera is { Count: > 0 })
            {
                var es = species.Genera.FirstOrDefault(g => g.Language?.Name == "es")?.Genus;
                var en = species.Genera.FirstOrDefault(g => g.Language?.Name == "en")?.Genus;

                if (!string.IsNullOrWhiteSpace(es)) result = es!;
                else if (!string.IsNullOrWhiteSpace(en)) result = en!;
            }

            _cache.Set(cacheKey, result, TimeSpan.FromHours(1));
            return result;
        }

        private Task<string> GetSpeciesNameAsync(int pokemonId, CancellationToken ct)
            => GetSpeciesDisplayAsync(pokemonId, ct);

        private Task<string> GetSpeciesNameByIdAsync(int speciesId, CancellationToken ct)
            => GetSpeciesDisplayAsync(speciesId, ct);

        private static int ExtractId(string url)
        {
            var parts = url.TrimEnd('/').Split('/');
            return int.Parse(parts[^1]);
        }

        private static string DefaultSprite(int id)
            => $"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/{id}.png";
    }
}
