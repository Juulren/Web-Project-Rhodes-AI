using RhodesAI.Backend.Models.Domain;
using RhodesAI.Backend.Models.DTOs;

namespace RhodesAI.Backend.Services.Interfaces;

/// <summary>Consultas de sólo lectura sobre el catálogo de operadores.</summary>
public interface IOperatorReadService
{
    Task<IReadOnlyList<Operator>> GetAllAsync(CancellationToken ct = default);
    Task<Operator?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<Operator>> FilterAsync(OperatorFilterDto filter, CancellationToken ct = default);
}
