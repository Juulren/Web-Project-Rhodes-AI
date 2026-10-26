using RhodesAI.Backend.Models.Base;

namespace RhodesAI.Backend.Models.Domain;

/// <summary>
/// Operador favorito dentro del escuadrón del usuario, con rol, prioridad, calificación y nota táctica.
/// </summary>
public class FavoriteSquadMember : BaseEntity
{
    public const int MaxNotesLength = 500;

    public string OperatorName { get; private set; }
    public string SquadRole { get; private set; } = string.Empty;
    public int Priority { get; private set; }
    public int Rating { get; private set; }
    public PromotionLevel PreferredPromotion { get; private set; }
    public string TacticalNotes { get; private set; } = string.Empty;

    public FavoriteSquadMember(string operatorName, string squadRole = "", int priority = 3, int rating = 3,
        PromotionLevel preferredPromotion = PromotionLevel.Elite2, string tacticalNotes = "", string? id = null)
        : base(id)
    {
        OperatorName = operatorName?.Trim() ?? string.Empty;
        SquadRole = squadRole?.Trim() ?? string.Empty;
        Priority = InRange(priority, nameof(priority));
        Rating = InRange(rating, nameof(rating));
        PreferredPromotion = preferredPromotion;
        TacticalNotes = CleanNotes(tacticalNotes);
        EnsureValid();
    }

    public void UpdateTacticalNotes(string notes)
    {
        TacticalNotes = CleanNotes(notes);
        Touch();
    }

    public void AssignRole(string role)
    {
        SquadRole = role?.Trim() ?? string.Empty;
        Touch();
    }

    public void ChangePriority(int priority)
    {
        Priority = InRange(priority, nameof(priority));
        Touch();
    }

    public void Rate(int rating)
    {
        Rating = InRange(rating, nameof(rating));
        Touch();
    }

    public void SetPreferredPromotion(PromotionLevel level)
    {
        if (!Enum.IsDefined(level))
            throw new ArgumentOutOfRangeException(nameof(level));
        PreferredPromotion = level;
        Touch();
    }

    public override IReadOnlyList<string> Validate()
    {
        var errors = new List<string>(base.Validate());
        if (string.IsNullOrWhiteSpace(OperatorName))
            errors.Add("El nombre del operador favorito es obligatorio.");
        return errors;
    }

    private static int InRange(int value, string name) =>
        value is < 1 or > 5 ? throw new ArgumentOutOfRangeException(name, $"{name} debe estar entre 1 y 5.") : value;

    private static string CleanNotes(string? notes)
    {
        var clean = notes?.Trim() ?? string.Empty;
        if (clean.Length > MaxNotesLength)
            throw new ArgumentException($"La nota táctica no puede superar {MaxNotesLength} caracteres.", nameof(notes));
        return clean;
    }
}
