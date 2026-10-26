using Microsoft.AspNetCore.Mvc;
using RhodesAI.Backend.Models.Domain;
using RhodesAI.Backend.Models.DTOs;
using RhodesAI.Backend.Services.Exceptions;
using RhodesAI.Backend.Services.Interfaces;

namespace RhodesAI.Backend.Controllers;

/// <summary>CRUD RESTful de operadores favoritos y sus notas tácticas.</summary>
[ApiController]
[Route("api/favorites")]
public class FavoritesController(IFavoriteCrudService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<FavoriteSquadMember>>> GetAll() => Ok(await service.GetAllAsync());

    [HttpGet("{id}")]
    public async Task<ActionResult<FavoriteSquadMember>> GetById(string id)
    {
        var favorite = await service.GetByIdAsync(id);
        return favorite is null ? NotFound() : Ok(favorite);
    }

    [HttpPost]
    public async Task<ActionResult<FavoriteSquadMember>> Create(FavoriteCreateDto dto)
    {
        try
        {
            var created = await service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (DuplicateFavoriteException ex)
        {
            return Conflict(new ProblemDetails { Title = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<FavoriteSquadMember>> Update(string id, FavoriteUpdateDto dto)
    {
        try
        {
            var updated = await service.UpdateAsync(id, dto);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id) =>
        await service.DeleteAsync(id) ? NoContent() : NotFound();
}
