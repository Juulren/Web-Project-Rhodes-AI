using RhodesAI.Backend.Models.Domain;

namespace RhodesAI.Backend.Models.DTOs;

/// <summary>Proyección de lectura de un operador para la vista.</summary>
public record OperatorDto(
    string Id,
    string Name,
    int Rarity,
    OperatorClass Class,
    string Archetype,
    string Affiliation,
    string Description,
    string Biography,
    string ArtUrl,
    PromotionLevel Promotion,
    OperatorStats Stats,
    double CombatPower)
{
    public static OperatorDto From(Operator op) => new(
        op.Id, op.Name, op.Rarity, op.Class, op.Archetype, op.Affiliation, op.Description,
        op.Biography, op.ArtUrl, op.Promotion, op.Stats, op.CalculateCombatPower());
}
