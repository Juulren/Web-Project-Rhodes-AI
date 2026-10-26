using RhodesAI.Backend.Models.Base;
using RhodesAI.Backend.Models.Domain;

namespace RhodesAI.Tests;

/// <summary>Pruebas de POO: abstracción, encapsulamiento, herencia y polimorfismo del dominio.</summary>
public class DomainTests
{
    private static OperatorStats SampleStats(int def = 300, int atk = 500) =>
        new(hp: 2000, atk: atk, def: def, res: 10, cost: 18, block: 2, attackInterval: 1.2, redeployTime: 70);

    private static Dictionary<PromotionLevel, OperatorStats> ThreeTiers() => new()
    {
        [PromotionLevel.Base] = new OperatorStats(1000, 300, 150, 0, 15, 1, 1.2),
        [PromotionLevel.Elite1] = new OperatorStats(1500, 420, 210, 0, 15, 1, 1.2),
        [PromotionLevel.Elite2] = new OperatorStats(2000, 560, 280, 0, 15, 1, 1.2)
    };

    // ---------- Abstracción y herencia ----------

    [Fact]
    public void BaseEntity_IsAbstract_AndOperatorsInheritFromIt()
    {
        Assert.True(typeof(BaseEntity).IsAbstract);
        Assert.True(typeof(BaseEntity).IsAssignableFrom(typeof(Operator)));
        Assert.True(typeof(Operator).IsAssignableFrom(typeof(CombatOperator)));
        Assert.True(typeof(BaseEntity).IsAssignableFrom(typeof(FavoriteSquadMember)));
    }

    [Fact]
    public void Operator_GetsSlugIdAndTimestamps()
    {
        var op = new Operator("Silver Ash", 6, OperatorClass.Guard, SampleStats());

        Assert.Equal("silver-ash", op.Id);
        Assert.NotEqual(default, op.CreatedAt);
        Assert.Equal(op.CreatedAt, op.UpdatedAt);
        Assert.True(op.IsValid);
    }

    // ---------- Encapsulamiento ----------

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void Operator_RejectsRarityOutsideOneToSix(int rarity)
    {
        Assert.Throws<ArgumentException>(() => new Operator("Exusiai", rarity, OperatorClass.Sniper, SampleStats()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Operator_RejectsEmptyName(string name)
    {
        Assert.Throws<ArgumentException>(() => new Operator(name, 6, OperatorClass.Sniper, SampleStats()));
    }

    [Fact]
    public void OperatorStats_RejectsNegativeValuesAndZeroInterval()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new OperatorStats(-1, 100, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OperatorStats(100, 100, 100, attackInterval: 0));
    }

    [Fact]
    public void OperatorStats_ComputesDamagePerSecond()
    {
        var stats = new OperatorStats(1000, 600, 100, attackInterval: 1.5);
        Assert.Equal(400, stats.DamagePerSecond, 2);
    }

    [Fact]
    public void Operator_ExposesReadOnlyStatsDictionary()
    {
        var op = new Operator("Saria", 6, OperatorClass.Defender, ThreeTiers(), PromotionLevel.Elite2);
        Assert.False(op.StatsByPromotion is IDictionary<PromotionLevel, OperatorStats> { IsReadOnly: false });
    }

    [Fact]
    public void Operator_CannotStartAbovePromotionAllowedByRarity()
    {
        // Las unidades de 3★ sólo llegan a Elite 1.
        Assert.Throws<ArgumentException>(() =>
            new Operator("Kroos", 3, OperatorClass.Sniper, SampleStats(), PromotionLevel.Elite2));
    }

    [Fact]
    public void Promote_AdvancesAndTouchesEntity()
    {
        var op = new Operator("Saria", 6, OperatorClass.Defender, ThreeTiers());
        var before = op.UpdatedAt;
        Thread.Sleep(5);

        op.Promote(PromotionLevel.Elite2);

        Assert.Equal(PromotionLevel.Elite2, op.Promotion);
        Assert.Equal(2000, op.Stats.Hp);
        Assert.True(op.UpdatedAt > before);
    }

    [Fact]
    public void Promote_RejectsDemotionAndRarityCap()
    {
        var saria = new Operator("Saria", 6, OperatorClass.Defender, ThreeTiers(), PromotionLevel.Elite1);
        Assert.Throws<InvalidOperationException>(() => saria.Promote(PromotionLevel.Base));

        var melantha = new Operator("Melantha", 3, OperatorClass.Guard, SampleStats());
        Assert.Throws<InvalidOperationException>(() => melantha.Promote(PromotionLevel.Elite2));
    }

    [Fact]
    public void GetStatsFor_FallsBackToHighestAvailableTier()
    {
        // Operadores sin datos de E2 en la API usan los de la mejor promoción disponible.
        var tiers = ThreeTiers();
        tiers.Remove(PromotionLevel.Elite2);
        var op = new Operator("Myrtle", 4, OperatorClass.Vanguard, tiers);

        Assert.Equal(1500, op.GetStatsFor(PromotionLevel.Elite2).Hp);
    }

    // ---------- Polimorfismo ----------

    [Fact]
    public void CalculateCombatPower_GrowsWithPromotion()
    {
        var baseOp = new Operator("Saria", 6, OperatorClass.Defender, SampleStats(), PromotionLevel.Base);
        var e1 = new Operator("Saria", 6, OperatorClass.Defender, SampleStats(), PromotionLevel.Elite1);
        var e2 = new Operator("Saria", 6, OperatorClass.Defender, SampleStats(), PromotionLevel.Elite2);

        Assert.True(e1.CalculateCombatPower() > baseOp.CalculateCombatPower());
        Assert.True(e2.CalculateCombatPower() > e1.CalculateCombatPower());
    }

    [Fact]
    public void CombatOperator_OverridesCombatPowerThroughBaseReference()
    {
        Operator generic = new Operator("Hoshiguma", 6, OperatorClass.Defender, SampleStats());
        Operator tactical = new CombatOperator("Hoshiguma", 6, OperatorClass.Defender, SampleStats());

        Assert.NotEqual(generic.CalculateCombatPower(), tactical.CalculateCombatPower());
    }

    [Fact]
    public void CombatOperator_DefenderValuesDefenseMoreThanSniper()
    {
        var lowDef = SampleStats(def: 100);
        var highDef = SampleStats(def: 700);

        double defenderGain = new CombatOperator("A", 6, OperatorClass.Defender, highDef).CalculateCombatPower()
                            - new CombatOperator("A", 6, OperatorClass.Defender, lowDef).CalculateCombatPower();
        double sniperGain = new CombatOperator("B", 6, OperatorClass.Sniper, highDef).CalculateCombatPower()
                          - new CombatOperator("B", 6, OperatorClass.Sniper, lowDef).CalculateCombatPower();

        Assert.True(defenderGain > sniperGain);
    }

    [Fact]
    public void CombatOperator_SniperValuesAttackMoreThanDefender()
    {
        var lowAtk = SampleStats(atk: 300);
        var highAtk = SampleStats(atk: 900);

        double sniperGain = new CombatOperator("A", 6, OperatorClass.Sniper, highAtk).CalculateCombatPower()
                          - new CombatOperator("A", 6, OperatorClass.Sniper, lowAtk).CalculateCombatPower();
        double defenderGain = new CombatOperator("B", 6, OperatorClass.Defender, highAtk).CalculateCombatPower()
                            - new CombatOperator("B", 6, OperatorClass.Defender, lowAtk).CalculateCombatPower();

        Assert.True(sniperGain > defenderGain);
    }

    [Theory]
    [InlineData(OperatorClass.Vanguard)]
    [InlineData(OperatorClass.Guard)]
    [InlineData(OperatorClass.Defender)]
    [InlineData(OperatorClass.Sniper)]
    [InlineData(OperatorClass.Caster)]
    [InlineData(OperatorClass.Medic)]
    [InlineData(OperatorClass.Supporter)]
    [InlineData(OperatorClass.Specialist)]
    public void CombatOperator_ProducesPositivePowerForEveryClass(OperatorClass cls)
    {
        var op = new CombatOperator("Test", 5, cls, SampleStats(), PromotionLevel.Elite2);
        Assert.True(op.CalculateCombatPower() > 0);
    }

    // ---------- FavoriteSquadMember ----------

    [Fact]
    public void Favorite_UpdateTacticalNotes_TrimsAndTouches()
    {
        var fav = new FavoriteSquadMember("Eyjafjalla", "DPS de área");
        var before = fav.UpdatedAt;
        Thread.Sleep(5);

        fav.UpdateTacticalNotes("  Desplegar en el tile central  ");

        Assert.Equal("Desplegar en el tile central", fav.TacticalNotes);
        Assert.True(fav.UpdatedAt > before);
    }

    [Fact]
    public void Favorite_RejectsNotesLongerThanLimit()
    {
        var fav = new FavoriteSquadMember("Eyjafjalla");
        Assert.Throws<ArgumentException>(() => fav.UpdateTacticalNotes(new string('x', FavoriteSquadMember.MaxNotesLength + 1)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Favorite_RejectsPriorityAndRatingOutsideOneToFive(int value)
    {
        var fav = new FavoriteSquadMember("Eyjafjalla");
        Assert.Throws<ArgumentOutOfRangeException>(() => fav.ChangePriority(value));
        Assert.Throws<ArgumentOutOfRangeException>(() => fav.Rate(value));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FavoriteSquadMember("Ifrit", priority: value));
    }

    [Fact]
    public void Favorite_RequiresOperatorName()
    {
        Assert.Throws<ArgumentException>(() => new FavoriteSquadMember(" "));
    }
}
