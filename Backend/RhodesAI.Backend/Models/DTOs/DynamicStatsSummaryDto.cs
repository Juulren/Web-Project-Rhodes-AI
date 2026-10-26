namespace RhodesAI.Backend.Models.DTOs;

/// <summary>Una barra de distribución: cuántos operadores tiene la categoría y qué porcentaje del grupo representan.</summary>
public record DistributionEntryDto(string Label, int Count, double Percent);

/// <summary>Resumen estadístico del grupo de operadores visible tras aplicar filtros.</summary>
public class DynamicStatsSummaryDto
{
    public int Total { get; init; }
    public double AverageHp { get; init; }
    public double AverageAtk { get; init; }
    public double AverageDef { get; init; }
    public double AverageCost { get; init; }
    /// <summary>Clases presentes, de mayor a menor cantidad.</summary>
    public IReadOnlyList<DistributionEntryDto> ClassDistribution { get; init; } = [];
    /// <summary>Las seis rarezas, de 6★ a 1★, incluidas las que no tienen operadores.</summary>
    public IReadOnlyList<DistributionEntryDto> RarityDistribution { get; init; } = [];
}
