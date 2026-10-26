using RhodesAI.Backend.Models.Domain;
using RhodesAI.Backend.Models.DTOs;
using RhodesAI.Backend.Repositories.Interfaces;
using RhodesAI.Backend.Services.Exceptions;
using RhodesAI.Backend.Services.Interfaces;

namespace RhodesAI.Backend.Services.Implementations;

/// <summary>
/// Reglas de negocio del CRUD de favoritos. Depende sólo de la abstracción IRepository,
/// por lo que la persistencia puede cambiar sin tocar esta clase.
/// </summary>
public class FavoriteCrudService(IRepository<FavoriteSquadMember, string> repository) : IFavoriteCrudService
{
    public async Task<FavoriteSquadMember> CreateAsync(FavoriteCreateDto dto)
    {
        var name = dto.OperatorName?.Trim() ?? string.Empty;
        var existing = await repository.GetAllAsync();
        if (name.Length > 0 && existing.Any(f => f.OperatorName.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new DuplicateFavoriteException(name);

        var favorite = new FavoriteSquadMember(name, dto.SquadRole ?? string.Empty, dto.Priority, dto.Rating,
            dto.PreferredPromotion, dto.TacticalNotes ?? string.Empty);
        return await repository.AddAsync(favorite);
    }

    public Task<IReadOnlyList<FavoriteSquadMember>> GetAllAsync() => repository.GetAllAsync();

    public Task<FavoriteSquadMember?> GetByIdAsync(string id) => repository.GetByIdAsync(id);

    public async Task<FavoriteSquadMember?> UpdateAsync(string id, FavoriteUpdateDto dto)
    {
        var favorite = await repository.GetByIdAsync(id);
        if (favorite is null)
            return null;

        if (dto.SquadRole is not null) favorite.AssignRole(dto.SquadRole);
        if (dto.Priority is int priority) favorite.ChangePriority(priority);
        if (dto.Rating is int rating) favorite.Rate(rating);
        if (dto.PreferredPromotion is PromotionLevel level) favorite.SetPreferredPromotion(level);
        if (dto.TacticalNotes is not null) favorite.UpdateTacticalNotes(dto.TacticalNotes);

        await repository.UpdateAsync(favorite);
        return favorite;
    }

    public Task<bool> DeleteAsync(string id) => repository.DeleteAsync(id);
}
