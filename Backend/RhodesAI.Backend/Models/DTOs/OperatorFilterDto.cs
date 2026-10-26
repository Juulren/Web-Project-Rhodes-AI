using RhodesAI.Backend.Models.Domain;

namespace RhodesAI.Backend.Models.DTOs;

/// <summary>Criterios de GET /api/operators/filter y /api/operators/stats. Todos son opcionales y se combinan con AND.</summary>
public class OperatorFilterDto
{
    /// <summary>Texto libre: nombre, afiliación, descripción o biografía.</summary>
    public string? Search { get; set; }
    public OperatorClass? Class { get; set; }
    public int? Rarity { get; set; }
    public string? Affiliation { get; set; }
}
