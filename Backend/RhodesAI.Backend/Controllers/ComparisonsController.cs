using Microsoft.AspNetCore.Mvc;
using RhodesAI.Backend.Models.DTOs;
using RhodesAI.Backend.Services.Interfaces;

namespace RhodesAI.Backend.Controllers;

/// <summary>Comparación cara a cara de dos operadores.</summary>
[ApiController]
[Route("api/comparisons")]
public class ComparisonsController(IOperatorStatsService statsService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ComparisonResultDto>> Compare(ComparisonRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.OperatorA) || string.IsNullOrWhiteSpace(request.OperatorB))
            return BadRequest(new ProblemDetails { Title = "Indica el nombre de los dos operadores." });

        try
        {
            var result = await statsService.CompareAsync(request.OperatorA, request.OperatorB, ct);
            return result is null
                ? NotFound(new ProblemDetails { Title = "Alguno de los operadores no existe." })
                : Ok(result);
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ProblemDetails { Title = "RhodesAPI no respondió.", Detail = ex.Message });
        }
    }
}
