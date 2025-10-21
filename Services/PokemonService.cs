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
        #region DI / STATE
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;
        private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

        public PokemonService(IHttpClientFactory httpClientFactory, IMemoryCache cache)
        {
            _httpClientFactory = httpClientFactory;
            _cache = cache;
        }
        #endregion

        #region PUBLIC API
        public async Task<PagedResult<PokemonListItemDto>> SearchAsync(PokemonFilterDto filter, CancellationToken ct = default)
        {
            var page = Math.Max(1, filter.Page);
            var size = Math.Clamp(filter.PageSize, 1, 100);
            var offset = (page - 1) * size;

            var hasName = !string.IsNullOrWhiteSpace(filter.Name);
            var hasSpecies = filter.SpeciesId.HasValue;

            // 🚀 1) Caso rápido: SOLO especie → usa /pokemon-species/{id}.varieties (sin cargar todo el índice)
            if (hasSpecies && !hasName)
            {
                var ids = await GetPokemonIdsBySpeciesAsync(filter.SpeciesId!.Value, ct);
                var items = await FetchPageItemsByIdsAsync(ids, page, size, ct);

                return new PagedResult<PokemonListItemDto>
                {
                    Items = items,
                    Page = page,
                    PageSize = size,
                    TotalCount = ids.Count
                };
            }

            // 2) Nombre (con o sin especie): mantenemos la búsqueda por índice de nombres
            if (hasName || hasSpecies)
            {
                var all = await GetAllPokemonIndexAsync(ct); // (id, name) de todo el catálogo (cacheado 1h)
                IEnumerable<(int id, string name)> working = all;

                if (hasName)
                    working = await ApplyNameFilterAsync(all, filter.Name!, ct);

                if (hasSpecies)
                    working = await ApplySpeciesFilterAsync(working, filter.SpeciesId!.Value, ct);

                var total = working.Count();
                var pageSlice = working.Skip(offset).Take(size).ToList();
                var items = await FetchPageItemsFromPairsAsync(pageSlice, ct);

                return new PagedResult<PokemonListItemDto>
                {
                    Items = items,
                    Page = page,
                    PageSize = size,
                    TotalCount = total
                };
            }

            // 3) Sin filtros → usa /pokemon?offset&limit
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

            // Deduplicar por "genus" mostrado; nos quedamos con el id más bajo
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
        #endregion

        #region SEARCH HELPERS
        // Nombre: "empieza con" (fallback a "contiene"), normalizando
        private async Task<IEnumerable<(int id, string name)>> ApplyNameFilterAsync(
            IReadOnlyList<(int id, string name)> all,
            string nameText,
            CancellationToken ct)
        {
            var needle = NormalizeKey(nameText);

            var projected = all.Select(t => new
            {
                t.id,
                t.name,
                norm = NormalizeKey(t.name)
            });

            var starts = projected.Where(x => x.norm.StartsWith(needle, StringComparison.Ordinal));
            var query = starts.Any()
                ? starts
                : projected.Where(x => x.norm.Contains(needle, StringComparison.Ordinal));

            return query
                .OrderBy(x => x.name, StringComparer.OrdinalIgnoreCase)
                .Select(x => (x.id, x.name))
                .ToList();
        }

        // Especie: intersecta conjuntos de "genera" (ES/EN) normalizados (se usa cuando también hay nombre)
        private async Task<IEnumerable<(int id, string name)>> ApplySpeciesFilterAsync(
            IEnumerable<(int id, string name)> working,
            int speciesId,
            CancellationToken ct)
        {
            var targetGenera = await GetSpeciesGeneraAsync(speciesId, ct);
            var result = new List<(int id, string name)>();

            foreach (var item in working)
            {
                ct.ThrowIfCancellationRequested();
                var itemGenera = await GetSpeciesGeneraAsync(item.id, ct);
                if (itemGenera.Overlaps(targetGenera))
                    result.Add(item);
            }

            return result;
        }
        #endregion

        #region BUILD PAGE ITEMS
        // Desde una lista (id,name)
        private async Task<List<PokemonListItemDto>> FetchPageItemsFromPairsAsync(
            List<(int id, string name)> pageSlice,
            CancellationToken ct)
        {
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
            return items;
        }

        // Desde lista de IDs (optimizado para especie)
        private async Task<List<PokemonListItemDto>> FetchPageItemsByIdsAsync(
            IReadOnlyList<int> ids, int page, int size, CancellationToken ct)
        {
            var offset = (page - 1) * size;
            var slice = ids.Skip(offset).Take(size).ToList();

            var items = new List<PokemonListItemDto>(slice.Count);
            foreach (var id in slice)
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
            return items;
        }
        #endregion

        #region SPECIES SHORTCUTS
        // Obtiene todos los Pokémon (IDs) que pertenecen a una especie via /pokemon-species/{id}.varieties
        private async Task<IReadOnlyList<int>> GetPokemonIdsBySpeciesAsync(int speciesId, CancellationToken ct)
        {
            var cacheKey = $"species_varieties_ids_{speciesId}";
            if (_cache.TryGetValue(cacheKey, out IReadOnlyList<int>? cached) && cached is not null)
                return cached;

            var client = _httpClientFactory.CreateClient("pokeapi");
            PokemonSpeciesResponse? species = null;
            try
            {
                species = await client.GetFromJsonAsync<PokemonSpeciesResponse>($"pokemon-species/{speciesId}", _json, ct);
            }
            catch { /* ignore */ }

            var list = new List<int>();
            if (species?.Varieties is { Count: > 0 })
            {
                foreach (var v in species.Varieties)
                {
                    var url = v.Pokemon?.Url ?? "";
                    if (!string.IsNullOrWhiteSpace(url))
                        list.Add(ExtractId(url));
                }
            }

            list.Sort();
            _cache.Set(cacheKey, list, TimeSpan.FromHours(1));
            return list;
        }
        #endregion

        #region HTTP / CACHE HELPERS
        private async Task<PokemonDetailResponse?> GetPokemonDetailAsync(object idOrName, CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient("pokeapi");
            return await client.GetFromJsonAsync<PokemonDetailResponse>($"pokemon/{idOrName}", _json, ct);
        }

        // Índice global (id, name) para búsquedas por nombre (cache 1h)
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

            var ordered = list.OrderBy(t => t.id).ToList();
            _cache.Set("pokemon_index_all", ordered, TimeSpan.FromHours(1));
            return ordered;
        }

        // Devuelve el "genus" preferido (ES -> EN -> slug) con fallback pokemon→species
        private async Task<string> GetSpeciesDisplayAsync(int id, CancellationToken ct)
        {
            // 1) Intenta directo a /pokemon-species/{id}
            var name = await TryGetSpeciesDisplayInternalAsync(id, ct);
            if (!string.IsNullOrWhiteSpace(name))
                return name!;

            // 2) Fallback: quizá era /pokemon/{id}. Obtén speciesId real y reintenta
            var client = _httpClientFactory.CreateClient("pokeapi");
            PokemonDetailResponse? poke = null;
            try
            {
                poke = await client.GetFromJsonAsync<PokemonDetailResponse>($"pokemon/{id}", _json, ct);
            }
            catch { /* ignore */ }

            if (poke?.Species?.Url is not string sUrl || string.IsNullOrWhiteSpace(sUrl))
                return string.Empty;

            var speciesId = ExtractId(sUrl);
            name = await TryGetSpeciesDisplayInternalAsync(speciesId, ct);
            return name ?? string.Empty;
        }

        private async Task<string?> TryGetSpeciesDisplayInternalAsync(int speciesId, CancellationToken ct)
        {
            var cacheKey = $"species_genus_{speciesId}";
            if (_cache.TryGetValue(cacheKey, out string? cached) && !string.IsNullOrWhiteSpace(cached))
                return cached;

            var client = _httpClientFactory.CreateClient("pokeapi");
            PokemonSpeciesResponse? species = null;
            try
            {
                species = await client.GetFromJsonAsync<PokemonSpeciesResponse>($"pokemon-species/{speciesId}", _json, ct);
            }
            catch { species = null; }

            if (species is null) return null;

            string result = species.Name ?? "";
            if (species.Genera is { Count: > 0 })
            {
                var es = species.Genera.FirstOrDefault(g => g.Language?.Name == "es")?.Genus;
                var en = species.Genera.FirstOrDefault(g => g.Language?.Name == "en")?.Genus;

                if (!string.IsNullOrWhiteSpace(es)) result = es!;
                else if (!string.IsNullOrWhiteSpace(en)) result = en!;
            }

            _cache.Set(cacheKey, result, TimeSpan.FromHours(1));
            return result;
        }

        // Conjunto de géneros normalizados (ES/EN) para comparar por especie
        private async Task<HashSet<string>> GetSpeciesGeneraAsync(int id, CancellationToken ct)
        {
            var cacheKey = $"species_genera_set_{id}";
            if (_cache.TryGetValue(cacheKey, out HashSet<string>? cached) && cached is not null)
                return cached;

            var set = await TryGetSpeciesGeneraInternalAsync(id, ct);
            if (set.Count == 0)
            {
                // Fallback: quizá te dieron un id de /pokemon/{id}, resuelve species real y reintenta
                var client = _httpClientFactory.CreateClient("pokeapi");
                PokemonDetailResponse? poke = null;
                try { poke = await client.GetFromJsonAsync<PokemonDetailResponse>($"pokemon/{id}", _json, ct); }
                catch { /* ignore */ }

                if (poke?.Species?.Url is string sUrl && !string.IsNullOrWhiteSpace(sUrl))
                {
                    var speciesId = ExtractId(sUrl);
                    set = await TryGetSpeciesGeneraInternalAsync(speciesId, ct);
                }
            }

            _cache.Set(cacheKey, set, TimeSpan.FromHours(1));
            return set;
        }

        private async Task<HashSet<string>> TryGetSpeciesGeneraInternalAsync(int speciesId, CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient("pokeapi");
            PokemonSpeciesResponse? species = null;
            try
            {
                species = await client.GetFromJsonAsync<PokemonSpeciesResponse>($"pokemon-species/{speciesId}", _json, ct);
            }
            catch { species = null; }

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (species?.Genera is { Count: > 0 })
            {
                var es = species.Genera.FirstOrDefault(g => g.Language?.Name == "es")?.Genus;
                var en = species.Genera.FirstOrDefault(g => g.Language?.Name == "en")?.Genus;

                if (!string.IsNullOrWhiteSpace(es)) set.Add(NormalizeKey(es));
                if (!string.IsNullOrWhiteSpace(en)) set.Add(NormalizeKey(en));
            }

            // Fallback: usa el slug si no hay genera
            if (set.Count == 0 && !string.IsNullOrWhiteSpace(species?.Name))
                set.Add(NormalizeKey(species!.Name));

            return set;
        }
        #endregion

        #region ALIASES
        private Task<string> GetSpeciesNameAsync(int pokemonId, CancellationToken ct)
            => GetSpeciesDisplayAsync(pokemonId, ct);

        private Task<string> GetSpeciesNameByIdAsync(int speciesId, CancellationToken ct)
            => GetSpeciesDisplayAsync(speciesId, ct);
        #endregion

        #region UTILITIES
        // Normaliza texto para comparación (minúsculas, sin tildes, sin separadores)
        private static string NormalizeKey(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;

            s = s.Trim().ToLowerInvariant();

            // quitar diacríticos
            var d = s.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder(d.Length);
            foreach (var ch in d)
            {
                var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
                if (cat != System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }
            var norm = sb.ToString().Normalize(System.Text.NormalizationForm.FormC);

            // quitar separadores comunes
            norm = norm.Replace("-", "").Replace(" ", "").Replace("_", "");
            return norm;
        }

        private static int ExtractId(string url)
        {
            var parts = url.TrimEnd('/').Split('/');
            return int.Parse(parts[^1]);
        }

        private static string DefaultSprite(int id)
            => $"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/{id}.png";
        #endregion
    }
}
