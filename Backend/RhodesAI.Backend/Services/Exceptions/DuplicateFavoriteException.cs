namespace RhodesAI.Backend.Services.Exceptions;

/// <summary>Se lanza al intentar registrar un operador que ya está en favoritos.</summary>
public class DuplicateFavoriteException(string operatorName)
    : InvalidOperationException($"{operatorName} ya está en tu escuadrón de favoritos.")
{
    public string OperatorName { get; } = operatorName;
}
