using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using RhodesAI.Backend.Models.Base;

namespace RhodesAI.Backend.Models.Domain;

/// <summary>
/// Operador de Rhodes Island. Encapsula sus invariantes (nombre, rareza 1 a 6, límites de promoción)
/// y expone CalculateCombatPower() como punto de extensión polimórfico.
/// </summary>
public partial class Operator : BaseEntity
{
    private readonly ReadOnlyDictionary<PromotionLevel, OperatorStats> _statsByPromotion;

    public string Name { get; private set; }
    public int Rarity { get; private set; }
    public OperatorClass Class { get; private set; }
    public string Archetype { get; private set; }
    public string Affiliation { get; private set; }
    public string Description { get; private set; }
    public string Biography { get; private set; }
    public string ArtUrl { get; private set; }
    public PromotionLevel Promotion { get; private set; }
    public IReadOnlyDictionary<PromotionLevel, OperatorStats> StatsByPromotion => _statsByPromotion;

    public Operator(string name, int rarity, OperatorClass operatorClass,
        IReadOnlyDictionary<PromotionLevel, OperatorStats> statsByPromotion,
        PromotionLevel promotion = PromotionLevel.Base,
        string affiliation = "", string description = "", string biography = "",
        string artUrl = "", string archetype = "")
        : base(ToSlug(name))
    {
        if (statsByPromotion is null || statsByPromotion.Count == 0)
            throw new ArgumentException("El operador necesita al menos un bloque de estadísticas.", nameof(statsByPromotion));

        Name = name?.Trim() ?? string.Empty;
        Rarity = rarity;
        Class = operatorClass;
        Promotion = promotion;
        Affiliation = affiliation.Trim();
        Description = description.Trim();
        Biography = biography.Trim();
        ArtUrl = artUrl.Trim();
        Archetype = archetype.Trim();
        _statsByPromotion = new ReadOnlyDictionary<PromotionLevel, OperatorStats>(
            new Dictionary<PromotionLevel, OperatorStats>(statsByPromotion));

        EnsureValid();
    }

    public Operator(string name, int rarity, OperatorClass operatorClass, OperatorStats stats,
        PromotionLevel promotion = PromotionLevel.Base)
        : this(name, rarity, operatorClass, new Dictionary<PromotionLevel, OperatorStats> { [promotion] = stats }, promotion)
    {
    }

    /// <summary>Estadísticas del nivel de promoción actual.</summary>
    public OperatorStats Stats => GetStatsFor(Promotion);

    /// <summary>Promoción máxima permitida por la rareza (1-2★ sin promoción, 3★ hasta E1, 4-6★ hasta E2).</summary>
    public PromotionLevel MaxPromotion => Rarity switch
    {
        <= 2 => PromotionLevel.Base,
        3 => PromotionLevel.Elite1,
        _ => PromotionLevel.Elite2
    };

    /// <summary>
    /// Devuelve las estadísticas del nivel pedido o, si la API no las trae, las del nivel más alto disponible por debajo.
    /// </summary>
    public OperatorStats GetStatsFor(PromotionLevel level)
    {
        for (var current = level; current >= PromotionLevel.Base; current--)
        {
            if (_statsByPromotion.TryGetValue(current, out var stats))
                return stats;
        }
        return _statsByPromotion.OrderBy(pair => pair.Key).First().Value;
    }

    public void Promote(PromotionLevel target)
    {
        if (target < Promotion)
            throw new InvalidOperationException($"{Name} no puede bajar de {Promotion} a {target}.");
        if (target > MaxPromotion)
            throw new InvalidOperationException($"Un operador de {Rarity}★ sólo puede llegar a {MaxPromotion}.");

        Promotion = target;
        Touch();
    }

    /// <summary>Multiplicador por nivel de promoción; las subclases pueden ajustarlo.</summary>
    protected virtual double PromotionMultiplier => Promotion switch
    {
        PromotionLevel.Elite1 => 1.15,
        PromotionLevel.Elite2 => 1.35,
        _ => 1.0
    };

    /// <summary>Poder de combate genérico; las subclases lo sobrescriben.</summary>
    public virtual double CalculateCombatPower()
    {
        var s = Stats;
        double raw = s.Hp * 0.1 + s.Atk + s.Def * 0.5 + s.Res * 5;
        return Math.Round(raw * PromotionMultiplier, 2);
    }

    public override IReadOnlyList<string> Validate()
    {
        var errors = new List<string>(base.Validate());
        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("El nombre del operador es obligatorio.");
        if (Rarity is < 1 or > 6)
            errors.Add("La rareza debe estar entre 1 y 6 estrellas.");
        if (!Enum.IsDefined(Class))
            errors.Add("La clase táctica no es válida.");
        if (Promotion > MaxPromotion)
            errors.Add($"Un operador de {Rarity}★ sólo puede llegar a {MaxPromotion}.");
        return errors;
    }

    private static string ToSlug(string? name) =>
        NonAlphanumeric().Replace((name ?? string.Empty).Trim().ToLowerInvariant(), "-").Trim('-');

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();
}
