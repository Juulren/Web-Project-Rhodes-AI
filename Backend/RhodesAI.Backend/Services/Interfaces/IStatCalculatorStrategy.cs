using RhodesAI.Backend.Models.Domain;

namespace RhodesAI.Backend.Services.Interfaces;

/// <summary>
/// Fórmula intercambiable para evaluar operadores (Abierto/Cerrado): se agregan nuevas
/// implementaciones registrándolas en el contenedor, sin modificar RhodesOperatorService.
/// </summary>
public interface IStatCalculatorStrategy
{
    /// <summary>Clave estable que identifica la fórmula en las respuestas.</summary>
    string Key { get; }
    double Calculate(Operator op);
}
