namespace RhodesAI.Backend.Models.Domain;

/// <summary>Objeto de valor inmutable con los atributos de combate de un operador.</summary>
public sealed class OperatorStats
{
    public int Hp { get; }
    public int Atk { get; }
    public int Def { get; }
    public int Res { get; }
    public int Cost { get; }
    public int Block { get; }
    public double AttackInterval { get; }
    public int RedeployTime { get; }

    public OperatorStats(int hp, int atk, int def, int res = 0, int cost = 0, int block = 1,
        double attackInterval = 1.0, int redeployTime = 70)
    {
        Hp = NonNegative(hp, nameof(hp));
        Atk = NonNegative(atk, nameof(atk));
        Def = NonNegative(def, nameof(def));
        Res = NonNegative(res, nameof(res));
        Cost = NonNegative(cost, nameof(cost));
        Block = NonNegative(block, nameof(block));
        RedeployTime = NonNegative(redeployTime, nameof(redeployTime));
        if (attackInterval <= 0)
            throw new ArgumentOutOfRangeException(nameof(attackInterval), "El intervalo de ataque debe ser mayor que cero.");
        AttackInterval = attackInterval;
    }

    /// <summary>Daño por segundo aproximado (ATK / intervalo de ataque).</summary>
    public double DamagePerSecond => Atk / AttackInterval;

    private static int NonNegative(int value, string name) =>
        value < 0 ? throw new ArgumentOutOfRangeException(name, $"{name} no puede ser negativo.") : value;
}
