using RhodesAI.Backend.Models.Domain;
using RhodesAI.Backend.Models.DTOs;

namespace RhodesAI.Backend.Services.Interfaces;

/// <summary>Casos de uso CRUD sobre los operadores favoritos.</summary>
public interface IFavoriteCrudService
{
    /// <exception cref="Exceptions.DuplicateFavoriteException">Si el operador ya existe.</exception>
    Task<FavoriteSquadMember> CreateAsync(FavoriteCreateDto dto);
    Task<IReadOnlyList<FavoriteSquadMember>> GetAllAsync();
    Task<FavoriteSquadMember?> GetByIdAsync(string id);
    /// <returns>El favorito actualizado o null si no existe.</returns>
    Task<FavoriteSquadMember?> UpdateAsync(string id, FavoriteUpdateDto dto);
    Task<bool> DeleteAsync(string id);
}
