using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Graphics;
using Pokedex1.Models;

namespace Pokedex1.Services;

public static class PokemonService
{
    public static List<Pokemon> MyTeam { get; set; } = new List<Pokemon>();

    private static readonly HttpClient _http = new();
    private static readonly SemaphoreSlim _semaphore = new(8); // concurrency for detail/ability calls
    private static readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(10);

    // Simple caches
    private static List<Pokemon> _cachedSummaries = new();
    private static DateTime _cachedAt = DateTime.MinValue;
    private static readonly Dictionary<int, Pokemon> _detailsCache = new();

    public static async Task<List<Pokemon>> GetPokemonPageAsync(int offset = 0, int limit = 30, CancellationToken ct = default, bool forceRefresh = false)
    {
        // If requesting the first page and cache is fresh, return from cache
        if (offset == 0 && !forceRefresh && _cachedSummaries is not null && (DateTime.UtcNow - _cachedAt) < _cacheDuration && _cachedSummaries.Count >= limit)
            return _cachedSummaries.Take(limit).ToList();

        ct.ThrowIfCancellationRequested();

        var listUrl = $"https://pokeapi.co/api/v2/pokemon?offset={offset}&limit={limit}";
        using var listResp = await _http.GetAsync(listUrl, ct).ConfigureAwait(false);
        listResp.EnsureSuccessStatusCode();

        using var listStream = await listResp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var listDoc = await JsonDocument.ParseAsync(listStream).ConfigureAwait(false);

        var results = listDoc.RootElement.GetProperty("results").EnumerateArray().ToArray();

        // Fetch details (id, sprites, types, basic stats, abilities) with limited concurrency
        var detailTasks = results.Select(async r =>
        {
            ct.ThrowIfCancellationRequested();

            var url = r.GetProperty("url").GetString()!;
            var name = r.GetProperty("name").GetString()!;

            // If details for this Pokemon id are cached, reuse them (we need id to check cache - but list doesn't include id)
            // So fetch the detail endpoint (one request per pokemon) with concurrency limit.
            await _semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();
                using var ds = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                using var pd = await JsonDocument.ParseAsync(ds).ConfigureAwait(false);

                var root = pd.RootElement;
                var id = root.GetProperty("id").GetInt32();

                // image: prefer official-artwork
                string imageUrl = string.Empty;
                if (root.TryGetProperty("sprites", out var sprites))
                {
                    if (sprites.TryGetProperty("other", out var other) &&
                        other.TryGetProperty("official-artwork", out var artwork) &&
                        artwork.TryGetProperty("front_default", out var fd) &&
                        fd.ValueKind == JsonValueKind.String)
                    {
                        imageUrl = fd.GetString()!;
                    }
                    else if (sprites.TryGetProperty("front_default", out var fd2) && fd2.ValueKind == JsonValueKind.String)
                    {
                        imageUrl = fd2.GetString()!;
                    }
                }

                var pokemonTypes = ParseTypes(root);
                string type = pokemonTypes.FirstOrDefault() ?? string.Empty;
                string cryUrl = ParseCryUrl(root);

                // stats: gather raw and normalized
                int rawHp = 0, rawAtk = 0, rawDef = 0, rawSpAtk = 0, rawSpDef = 0, rawSpeed = 0;
                if (root.TryGetProperty("stats", out var stats))
                {
                    foreach (var s in stats.EnumerateArray())
                    {
                        var statName = s.GetProperty("stat").GetProperty("name").GetString()!;
                        var baseStat = s.GetProperty("base_stat").GetInt32();
                        switch (statName)
                        {
                            case "hp": rawHp = baseStat; break;
                            case "attack": rawAtk = baseStat; break;
                            case "defense": rawDef = baseStat; break;
                            case "special-attack": rawSpAtk = baseStat; break;
                            case "special-defense": rawSpDef = baseStat; break;
                            case "speed": rawSpeed = baseStat; break;
                        }
                    }
                }

                // abilities names
                var abilities = new List<string>();
                if (root.TryGetProperty("abilities", out var abilitiesElem))
                {
                    foreach (var a in abilitiesElem.EnumerateArray())
                    {
                        if (a.TryGetProperty("ability", out var abilityObj) && abilityObj.TryGetProperty("name", out var nameElem))
                        {
                            abilities.Add(Capitalize(nameElem.GetString()!));
                        }
                    }
                }

                var pokemon = new Pokemon
                {
                    Id = id,
                    Name = Capitalize(name),
                    Type = string.IsNullOrEmpty(type) ? "" : Capitalize(type),
                    Types = pokemonTypes.Select(Capitalize).ToList(),
                    ImageUrl = imageUrl,
                    CryUrl = cryUrl,
                    Description = string.Empty,
                    TypeColor = GetColorForType(type),
                    RawHP = rawHp,
                    RawAttack = rawAtk,
                    RawDefense = rawDef,
                    RawSpecialAttack = rawSpAtk,
                    RawSpecialDefense = rawSpDef,
                    RawSpeed = rawSpeed,
                    HP = NormalizeStat(rawHp),
                    Attack = NormalizeStat(rawAtk),
                    Defense = NormalizeStat(rawDef),
                    SpecialAttack = NormalizeStat(rawSpAtk),
                    SpecialDefense = NormalizeStat(rawSpDef),
                    Speed = NormalizeStat(rawSpeed),
                    Abilities = abilities
                };

                // base_experience if present
                if (root.TryGetProperty("base_experience", out var be) && be.ValueKind == JsonValueKind.Number)
                {
                    pokemon.BaseExperience = be.GetInt32();
                }

                // store lightweight detail in details cache so detail page can reuse without re-fetching the same detail response
                _detailsCache[id] = pokemon;

                return pokemon;
            }
            catch
            {
                return null;
            }
            finally
            {
                _semaphore.Release();
            }
        }).ToArray();

        var details = await Task.WhenAll(detailTasks).ConfigureAwait(false);
        var pageList = details.Where(p => p is not null).OrderBy(p => p!.Id).Select(p => p!).ToList();

        // Update first-page cache
        if (offset == 0)
        {
            _cachedSummaries = pageList;
            _cachedAt = DateTime.UtcNow;
        }

        return pageList;
    }

    // Fetch species/flavor text and evolution chain for the given pokemon id (on-demand for details view).
    public static async Task<Pokemon?> GetPokemonDetailsAsync(int id, CancellationToken ct = default, bool forceRefresh = false)
    {
        if (!forceRefresh && _detailsCache.TryGetValue(id, out var cached) && !string.IsNullOrEmpty(cached.Description) && cached.AbilityDetails.Count > 0 && !string.IsNullOrEmpty(cached.GrowthRate))
            return cached;

        ct.ThrowIfCancellationRequested();

        // Ensure we have base detail (from /pokemon/{id})
        Pokemon basePokemon;
        if (!_detailsCache.TryGetValue(id, out basePokemon))
        {
            var detailUrl = $"https://pokeapi.co/api/v2/pokemon/{id}";
            using var resp = await _http.GetAsync(detailUrl, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            using var ds = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var pd = await JsonDocument.ParseAsync(ds).ConfigureAwait(false);
            var root = pd.RootElement;

            // parse raw + normalized stats (same logic as in page)
            int rawHp = 0, rawAtk = 0, rawDef = 0, rawSpAtk = 0, rawSpDef = 0, rawSpeed = 0;
            if (root.TryGetProperty("stats", out var stats))
            {
                foreach (var s in stats.EnumerateArray())
                {
                    var statName = s.GetProperty("stat").GetProperty("name").GetString()!;
                    var baseStat = s.GetProperty("base_stat").GetInt32();
                    switch (statName)
                    {
                        case "hp": rawHp = baseStat; break;
                        case "attack": rawAtk = baseStat; break;
                        case "defense": rawDef = baseStat; break;
                        case "special-attack": rawSpAtk = baseStat; break;
                        case "special-defense": rawSpDef = baseStat; break;
                        case "speed": rawSpeed = baseStat; break;
                    }
                }
            }

            string imageUrl = string.Empty;
            if (root.TryGetProperty("sprites", out var sprites))
            {
                if (sprites.TryGetProperty("other", out var other) &&
                    other.TryGetProperty("official-artwork", out var artwork) &&
                    artwork.TryGetProperty("front_default", out var fd) &&
                    fd.ValueKind == JsonValueKind.String)
                {
                    imageUrl = fd.GetString()!;
                }
                else if (sprites.TryGetProperty("front_default", out var fd2) && fd2.ValueKind == JsonValueKind.String)
                {
                    imageUrl = fd2.GetString()!;
                }
            }

            var pokemonTypes = ParseTypes(root);
            string type = pokemonTypes.FirstOrDefault() ?? string.Empty;
            string cryUrl = ParseCryUrl(root);

            var abilities = new List<string>();
            if (root.TryGetProperty("abilities", out var abilitiesElem))
            {
                foreach (var a in abilitiesElem.EnumerateArray())
                {
                    if (a.TryGetProperty("ability", out var abilityObj) && abilityObj.TryGetProperty("name", out var nameElem))
                    {
                        abilities.Add(Capitalize(nameElem.GetString()!));
                    }
                }
            }

            basePokemon = new Pokemon
            {
                Id = id,
                Name = Capitalize(root.GetProperty("name").GetString()!),
                Type = string.IsNullOrEmpty(type) ? "" : Capitalize(type),
                Types = pokemonTypes.Select(Capitalize).ToList(),
                ImageUrl = imageUrl,
                CryUrl = cryUrl,
                TypeColor = GetColorForType(type),
                RawHP = rawHp,
                RawAttack = rawAtk,
                RawDefense = rawDef,
                RawSpecialAttack = rawSpAtk,
                RawSpecialDefense = rawSpDef,
                RawSpeed = rawSpeed,
                HP = NormalizeStat(rawHp),
                Attack = NormalizeStat(rawAtk),
                Defense = NormalizeStat(rawDef),
                SpecialAttack = NormalizeStat(rawSpAtk),
                SpecialDefense = NormalizeStat(rawSpDef),
                Speed = NormalizeStat(rawSpeed),
                Abilities = abilities
            };

            // base_experience if present
            if (root.TryGetProperty("base_experience", out var be) && be.ValueKind == JsonValueKind.Number)
            {
                basePokemon.BaseExperience = be.GetInt32();
            }

            _detailsCache[id] = basePokemon;
        }

        // Species: flavor text and evolution chain
        string speciesUrl = $"https://pokeapi.co/api/v2/pokemon-species/{id}/";
        try
        {
            using var speciesResp = await _http.GetAsync(speciesUrl, ct).ConfigureAwait(false);
            speciesResp.EnsureSuccessStatusCode();
            using var ss = await speciesResp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var sd = await JsonDocument.ParseAsync(ss).ConfigureAwait(false);

            var sroot = sd.RootElement;

            // flavor text (first English)
            string flavor = string.Empty;
            if (sroot.TryGetProperty("flavor_text_entries", out var flavorEntries))
            {
                foreach (var e in flavorEntries.EnumerateArray())
                {
                    if (e.GetProperty("language").GetProperty("name").GetString() == "en")
                    {
                        flavor = e.GetProperty("flavor_text").GetString() ?? "";
                        flavor = flavor.Replace("\n", " ").Replace("\f", " ").Trim();
                        break;
                    }
                }
            }

            // growth rate
            string growthName = string.Empty;
            if (sroot.TryGetProperty("growth_rate", out var growthElem) && growthElem.TryGetProperty("url", out var growthUrlElem))
            {
                var growthUrl = growthUrlElem.GetString();
                if (!string.IsNullOrEmpty(growthUrl))
                {
                    try
                    {
                        using var gresp = await _http.GetAsync(growthUrl, ct).ConfigureAwait(false);
                        gresp.EnsureSuccessStatusCode();
                        using var gs = await gresp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                        using var gd = await JsonDocument.ParseAsync(gs).ConfigureAwait(false);
                        var groot = gd.RootElement;
                        if (groot.TryGetProperty("name", out var gname))
                            growthName = Capitalize(gname.GetString()!);
                    }
                    catch
                    {
                        // ignore growth fetch issues
                    }
                }
            }

            // evolution chain url
            string evoUrl = null;
            if (sroot.TryGetProperty("evolution_chain", out var evoElem) && evoElem.TryGetProperty("url", out var evoUrlElem))
            {
                evoUrl = evoUrlElem.GetString();
            }

            basePokemon.Description = string.IsNullOrEmpty(flavor) ? basePokemon.Description : flavor;
            basePokemon.GrowthRate = growthName;
            basePokemon.LevelUpGuide = $"Gain experience from battles and items. Base experience yield: {basePokemon.BaseExperience}. Growth rate: {basePokemon.GrowthRate}.";

            if (!string.IsNullOrEmpty(evoUrl))
            {
                try
                {
                    using var evoResp = await _http.GetAsync(evoUrl, ct).ConfigureAwait(false);
                    evoResp.EnsureSuccessStatusCode();
                    using var es = await evoResp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                    using var ed = await JsonDocument.ParseAsync(es).ConfigureAwait(false);

                    var evoRoot = ed.RootElement.GetProperty("chain");
                    var evoList = new List<Evolution>();
                    FlattenEvolutionChain(evoRoot, evoList);
                    basePokemon.EvolutionChain = evoList;
                }
                catch
                {
                    // ignore
                }
            }
        }
        catch
        {
            // ignore species fetch error
        }

        // Fetch ability descriptions (concurrent but limited)
        var abilityDetails = new List<AbilityDetail>();
        var abilityNames = basePokemon.Abilities ?? new List<string>();
        var abilityTasks = abilityNames.Select(async an =>
        {
            // ability name might have spaces/capitalization; PokeAPI expects lower-case hyphenated names
            var apiName = an.ToLowerInvariant().Replace(' ', '-');
            var abilityUrl = $"https://pokeapi.co/api/v2/ability/{apiName}/";
            try
            {
                await _semaphore.WaitAsync(ct).ConfigureAwait(false);
                using var aresp = await _http.GetAsync(abilityUrl, ct).ConfigureAwait(false);
                aresp.EnsureSuccessStatusCode();
                using var asStream = await aresp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                using var adoc = await JsonDocument.ParseAsync(asStream).ConfigureAwait(false);
                var aroot = adoc.RootElement;

                string desc = string.Empty;
                if (aroot.TryGetProperty("effect_entries", out var effects))
                {
                    foreach (var e in effects.EnumerateArray())
                    {
                        if (e.GetProperty("language").GetProperty("name").GetString() == "en")
                        {
                            if (e.TryGetProperty("short_effect", out var se) && se.ValueKind == JsonValueKind.String)
                                desc = se.GetString()!;
                            else if (e.TryGetProperty("effect", out var ef) && ef.ValueKind == JsonValueKind.String)
                                desc = ef.GetString()!;
                            break;
                        }
                    }
                }

                return new AbilityDetail { Name = an, Description = desc };
            }
            catch
            {
                return new AbilityDetail { Name = an, Description = string.Empty };
            }
            finally
            {
                _semaphore.Release();
            }
        }).ToArray();

        var adetails = await Task.WhenAll(abilityTasks).ConfigureAwait(false);
        abilityDetails.AddRange(adetails);

        basePokemon.AbilityDetails = abilityDetails.ToList();

        _detailsCache[id] = basePokemon;
        return basePokemon;
    }

    // Recursively traverse the evolution chain and collect species id/name + image url
    private static void FlattenEvolutionChain(JsonElement node, List<Evolution> list)
    {
        if (node.ValueKind != JsonValueKind.Object)
            return;

        if (node.TryGetProperty("species", out var species) && species.TryGetProperty("name", out var nameElem) && species.TryGetProperty("url", out var urlElem))
        {
            var speciesName = nameElem.GetString()!;
            var speciesUrl = urlElem.GetString()!; // e.g. https://pokeapi.co/api/v2/pokemon-species/1/
            var id = ExtractIdFromUrl(speciesUrl);
            var imageUrl = id > 0 ? GetOfficialArtworkUrl(id) : string.Empty;

            list.Add(new Evolution
            {
                Id = id,
                Name = Capitalize(speciesName),
                ImageUrl = imageUrl
            });
        }

        if (node.TryGetProperty("evolves_to", out var evolvesTo) && evolvesTo.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in evolvesTo.EnumerateArray())
            {
                FlattenEvolutionChain(child, list);
            }
        }
    }

    private static int ExtractIdFromUrl(string url)
    {
        // species url ends with "/{id}/"
        if (string.IsNullOrEmpty(url))
            return 0;

        var parts = url.TrimEnd('/').Split('/');
        if (int.TryParse(parts.Last(), out var id))
            return id;
        return 0;
    }

    private static string GetOfficialArtworkUrl(int id)
        => $"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/other/official-artwork/{id}.png";

    private static double NormalizeStat(int raw) => Math.Clamp(raw / 255.0, 0.0, 1.0);

    private static List<string> ParseTypes(JsonElement root)
    {
        var types = new List<(int Slot, string Name)>();
        if (root.TryGetProperty("types", out var typeElements))
        {
            foreach (var t in typeElements.EnumerateArray())
            {
                var slot = t.TryGetProperty("slot", out var slotElement) ? slotElement.GetInt32() : 0;
                var name = t.GetProperty("type").GetProperty("name").GetString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(name))
                    types.Add((slot, name));
            }
        }

        return types.OrderBy(t => t.Slot).Select(t => t.Name).ToList();
    }

    private static string ParseCryUrl(JsonElement root)
    {
        if (root.TryGetProperty("cries", out var cries))
        {
            if (cries.TryGetProperty("latest", out var latest) && latest.ValueKind == JsonValueKind.String)
                return latest.GetString() ?? string.Empty;

            if (cries.TryGetProperty("legacy", out var legacy) && legacy.ValueKind == JsonValueKind.String)
                return legacy.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    private static Color GetColorForType(string type)
    {
        return (type ?? "").ToLowerInvariant() switch
        {
            "grass" => Colors.Green,
            "fire" => Colors.Orange,
            "water" => Colors.Blue,
            "electric" => Colors.Yellow,
            "psychic" => Colors.Purple,
            "rock" => Colors.Brown,
            "ground" => Colors.Brown,
            "flying" => Colors.SkyBlue,
            "ice" => Colors.LightBlue,
            "dragon" => Colors.Indigo,
            "poison" => Colors.Violet,
            _ => Colors.Gray
        };
    }

    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
}
