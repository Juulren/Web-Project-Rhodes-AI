using System.Net;

namespace RhodesAI.Backend.Services.Exceptions;

/// <summary>
/// RhodesAPI falló (HTTP distinto de 2xx, red caída, tiempo agotado o JSON inválido).
/// Hereda de HttpRequestException para que cualquier manejador existente la siga capturando.
/// </summary>
public class UpstreamApiException(string message, HttpStatusCode? statusCode = null, Exception? inner = null)
    : HttpRequestException(message, inner, statusCode);
