using System.Globalization;
using RhodesAI.Backend.Models.Domain;
using RhodesAI.Backend.Models.DTOs;
using RhodesAI.Backend.Services.Interfaces;

namespace RhodesAI.Backend.Services.Implementations;

/// <summary>
/// Reglas de negocio sobre el catálogo: búsqueda, filtrado, ordenamiento, estadísticas y comparación.
/// Las fórmulas de evaluación llegan inyectadas como estrategias (Abierto/Cerrado).
/// </summary>
public class RhodesOperatorService(IRhodesApiClient client, IEnumerable<IStatCalculatorStrategy> strategies)
    : IOperatorReadService, IOperatorStatsService
{
    /// <summary>Orden alfabético que ignora comillas, acentos y mayúsculas ('Justice Knight' va con la J).</summary>
    private static readonly StringComparer NameComparer = StringComparer.Create(
        CultureInfo.GetCultureInfo("es"), CompareOptions.IgnoreSymbols | CompareOptions.IgnoreNonSpace | CompareOptions.IgnoreCase);

    public Task<IReadOnlyList<Operator>> GetAllAsync(CancellationToken ct = default) => client.GetOperatorsAsync(ct);

    public async Task<Operator?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        var key = name.Trim();
        var all = await client.GetOperatorsAsync(ct);
        return all.FirstOrDefault(o => o.Name.Equals(key, StringComparison.OrdinalIgnoreCase)
                                    || o.Id.Equals(key, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<Operator>> FilterAsync(OperatorFilterDto filter, CancellationToken ct = default)
    {
        IEnumerable<Operator> query = await client.GetOperatorsAsync(ct);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(o => Contains(o.Name, term) || Contains(o.Affiliation, term)
                                  || Contains(o.Description, term) || Contains(o.Biography, term));
        }
        if (filter.Class is OperatorClass cls)
            query = query.Where(o => o.Class == cls);
        if (filter.Rarity is int rarity)
            query = query.Where(o => o.Rarity == rarity);
        if (!string.IsNullOrWhiteSpace(filter.Affiliation))
            query = query.Where(o => o.Affiliation.Equals(filter.Affiliation.Trim(), StringComparison.OrdinalIgnoreCase));

        return Sort(query, filter.SortBy).ToList();
    }

    private static IEnumerable<Operator> Sort(IEnumerable<Operator> operators, OperatorSort sort) => sort switch
    {
        OperatorSort.NameDesc => operators.OrderByDescending(o => o.Name, NameComparer),
        OperatorSort.RarityDesc => operators.OrderByDescending(o => o.Rarity).ThenBy(o => o.Name, NameComparer),
        OperatorSort.RarityAsc => operators.OrderBy(o => o.Rarity).ThenBy(o => o.Name, NameComparer),
        _ => operators.OrderBy(o => o.Name, NameComparer)
    };

    public async Task<DynamicStatsSummaryDto> GetSummaryAsync(OperatorFilterDto filter, CancellationToken ct = default)
    {
        var visible = await FilterAsync(filter, ct);
        int total = visible.Count;

        var classes = visible
            .GroupBy(o => o.Class)
            .Select(g => new DistributionEntryDto(g.Key.ToString(), g.Count(), Percent(g.Count(), total)))
            .OrderByDescending(e => e.Count)
            .ThenBy(e => e.Label, NameComparer)
            .ToList();

        var rarities = Enumerable.Range(1, 6).Reverse()
            .Select(stars =>
            {
                int count = visible.Count(o => o.Rarity == stars);
                return new DistributionEntryDto($"{stars}★", count, Percent(count, total));
            })
            .ToList();

        return new DynamicStatsSummaryDto
        {
            Total = total,
            AverageHp = Average(visible, o => o.Stats.Hp),
            AverageAtk = Average(visible, o => o.Stats.Atk),
            AverageDef = Average(visible, o => o.Stats.Def),
            AverageCost = Average(visible, o => o.Stats.Cost),
            ClassDistribution = classes,
            RarityDistribution = rarities
        };
    }

    public async Task<ComparisonResultDto?> CompareAsync(string operatorA, string operatorB, CancellationToken ct = default)
    {
        var a = await GetByNameAsync(operatorA, ct);
        var b = await GetByNameAsync(operatorB, ct);
        if (a is null || b is null)
            return null;

        AttributeComparisonDto[] attributes =
        [
            Compare("HP", a.Stats.Hp, b.Stats.Hp),
            Compare("ATK", a.Stats.Atk, b.Stats.Atk),
            Compare("DEF", a.Stats.Def, b.Stats.Def),
            Compare("RES", a.Stats.Res, b.Stats.Res),
            Compare("Cost", a.Stats.Cost, b.Stats.Cost, lowerIsBetter: true),
            Compare("Block", a.Stats.Block, b.Stats.Block),
            Compare("Attack Time", a.Stats.AttackInterval, b.Stats.AttackInterval, lowerIsBetter: true)
        ];
        int winsA = attributes.Count(x => x.Winner == "A");
        int winsB = attributes.Count(x => x.Winner == "B");

        return new ComparisonResultDto
        {
            OperatorA = OperatorDto.From(a),
            OperatorB = OperatorDto.From(b),
            Attributes = attributes,
            WinsA = winsA,
            WinsB = winsB,
            OverallWinner = winsA == winsB ? "Empate" : winsA > winsB ? "A" : "B",
            StrategyScores = strategies.ToDictionary(s => s.Key, s => new[] { s.Calculate(a), s.Calculate(b) })
        };
    }

    private static AttributeComparisonDto Compare(string attribute, double a, double b, bool lowerIsBetter = false)
    {
        string winner = a.Equals(b) ? "Empate" : (a > b) ^ lowerIsBetter ? "A" : "B";
        double max = Math.Max(a, b);
        return new AttributeComparisonDto(attribute, a, b, winner)
        {
            LowerIsBetter = lowerIsBetter,
            RatioA = max > 0 ? a / max : 0,
            RatioB = max > 0 ? b / max : 0
        };
    }

    private static double Percent(int count, int total) => total == 0 ? 0 : Math.Round(count * 100.0 / total, 1);

    private static double Average(IReadOnlyList<Operator> operators, Func<Operator, int> selector) =>
        operators.Count == 0 ? 0 : Math.Round(operators.Average(selector), 1);

    private static bool Contains(string source, string term) =>
        source.Contains(term, StringComparison.OrdinalIgnoreCase);
}
