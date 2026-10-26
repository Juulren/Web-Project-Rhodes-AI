using Microsoft.AspNetCore.Mvc;
using RhodesAI.Backend.Controllers;
using RhodesAI.Backend.Models.Domain;
using RhodesAI.Backend.Models.DTOs;
using RhodesAI.Backend.Services.Implementations;
using RhodesAI.Backend.Services.Interfaces;

namespace RhodesAI.Tests;

/// <summary>Retos adicionales en el backend: ordenamiento, estadísticas dinámicas con distribución y comparador.</summary>
public class ChallengesServiceTests
{
    private sealed class FakeApiClient(IReadOnlyList<Operator> operators) : IRhodesApiClient
    {
        public Task<IReadOnlyList<Operator>> GetOperatorsAsync(CancellationToken ct = default) => Task.FromResult(operators);
    }

    private static Operator Op(string name, int rarity, OperatorClass cls, int hp = 1000, int atk = 400, int def = 200,
        int res = 0, int cost = 15, int block = 1, double interval = 1.2) =>
        new CombatOperator(name, rarity, cls,
            new Dictionary<PromotionLevel, OperatorStats> { [PromotionLevel.Base] = new(hp, atk, def, res, cost, block, interval) },
            PromotionLevel.Base);

    private static readonly Operator Saria = Op("Saria", 6, OperatorClass.Defender, hp: 3000, atk: 400, def: 600, res: 10, cost: 20, block: 3, interval: 1.2);
    private static readonly Operator Exusiai = Op("Exusiai", 6, OperatorClass.Sniper, hp: 1600, atk: 540, def: 160, cost: 12, interval: 1.0);

    private static Operator[] Catalog() =>
    [
        Saria,
        Op("'Justice Knight'", 1, OperatorClass.Sniper, hp: 500, atk: 170, def: 40, cost: 3),
        Exusiai,
        Op("Myrtle", 4, OperatorClass.Vanguard, hp: 1300, atk: 330, def: 230, cost: 8),
        Op("Ángel", 5, OperatorClass.Guard, hp: 2000, atk: 500, def: 300, cost: 16)
    ];

    private static RhodesOperatorService Service(params Operator[] operators) =>
        new(new FakeApiClient(operators.Length > 0 ? operators : Catalog()), [new EliteTwoStatCalculator()]);

    private static async Task<string[]> Sorted(OperatorSort sort) =>
        (await Service().FilterAsync(new OperatorFilterDto { SortBy = sort })).Select(o => o.Name).ToArray();

    // ---------- Reto 2: orden ----------

    [Fact]
    public async Task Filter_SortsAlphabeticallyIgnoringQuotesAndAccents()
    {
        Assert.Equal(new[] { "Ángel", "Exusiai", "'Justice Knight'", "Myrtle", "Saria" }, await Sorted(OperatorSort.NameAsc));
        Assert.Equal(new[] { "Saria", "Myrtle", "'Justice Knight'", "Exusiai", "Ángel" }, await Sorted(OperatorSort.NameDesc));
    }

    [Fact]
    public async Task Filter_SortsByRarityWithNameAsTieBreaker()
    {
        Assert.Equal(new[] { "Exusiai", "Saria", "Ángel", "Myrtle", "'Justice Knight'" }, await Sorted(OperatorSort.RarityDesc));
        Assert.Equal(new[] { "'Justice Knight'", "Myrtle", "Ángel", "Exusiai", "Saria" }, await Sorted(OperatorSort.RarityAsc));
    }

    // ---------- Reto 3: estadísticas dinámicas ----------

    [Fact]
    public async Task Summary_ComputesAveragesAndClassDistribution()
    {
        var summary = await Service().GetSummaryAsync(new OperatorFilterDto());

        Assert.Equal(5, summary.Total);
        Assert.Equal(1680, summary.AverageHp);
        Assert.Equal(11.8, summary.AverageCost);
        Assert.Equal(new[]
        {
            new DistributionEntryDto("Sniper", 2, 40),
            new DistributionEntryDto("Defender", 1, 20),
            new DistributionEntryDto("Guard", 1, 20),
            new DistributionEntryDto("Vanguard", 1, 20)
        }, summary.ClassDistribution);
    }

    [Fact]
    public async Task Summary_ListsAllSixRaritiesFromSixToOne()
    {
        var summary = await Service().GetSummaryAsync(new OperatorFilterDto());

        Assert.Equal(new[] { "6★", "5★", "4★", "3★", "2★", "1★" }, summary.RarityDistribution.Select(r => r.Label));
        Assert.Equal(new[] { 2, 1, 1, 0, 0, 1 }, summary.RarityDistribution.Select(r => r.Count));
        Assert.Equal(new[] { 40.0, 20, 20, 0, 0, 20 }, summary.RarityDistribution.Select(r => r.Percent));
    }

    [Fact]
    public async Task Summary_RoundsPercentagesAndFollowsTheFilter()
    {
        var summary = await Service(Catalog()[..3]).GetSummaryAsync(new OperatorFilterDto());
        Assert.Equal(new[] { 66.7, 33.3 }, summary.ClassDistribution.Select(c => c.Percent));

        var snipers = await Service().GetSummaryAsync(new OperatorFilterDto { Class = OperatorClass.Sniper });
        Assert.Equal(2, snipers.Total);
        Assert.Equal(100, Assert.Single(snipers.ClassDistribution).Percent);
    }

    [Fact]
    public async Task Summary_EmptyGroupReturnsZerosAndSixEmptyRarities()
    {
        var summary = await Service().GetSummaryAsync(new OperatorFilterDto { Search = "nadie" });

        Assert.Equal(0, summary.Total);
        Assert.Empty(summary.ClassDistribution);
        Assert.Equal(6, summary.RarityDistribution.Count);
        Assert.All(summary.RarityDistribution, r => Assert.Equal(0, r.Percent));
    }

    // ---------- Reto 4: comparador ----------

    [Fact]
    public async Task Compare_MarksLowerIsBetterForCostAndAttackTime()
    {
        var result = (await Service().CompareAsync("Saria", "Exusiai"))!;
        var byName = result.Attributes.ToDictionary(a => a.Attribute);

        Assert.True(byName["Cost"].LowerIsBetter);
        Assert.True(byName["Attack Time"].LowerIsBetter);
        Assert.False(byName["HP"].LowerIsBetter);
        Assert.Equal("B", byName["Cost"].Winner);
        Assert.Equal("B", byName["Attack Time"].Winner);
        Assert.Equal("A", byName["Block"].Winner);
    }

    [Fact]
    public async Task Compare_ScalesBarsAgainstTheLargerValue()
    {
        var result = (await Service().CompareAsync("Saria", "Exusiai"))!;
        var hp = result.Attributes.Single(a => a.Attribute == "HP");
        var res = result.Attributes.Single(a => a.Attribute == "RES");

        Assert.Equal(1, hp.RatioA);
        Assert.Equal(1600.0 / 3000, hp.RatioB, 4);
        Assert.Equal(0, res.RatioB);
    }

    [Fact]
    public async Task Compare_CountsWinsAndDecidesOverallWinner()
    {
        var result = (await Service().CompareAsync("Saria", "Exusiai"))!;

        Assert.Equal(4, result.WinsA);
        Assert.Equal(3, result.WinsB);
        Assert.Equal("A", result.OverallWinner);
        Assert.True(result.StrategyScores.ContainsKey("elite2CombatPower"));
    }

    [Fact]
    public async Task Compare_SameOperatorIsATieEverywhere()
    {
        var result = (await Service().CompareAsync("Myrtle", "myrtle"))!;

        Assert.All(result.Attributes, a => Assert.Equal("Empate", a.Winner));
        Assert.Equal("Empate", result.OverallWinner);
        Assert.Equal(0, result.WinsA);
    }

    // ---------- Controladores ----------

    [Fact]
    public async Task OperatorsController_StatsEndpointReturnsDistributions()
    {
        var service = Service();
        var controller = new OperatorsController(service, service);

        var ok = Assert.IsType<OkObjectResult>(await controller.Stats(new OperatorFilterDto(), CancellationToken.None));
        var summary = Assert.IsType<DynamicStatsSummaryDto>(ok.Value);

        Assert.Equal(4, summary.ClassDistribution.Count);
    }

    [Fact]
    public async Task ComparisonsController_ReturnsOverallWinner()
    {
        var controller = new ComparisonsController(Service());

        var ok = Assert.IsType<OkObjectResult>((await controller.Compare(
            new ComparisonRequestDto { OperatorA = "Saria", OperatorB = "Exusiai" }, CancellationToken.None)).Result);

        Assert.Equal("A", Assert.IsType<ComparisonResultDto>(ok.Value).OverallWinner);
    }
}
