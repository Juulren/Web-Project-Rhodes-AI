using RhodesAI.Backend.Models.Domain;
using RhodesAI.Backend.Services.Interfaces;

namespace RhodesAI.Backend.Services.Implementations;

/// <summary>
/// Proyecta el poder de combate táctico del operador en su promoción máxima (Elite 2 cuando la rareza lo permite).
/// </summary>
public class EliteTwoStatCalculator : IStatCalculatorStrategy
{
    public string Key => "elite2CombatPower";

    public double Calculate(Operator op)
    {
        var projected = new CombatOperator(op.Name, op.Rarity, op.Class, op.StatsByPromotion, op.MaxPromotion);
        return projected.CalculateCombatPower();
    }
}
