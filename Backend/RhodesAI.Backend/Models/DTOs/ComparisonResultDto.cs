namespace RhodesAI.Backend.Models.DTOs;

/// <summary>Cuerpo de POST /api/comparisons.</summary>
public class ComparisonRequestDto
{
    public string OperatorA { get; set; } = string.Empty;
    public string OperatorB { get; set; } = string.Empty;
}

/// <summary>Un atributo comparado cara a cara. Winner vale "A", "B" o "Empate".</summary>
public record AttributeComparisonDto(string Attribute, double ValueA, double ValueB, string Winner)
{
    /// <summary>En coste e intervalo de ataque gana el valor menor.</summary>
    public bool LowerIsBetter { get; init; }
    /// <summary>Longitud de la barra de A respecto al mayor de los dos valores (0 a 1).</summary>
    public double RatioA { get; init; }
    public double RatioB { get; init; }
}

/// <summary>Resultado del análisis comparativo entre dos operadores.</summary>
public class ComparisonResultDto
{
    public OperatorDto OperatorA { get; init; } = default!;
    public OperatorDto OperatorB { get; init; } = default!;
    public IReadOnlyList<AttributeComparisonDto> Attributes { get; init; } = [];
    public int WinsA { get; init; }
    public int WinsB { get; init; }
    /// <summary>"A", "B" o "Empate" según cuántos atributos gana cada uno.</summary>
    public string OverallWinner { get; init; } = string.Empty;
    /// <summary>Puntuación de cada estrategia registrada (clave de la estrategia → [A, B]).</summary>
    public IReadOnlyDictionary<string, double[]> StrategyScores { get; init; } = new Dictionary<string, double[]>();
}
