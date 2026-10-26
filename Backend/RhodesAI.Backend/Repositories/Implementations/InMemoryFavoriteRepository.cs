using System.Collections.Concurrent;
using RhodesAI.Backend.Models.Domain;
using RhodesAI.Backend.Repositories.Interfaces;

namespace RhodesAI.Backend.Repositories.Implementations;

/// <summary>Persistencia en memoria, segura para concurrencia, de los favoritos.</summary>
public class InMemoryFavoriteRepository : IRepository<FavoriteSquadMember, string>
{
    private readonly ConcurrentDictionary<string, FavoriteSquadMember> _store = new();

    public Task<IReadOnlyList<FavoriteSquadMember>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<FavoriteSquadMember>>(
            _store.Values.OrderBy(f => f.Priority).ThenBy(f => f.CreatedAt).ToList());

    public Task<FavoriteSquadMember?> GetByIdAsync(string id) =>
        Task.FromResult(_store.TryGetValue(id, out var favorite) ? favorite : null);

    public Task<FavoriteSquadMember> AddAsync(FavoriteSquadMember entity)
    {
        if (!_store.TryAdd(entity.Id, entity))
            throw new InvalidOperationException($"Ya existe un favorito con id {entity.Id}.");
        return Task.FromResult(entity);
    }

    public Task<bool> UpdateAsync(FavoriteSquadMember entity)
    {
        if (!_store.ContainsKey(entity.Id))
            return Task.FromResult(false);
        _store[entity.Id] = entity;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(string id) => Task.FromResult(_store.TryRemove(id, out _));
}
