using RhodesAI.Backend.Models.DTOs;

namespace RhodesAI.Backend.Services.Interfaces;

/// <summary>Cálculos estadísticos y comparativos sobre los operadores.</summary>
public interface IOperatorStatsService
{
    Task<DynamicStatsSummaryDto> GetSummaryAsync(OperatorFilterDto filter, CancellationToken ct = default);
    /// <returns>null si alguno de los dos operadores no existe.</returns>
    Task<ComparisonResultDto?> CompareAsync(string operatorA, string operatorB, CancellationToken ct = default);
}
