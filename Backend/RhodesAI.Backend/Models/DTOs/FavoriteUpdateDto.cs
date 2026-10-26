using System.ComponentModel.DataAnnotations;
using RhodesAI.Backend.Models.Domain;

namespace RhodesAI.Backend.Models.DTOs;

/// <summary>Cuerpo de PUT /api/favorites/{id}. Los campos nulos no se modifican.</summary>
public class FavoriteUpdateDto
{
    public string? SquadRole { get; set; }
    [Range(1, 5)]
    public int? Priority { get; set; }
    [Range(1, 5)]
    public int? Rating { get; set; }
    public PromotionLevel? PreferredPromotion { get; set; }
    [MaxLength(FavoriteSquadMember.MaxNotesLength)]
    public string? TacticalNotes { get; set; }
}
