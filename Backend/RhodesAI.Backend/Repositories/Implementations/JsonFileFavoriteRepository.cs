using System.Text.Json;
using System.Text.Json.Serialization;
using RhodesAI.Backend.Models.Domain;
using RhodesAI.Backend.Repositories.Interfaces;

namespace RhodesAI.Backend.Repositories.Implementations;

/// <summary>
/// Persistencia de favoritos en un archivo JSON que sobrevive a reinicios del backend.
/// Sustituye a InMemoryFavoriteRepository sin cambiar a sus consumidores (Liskov): ambas pasan la misma suite de contrato.
/// Cada escritura es atómica (archivo temporal + reemplazo) y un archivo dañado se aparta como .corrupt.
/// </summary>
public class JsonFileFavoriteRepository : IRepository<FavoriteSquadMember, string>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<string, FavoriteSquadMember> _items;

    public string FilePath { get; }

    public JsonFileFavoriteRepository(string filePath)
    {
        FilePath = Path.GetFullPath(filePath);
        _items = Load().ToDictionary(f => f.Id);
    }

    public async Task<IReadOnlyList<FavoriteSquadMember>> GetAllAsync()
    {
        await _lock.WaitAsync();
        try
        {
            return _items.Values.OrderBy(f => f.Priority).ThenBy(f => f.CreatedAt).ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<FavoriteSquadMember?> GetByIdAsync(string id)
    {
        await _lock.WaitAsync();
        try
        {
            return _items.GetValueOrDefault(id);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<FavoriteSquadMember> AddAsync(FavoriteSquadMember entity)
    {
        await _lock.WaitAsync();
        try
        {
            if (!_items.TryAdd(entity.Id, entity))
                throw new InvalidOperationException($"Ya existe un favorito con id {entity.Id}.");
            await SaveAsync();
            return entity;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> UpdateAsync(FavoriteSquadMember entity)
    {
        await _lock.WaitAsync();
        try
        {
            if (!_items.ContainsKey(entity.Id))
                return false;
            _items[entity.Id] = entity;
            await SaveAsync();
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> DeleteAsync(string id)
    {
        await _lock.WaitAsync();
        try
        {
            if (!_items.Remove(id))
                return false;
            await SaveAsync();
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Forma plana en disco; el dominio se reconstruye con FavoriteSquadMember.Restore.</summary>
    private sealed record FavoriteRecord(
        string Id, string OperatorName, string SquadRole, int Priority, int Rating,
        PromotionLevel PreferredPromotion, string TacticalNotes, DateTime CreatedAt, DateTime UpdatedAt);

    private List<FavoriteSquadMember> Load()
    {
        if (!File.Exists(FilePath))
            return [];
        try
        {
            var records = JsonSerializer.Deserialize<List<FavoriteRecord>>(File.ReadAllText(FilePath), JsonOptions) ?? [];
            return records
                .Select(r => FavoriteSquadMember.Restore(r.Id, r.OperatorName, r.SquadRole, r.Priority, r.Rating,
                    r.PreferredPromotion, r.TacticalNotes, r.CreatedAt, r.UpdatedAt))
                .ToList();
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            // No se pierde nada: el archivo dañado queda aparte para revisarlo y se empieza de cero.
            File.Copy(FilePath, FilePath + ".corrupt", overwrite: true);
            return [];
        }
    }

    private async Task SaveAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var records = _items.Values.Select(f => new FavoriteRecord(f.Id, f.OperatorName, f.SquadRole, f.Priority,
            f.Rating, f.PreferredPromotion, f.TacticalNotes, f.CreatedAt, f.UpdatedAt));

        var temp = FilePath + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(records, JsonOptions));
        File.Move(temp, FilePath, overwrite: true);
    }
}
