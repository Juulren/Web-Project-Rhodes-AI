using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using RhodesAI.Backend.Models.Domain;
using RhodesAI.Backend.Services.Exceptions;
using RhodesAI.Backend.Services.Interfaces;

namespace RhodesAI.Backend.Services.Implementations;

/// <summary>
/// Única responsabilidad: descargar el catálogo de RhodesAPI y entregarlo como entidades del dominio.
/// La conversión del JSON vive en RhodesOperatorAdapter. Cualquier fallo remoto se traduce a UpstreamApiException.
/// </summary>
public class RhodesApiHttpClient(HttpClient http, IMemoryCache cache, ILogger<RhodesApiHttpClient> logger) : IRhodesApiClient
{
    private const string CacheKey = "rhodes-operators";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public async Task<IReadOnlyList<Operator>> GetOperatorsAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlyList<Operator>? cached) && cached is not null)
            return cached;

        using var document = await DownloadAsync(ct);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new UpstreamApiException("RhodesAPI no devolvió una lista de operadores.");

        var operators = new List<Operator>();
        int discarded = 0;
        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (RhodesOperatorAdapter.Map(element) is { } op)
                operators.Add(op);
            else
                discarded++;
        }
        if (discarded > 0)
            logger.LogInformation("RhodesAPI: {Discarded} registros descartados por datos incompletos.", discarded);

        cache.Set(CacheKey, (IReadOnlyList<Operator>)operators, CacheDuration);
        return operators;
    }

    private async Task<JsonDocument> DownloadAsync(CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync("operator", HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new UpstreamApiException($"No se pudo conectar con RhodesAPI: {ex.Message}", null, ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new UpstreamApiException("RhodesAPI tardó demasiado en responder.", null, ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw new UpstreamApiException(
                    $"RhodesAPI respondió {(int)response.StatusCode} ({response.ReasonPhrase}).", response.StatusCode);

            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            }
            catch (JsonException ex)
            {
                throw new UpstreamApiException("RhodesAPI envió una respuesta que no es JSON válido.", null, ex);
            }
        }
    }
}
