using Microsoft.AspNetCore.Mvc;
using RhodesAI.Backend.Models.DTOs;
using RhodesAI.Backend.Services.Interfaces;

namespace RhodesAI.Backend.Controllers;

/// <summary>Lectura del catálogo de operadores, filtros y estadísticas dinámicas.</summary>
[ApiController]
[Route("api/operators")]
public class OperatorsController(IOperatorReadService readService, IOperatorStatsService statsService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IEnumerable<OperatorDto>>(StatusCodes.Status200OK)]
    public Task<IActionResult> GetAll(CancellationToken ct) =>
        Upstream(async () => Ok((await readService.GetAllAsync(ct)).Select(OperatorDto.From)));

    [HttpGet("filter")]
    [ProducesResponseType<IEnumerable<OperatorDto>>(StatusCodes.Status200OK)]
    public Task<IActionResult> Filter([FromQuery] OperatorFilterDto filter, CancellationToken ct) =>
        Upstream(async () => Ok((await readService.FilterAsync(filter, ct)).Select(OperatorDto.From)));

    [HttpGet("stats")]
    [ProducesResponseType<DynamicStatsSummaryDto>(StatusCodes.Status200OK)]
    public Task<IActionResult> Stats([FromQuery] OperatorFilterDto filter, CancellationToken ct) =>
        Upstream(async () => Ok(await statsService.GetSummaryAsync(filter, ct)));

    [HttpGet("{name}")]
    [ProducesResponseType<OperatorDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetByName(string name, CancellationToken ct) =>
        Upstream(async () =>
        {
            var op = await readService.GetByNameAsync(name, ct);
            return op is null
                ? NotFound(new ProblemDetails { Title = $"No existe un operador llamado {name}." })
                : Ok(OperatorDto.From(op));
        });

    /// <summary>Traduce las caídas de RhodesAPI a un 503 explícito para el cliente.</summary>
    private async Task<IActionResult> Upstream(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ProblemDetails { Title = "RhodesAPI no respondió.", Detail = ex.Message });
        }
    }
}
