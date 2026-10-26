using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using RhodesAI.Backend.Models.Domain;

namespace RhodesAI.Backend.Services.Implementations;

/// <summary>
/// Adaptador seguro del JSON irregular de RhodesAPI hacia el dominio. Nunca lanza excepciones:
/// los registros inutilizables devuelven null para que el cliente los descarte.
/// Irregularidades cubiertas: números como texto (" 1", "09", "block: 2", ""), afiliación como texto
/// o arreglo, tiers de estadísticas faltantes o vacíos, rareza 0 y retratos alojados en ak.gamepress.gg,
/// un dominio que ya no existe.
/// </summary>
public static partial class RhodesOperatorAdapter
{
    public const string PortraitBaseUrl = "https://raw.githubusercontent.com/yuanyan3060/ArknightsGameResource/main/portrait/";

    private static readonly (PromotionLevel Level, string Key)[] Tiers =
    [
        (PromotionLevel.Base, "e0max"), (PromotionLevel.Elite1, "e1max"), (PromotionLevel.Elite2, "e2max")
    ];

    public static Operator? Map(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Object)
            return null;

        var name = ReadString(json, "name");
        var classes = ReadStrings(json, "class");
        int rarity = (int)(ReadNumber(json, "rarity") ?? 0);
        if (name.Length == 0 || rarity is < 1 or > 6
            || classes.Count == 0 || !Enum.TryParse<OperatorClass>(classes[0], true, out var operatorClass))
            return null;

        var stats = ReadStats(json);
        if (stats.Count == 0)
            return null;

        var description = ReadString(json, "description");
        var biography = ReadString(json, "biography");
        try
        {
            return new CombatOperator(
                name,
                rarity,
                operatorClass,
                stats,
                rarity switch { <= 2 => PromotionLevel.Base, 3 => PromotionLevel.Elite1, _ => PromotionLevel.Elite2 },
                affiliation: ReadStrings(json, "affiliation").FirstOrDefault() ?? string.Empty,
                description: description.Length > 0 ? description : biography,
                biography: biography,
                artUrl: ReadPortrait(json, rarity),
                archetype: classes.Count > 1 ? classes[^1] : string.Empty);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static Dictionary<PromotionLevel, OperatorStats> ReadStats(JsonElement json)
    {
        var result = new Dictionary<PromotionLevel, OperatorStats>();
        if (!json.TryGetProperty("statistics", out var statistics) || statistics.ValueKind != JsonValueKind.Object)
            return result;

        // "base" aporta coste, RES, intervalo y redespliegue; los bloques eXmax sólo HP, ATK, DEF y bloqueo.
        statistics.TryGetProperty("base", out var baseBlock);
        foreach (var (level, key) in Tiers)
        {
            var tier = statistics.TryGetProperty(key, out var block) && block.ValueKind == JsonValueKind.Object ? block : default;
            // Sin e0max, el bloque "base" representa al operador sin promocionar.
            if (tier.ValueKind != JsonValueKind.Object && level == PromotionLevel.Base)
                tier = baseBlock;
            if ((ReadNumber(tier, "hp") ?? 0) <= 0)
                continue;

            result[level] = new OperatorStats(
                hp: ToInt(ReadNumber(tier, "hp")),
                atk: ToInt(ReadNumber(tier, "atk")),
                def: ToInt(ReadNumber(tier, "def")),
                res: ToInt(ReadNumber(baseBlock, "resist")),
                cost: ToInt(ReadNumber(baseBlock, "cost")),
                block: ToInt(ReadNumber(tier, "block") ?? ReadNumber(baseBlock, "block") ?? 1),
                attackInterval: ReadNumber(baseBlock, "interval") is > 0 and var interval ? interval : 1.0,
                redeployTime: ToInt(ReadNumber(baseBlock, "deploy") ?? 70));
        }
        return result;
    }

    /// <summary>Reconstruye la URL del retrato a partir del identificador interno (char_XXX_nombre) del enlace.</summary>
    private static string ReadPortrait(JsonElement json, int rarity)
    {
        if (!json.TryGetProperty("art", out var art) || art.ValueKind != JsonValueKind.Array)
            return string.Empty;

        var links = string.Join(' ', art.EnumerateArray().Select(item => ReadString(item, "link")));
        var match = CharacterId().Match(links);
        return match.Success
            ? $"{PortraitBaseUrl}{match.Value.ToLowerInvariant()}{(rarity >= 4 ? "_2" : "_1")}.png"
            : string.Empty;
    }

    private static int ToInt(double? value) => value is > 0 ? (int)Math.Round(value.Value) : 0;

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
            JsonValueKind.String => value.GetString()!.Trim() is { Length: > 0 } single ? [single] : [],
            JsonValueKind.Array => value.EnumerateArray()
                .Where(v => v.ValueKind == JsonValueKind.String)
                .Select(v => v.GetString()!.Trim())
                .Where(v => v.Length > 0)
                .ToList(),
            _ => []
        };
    }

    /// <summary>Extrae el primer número de valores como 12, " 1", "09" o "block: 2". Devuelve null si no hay ninguno.</summary>
    private static double? ReadNumber(JsonElement json, string property)
    {
        if (json.ValueKind != JsonValueKind.Object || !json.TryGetProperty(property, out var value))
            return null;
        if (value.ValueKind == JsonValueKind.Number)
            return value.GetDouble();
        if (value.ValueKind != JsonValueKind.String)
            return null;

        var match = FirstNumber().Match(value.GetString()!);
        return match.Success ? double.Parse(match.Value, CultureInfo.InvariantCulture) : null;
    }

    [GeneratedRegex(@"-?\d+(?:\.\d+)?")]
    private static partial Regex FirstNumber();

    [GeneratedRegex(@"char_\d+_[a-z0-9]+", RegexOptions.IgnoreCase)]
    private static partial Regex CharacterId();
}
