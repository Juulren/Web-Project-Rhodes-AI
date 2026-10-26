namespace RhodesAI.Backend.Models.Domain;

/// <summary>
/// Especialización que pondera los atributos según la clase táctica del operador.
/// </summary>
public class CombatOperator : Operator
{
    public CombatOperator(string name, int rarity, OperatorClass operatorClass,
        IReadOnlyDictionary<PromotionLevel, OperatorStats> statsByPromotion,
        PromotionLevel promotion = PromotionLevel.Base,
        string affiliation = "", string description = "", string biography = "",
        string artUrl = "", string archetype = "")
        : base(name, rarity, operatorClass, statsByPromotion, promotion, affiliation, description, biography, artUrl, archetype)
    {
    }

    public CombatOperator(string name, int rarity, OperatorClass operatorClass, OperatorStats stats,
        PromotionLevel promotion = PromotionLevel.Base)
        : base(name, rarity, operatorClass, stats, promotion)
    {
    }

    /// <summary>
    /// Cada clase valora atributos distintos: los Defenders su DEF y bloqueo, los Snipers y Casters su DPS,
    /// los Vanguards su bajo coste de DP y los Specialists su redespliegue rápido.
    /// </summary>
    public override double CalculateCombatPower()
    {
        var s = Stats;
        double dps = s.DamagePerSecond;
        double tactical = Class switch
        {
            OperatorClass.Defender => s.Def * 1.5 + s.Hp * 0.15 + s.Block * 50 + dps * 0.3,
            OperatorClass.Guard => dps * 1.2 + s.Hp * 0.1 + s.Def * 0.4 + s.Block * 25,
            OperatorClass.Sniper => dps * 1.5 + s.Hp * 0.05,
            OperatorClass.Caster => dps * 1.4 + s.Res * 3,
            OperatorClass.Medic => dps * 1.1 + s.Hp * 0.05 + s.Res * 2,
            OperatorClass.Vanguard => dps + s.Hp * 0.08 + 1000.0 / Math.Max(s.Cost, 1),
            OperatorClass.Supporter => dps * 0.9 + s.Res * 4 + s.Hp * 0.05,
            OperatorClass.Specialist => dps + s.Hp * 0.08 + 2000.0 / Math.Max(s.RedeployTime, 1),
            _ => base.CalculateCombatPower()
        };
        return Math.Round(tactical * PromotionMultiplier, 2);
    }
}
