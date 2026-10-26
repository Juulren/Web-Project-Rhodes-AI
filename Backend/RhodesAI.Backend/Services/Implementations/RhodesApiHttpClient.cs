using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using RhodesAI.Backend.Models.Domain;
using RhodesAI.Backend.Services.Interfaces;

namespace RhodesAI.Backend.Services.Implementations;

/// <summary>
/// Única responsabilidad: consumir RhodesAPI y convertir su JSON en entidades del dominio.
/// Cachea el catálogo para no descargarlo en cada petición.
/// </summary>
public class RhodesApiHttpClient(HttpClient http, IMemoryCache cache, ILogger<RhodesApiHttpClient> logger) : IRhodesApiClient
{
    private const string CacheKey = "rhodes-operators";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public async Task<IReadOnlyList<Operator>> GetOperatorsAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlyList<Operator>? cached) && cached is not null)
            return cached;

        using var response = await http.GetAsync("operator", ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var operators = new List<Operator>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            try
            {
                if (Map(element) is { } op)
                    operators.Add(op);
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning("Operador descartado por datos inválidos: {Message}", ex.Message);
            }
        }

        cache.Set(CacheKey, (IReadOnlyList<Operator>)operators, CacheDuration);
        return operators;
    }

    private static Operator? Map(JsonElement json)
    {
        var classes = ReadStrings(json, "class");
        if (classes.Count == 0 || !Enum.TryParse<OperatorClass>(classes[0], true, out var operatorClass))
            return null;

        var statsByPromotion = ReadStats(json);
        if (statsByPromotion.Count == 0)
            return null;

        int rarity = (int)ReadNumber(json, "rarity");
        var maxPromotion = rarity switch { <= 2 => PromotionLevel.Base, 3 => PromotionLevel.Elite1, _ => PromotionLevel.Elite2 };

        return new CombatOperator(
            ReadString(json, "name"),
            rarity,
            operatorClass,
            statsByPromotion,
            maxPromotion,
            affiliation: ReadStrings(json, "affiliation").FirstOrDefault() ?? string.Empty,
            description: ReadString(json, "description"),
            biography: ReadString(json, "biography"),
            artUrl: ReadArt(json),
            archetype: classes.Count > 1 ? classes[^1] : string.Empty);
    }

    private static Dictionary<PromotionLevel, OperatorStats> ReadStats(JsonElement json)
    {
        var result = new Dictionary<PromotionLevel, OperatorStats>();
        if (!json.TryGetProperty("statistics", out var stats) || stats.ValueKind != JsonValueKind.Object)
            return result;

        // "base" trae coste, RES, intervalo y redespliegue; los bloques eXmax sólo HP/ATK/DEF/bloqueo.
        stats.TryGetProperty("base", out var baseBlock);
        var tiers = new (PromotionLevel Level, string Key)[]
        {
            (PromotionLevel.Base, "e0max"), (PromotionLevel.Elite1, "e1max"), (PromotionLevel.Elite2, "e2max")
        };

        foreach (var (level, key) in tiers)
        {
            if (!stats.TryGetProperty(key, out var tier) || tier.ValueKind != JsonValueKind.Object)
                continue;
            if (ReadNumber(tier, "hp") <= 0)
                continue;

            result[level] = new OperatorStats(
                hp: (int)ReadNumber(tier, "hp"),
                atk: (int)ReadNumber(tier, "atk"),
                def: (int)ReadNumber(tier, "def"),
                res: (int)ReadNumber(baseBlock, "resist"),
                cost: (int)ReadNumber(baseBlock, "cost"),
                block: (int)Math.Max(ReadNumber(tier, "block"), ReadNumber(baseBlock, "block")),
                attackInterval: ReadNumber(baseBlock, "interval") is > 0 and var interval ? interval : 1.0,
                redeployTime: (int)ReadNumber(baseBlock, "deploy"));
        }
        return result;
    }

    private static string ReadArt(JsonElement json)
    {
        if (!json.TryGetProperty("art", out var art) || art.ValueKind != JsonValueKind.Array)
            return string.Empty;

        var links = art.EnumerateArray()
            .Where(a => a.ValueKind == JsonValueKind.Object)
            .ToDictionary(a => ReadString(a, "name"), a => ReadString(a, "link"), StringComparer.OrdinalIgnoreCase);

        foreach (var preferred in new[] { "E2", "E1", "Base" })
        {
            if (links.TryGetValue(preferred, out var link) && link.Length > 0)
                return link;
        }
        return links.Values.FirstOrDefault(l => l.Length > 0) ?? string.Empty;
    }

    private static string ReadString(JsonElement json, string property) =>
        json.ValueKind == JsonValueKind.Object && json.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!.Trim()
            : string.Empty;

    /// <summary>Lee un campo que puede venir como texto o como arreglo de textos.</summary>
    private static List<string> ReadStrings(JsonElement json, string property)
    {
        if (!json.TryGetProperty(property, out var value))
            return [];
        return value.ValueKind switch
        {
            JsonValueKind.String => [value.GetString()!.Trim()],
            JsonValueKind.Array => value.EnumerateArray()
                .Where(v => v.ValueKind == JsonValueKind.String)
                .Select(v => v.GetString()!.Trim())
                .Where(v => v.Length > 0)
                .ToList(),
            _ => []
        };
    }

    /// <summary>RhodesAPI entrega números como texto con espacios (" 1"); se aceptan ambos formatos.</summary>
    private static double ReadNumber(JsonElement json, string property)
    {
        if (json.ValueKind != JsonValueKind.Object || !json.TryGetProperty(property, out var value))
            return 0;
        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.String when double.TryParse(value.GetString()?.Trim(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => 0
        };
    }
}
