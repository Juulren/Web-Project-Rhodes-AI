using RhodesAI.Backend.Models.Domain;

namespace RhodesAI.Backend.Services.Interfaces;

/// <summary>Fuente de datos de operadores (RhodesAPI u otra equivalente).</summary>
public interface IRhodesApiClient
{
    Task<IReadOnlyList<Operator>> GetOperatorsAsync(CancellationToken ct = default);
}
