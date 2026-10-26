using RhodesAI.Backend.Models.Domain;
using RhodesAI.Backend.Models.DTOs;
using RhodesAI.Backend.Services.Interfaces;

namespace RhodesAI.Backend.Services.Implementations;

/// <summary>
/// Reglas de negocio sobre el catálogo: búsqueda, filtrado, estadísticas y comparación.
/// Las fórmulas de evaluación llegan inyectadas como estrategias (Abierto/Cerrado).
/// </summary>
public class RhodesOperatorService(IRhodesApiClient client, IEnumerable<IStatCalculatorStrategy> strategies)
    : IOperatorReadService, IOperatorStatsService
{
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

        return query.ToList();
    }

    public async Task<DynamicStatsSummaryDto> GetSummaryAsync(OperatorFilterDto filter, CancellationToken ct = default)
    {
        var visible = await FilterAsync(filter, ct);
        if (visible.Count == 0)
            return new DynamicStatsSummaryDto();

        return new DynamicStatsSummaryDto
        {
            Total = visible.Count,
            AverageHp = Math.Round(visible.Average(o => o.Stats.Hp), 1),
            AverageAtk = Math.Round(visible.Average(o => o.Stats.Atk), 1),
            AverageDef = Math.Round(visible.Average(o => o.Stats.Def), 1),
            AverageCost = Math.Round(visible.Average(o => o.Stats.Cost), 1)
        };
    }

    public async Task<ComparisonResultDto?> CompareAsync(string operatorA, string operatorB, CancellationToken ct = default)
    {
        var a = await GetByNameAsync(operatorA, ct);
        var b = await GetByNameAsync(operatorB, ct);
        if (a is null || b is null)
            return null;

        return new ComparisonResultDto
        {
            OperatorA = OperatorDto.From(a),
            OperatorB = OperatorDto.From(b),
            Attributes =
            [
                Compare("HP", a.Stats.Hp, b.Stats.Hp),
                Compare("ATK", a.Stats.Atk, b.Stats.Atk),
                Compare("DEF", a.Stats.Def, b.Stats.Def),
                Compare("RES", a.Stats.Res, b.Stats.Res),
                Compare("Cost", a.Stats.Cost, b.Stats.Cost, lowerIsBetter: true),
                Compare("Block", a.Stats.Block, b.Stats.Block),
                Compare("Attack Time", a.Stats.AttackInterval, b.Stats.AttackInterval, lowerIsBetter: true)
            ],
            StrategyScores = strategies.ToDictionary(s => s.Key, s => new[] { s.Calculate(a), s.Calculate(b) })
        };
    }

    private static AttributeComparisonDto Compare(string attribute, double a, double b, bool lowerIsBetter = false)
    {
        string winner = a.Equals(b) ? "Empate" : (a > b) ^ lowerIsBetter ? "A" : "B";
        return new AttributeComparisonDto(attribute, a, b, winner);
    }

    private static bool Contains(string source, string term) =>
        source.Contains(term, StringComparison.OrdinalIgnoreCase);
}
