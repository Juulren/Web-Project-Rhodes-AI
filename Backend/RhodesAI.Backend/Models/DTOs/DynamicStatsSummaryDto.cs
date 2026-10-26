namespace RhodesAI.Backend.Models.DTOs;

/// <summary>Resumen estadístico del grupo de operadores visible tras aplicar filtros.</summary>
public class DynamicStatsSummaryDto
{
    public int Total { get; init; }
    public double AverageHp { get; init; }
    public double AverageAtk { get; init; }
    public double AverageDef { get; init; }
    public double AverageCost { get; init; }
}
