using RhodesAI.Backend.Models.Base;

namespace RhodesAI.Backend.Repositories.Interfaces;

/// <summary>
/// Contrato genérico de persistencia CRUD. Cualquier implementación (memoria, JSON, base de datos)
/// debe poder sustituirse sin alterar a sus consumidores (Liskov).
/// </summary>
public interface IRepository<TEntity, in TKey> where TEntity : BaseEntity
{
    Task<IReadOnlyList<TEntity>> GetAllAsync();
    Task<TEntity?> GetByIdAsync(TKey id);
    Task<TEntity> AddAsync(TEntity entity);
    Task<bool> UpdateAsync(TEntity entity);
    Task<bool> DeleteAsync(TKey id);
}
