namespace RhodesAI.Backend.Models.DTOs;

/// <summary>Cuerpo de POST /api/comparisons.</summary>
public class ComparisonRequestDto
{
    public string OperatorA { get; set; } = string.Empty;
    public string OperatorB { get; set; } = string.Empty;
}

/// <summary>Un atributo comparado cara a cara. Winner vale "A", "B" o "Empate".</summary>
public record AttributeComparisonDto(string Attribute, double ValueA, double ValueB, string Winner);

/// <summary>Resultado del análisis comparativo entre dos operadores.</summary>
public class ComparisonResultDto
{
    public OperatorDto OperatorA { get; init; } = default!;
    public OperatorDto OperatorB { get; init; } = default!;
    public IReadOnlyList<AttributeComparisonDto> Attributes { get; init; } = [];
    /// <summary>Puntuación de cada estrategia registrada (clave de la estrategia → [A, B]).</summary>
    public IReadOnlyDictionary<string, double[]> StrategyScores { get; init; } = new Dictionary<string, double[]>();
}
