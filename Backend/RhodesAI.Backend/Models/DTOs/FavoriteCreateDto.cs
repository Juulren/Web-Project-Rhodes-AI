using System.ComponentModel.DataAnnotations;
using RhodesAI.Backend.Models.Domain;

namespace RhodesAI.Backend.Models.DTOs;

/// <summary>Cuerpo de POST /api/favorites.</summary>
public class FavoriteCreateDto
{
    [Required]
    public string OperatorName { get; set; } = string.Empty;
    public string? SquadRole { get; set; }
    [Range(1, 5)]
    public int Priority { get; set; } = 3;
    [Range(1, 5)]
    public int Rating { get; set; } = 3;
    public PromotionLevel PreferredPromotion { get; set; } = PromotionLevel.Elite2;
    [MaxLength(FavoriteSquadMember.MaxNotesLength)]
    public string? TacticalNotes { get; set; }
}
