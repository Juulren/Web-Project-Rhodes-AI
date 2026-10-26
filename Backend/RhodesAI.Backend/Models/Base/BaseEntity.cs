namespace RhodesAI.Backend.Models.Base;

/// <summary>
/// Abstracción raíz de todas las entidades del dominio.
/// Encapsula identidad y marcas de tiempo; las subclases sólo pueden mutarlas a través de Touch().
/// </summary>
public abstract class BaseEntity
{
    public string Id { get; protected set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    protected BaseEntity(string? id = null)
    {
        Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id.Trim();
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    /// <summary>Marca la entidad como modificada.</summary>
    protected void Touch() => UpdatedAt = DateTime.UtcNow;

    /// <summary>Restaura las fechas de auditoría al reconstruir una entidad desde la persistencia.</summary>
    protected void RestoreAudit(DateTime createdAt, DateTime updatedAt)
    {
        if (updatedAt < createdAt)
            throw new ArgumentException("La fecha de modificación no puede ser anterior a la de creación.");
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>Reglas de validación; las subclases las extienden con override.</summary>
    public virtual IReadOnlyList<string> Validate() =>
        string.IsNullOrWhiteSpace(Id) ? ["El identificador es obligatorio."] : [];

    public bool IsValid => Validate().Count == 0;

    /// <summary>Lanza ArgumentException si alguna invariante no se cumple.</summary>
    protected void EnsureValid()
    {
        var errors = Validate();
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors));
    }
}
