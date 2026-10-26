using RhodesAI.Backend.Models.Domain;
using RhodesAI.Backend.Models.DTOs;
using RhodesAI.Backend.Repositories.Implementations;
using RhodesAI.Backend.Services.Exceptions;
using RhodesAI.Backend.Services.Implementations;

namespace RhodesAI.Tests;

/// <summary>Pruebas del CRUD completo de favoritos (servicio + repositorio en memoria).</summary>
public class FavoriteCrudTests
{
    private readonly InMemoryFavoriteRepository _repository = new();
    private readonly FavoriteCrudService _service;

    public FavoriteCrudTests() => _service = new FavoriteCrudService(_repository);

    private static FavoriteCreateDto Dto(string name, string? notes = null) => new()
    {
        OperatorName = name,
        SquadRole = "DPS principal",
        Priority = 1,
        Rating = 5,
        PreferredPromotion = PromotionLevel.Elite2,
        TacticalNotes = notes
    };

    // ---------- CREATE ----------

    [Fact]
    public async Task Create_StoresFavoriteWithAllFields()
    {
        var created = await _service.CreateAsync(Dto("Exusiai", "Usar S3 en oleadas aéreas"));

        Assert.False(string.IsNullOrWhiteSpace(created.Id));
        Assert.Equal("Exusiai", created.OperatorName);
        Assert.Equal("DPS principal", created.SquadRole);
        Assert.Equal(1, created.Priority);
        Assert.Equal(5, created.Rating);
        Assert.Equal("Usar S3 en oleadas aéreas", created.TacticalNotes);
    }

    [Fact]
    public async Task Create_RejectsDuplicateOperatorIgnoringCase()
    {
        await _service.CreateAsync(Dto("Exusiai"));

        var ex = await Assert.ThrowsAsync<DuplicateFavoriteException>(() => _service.CreateAsync(Dto("  exusiai ")));
        Assert.Equal("exusiai", ex.OperatorName, ignoreCase: true);
        Assert.Single(await _service.GetAllAsync());
    }

    [Fact]
    public async Task Create_PropagatesDomainValidation()
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.CreateAsync(Dto("")));
    }

    // ---------- READ ----------

    [Fact]
    public async Task GetAll_ReturnsEveryFavorite()
    {
        await _service.CreateAsync(Dto("Exusiai"));
        await _service.CreateAsync(Dto("Saria"));
        await _service.CreateAsync(Dto("Eyjafjalla"));

        var all = await _service.GetAllAsync();

        Assert.Equal(3, all.Count);
        Assert.Contains(all, f => f.OperatorName == "Saria");
    }

    [Fact]
    public async Task GetById_ReturnsMatchOrNull()
    {
        var created = await _service.CreateAsync(Dto("Saria"));

        Assert.Equal("Saria", (await _service.GetByIdAsync(created.Id))?.OperatorName);
        Assert.Null(await _service.GetByIdAsync("no-existe"));
    }

    // ---------- UPDATE ----------

    [Fact]
    public async Task Update_ChangesOnlyProvidedFields()
    {
        var created = await _service.CreateAsync(Dto("Saria", "Nota original"));

        var updated = await _service.UpdateAsync(created.Id, new FavoriteUpdateDto
        {
            TacticalNotes = "Curar a los Guards de la línea frontal",
            Rating = 4
        });

        Assert.NotNull(updated);
        Assert.Equal("Curar a los Guards de la línea frontal", updated!.TacticalNotes);
        Assert.Equal(4, updated.Rating);
        Assert.Equal(1, updated.Priority);
        Assert.Equal("DPS principal", updated.SquadRole);

        var reloaded = await _service.GetByIdAsync(created.Id);
        Assert.Equal("Curar a los Guards de la línea frontal", reloaded!.TacticalNotes);
    }

    [Fact]
    public async Task Update_CanChangeRolePriorityAndPromotion()
    {
        var created = await _service.CreateAsync(Dto("Myrtle"));

        var updated = await _service.UpdateAsync(created.Id, new FavoriteUpdateDto
        {
            SquadRole = "Generador de DP",
            Priority = 2,
            PreferredPromotion = PromotionLevel.Elite1
        });

        Assert.Equal("Generador de DP", updated!.SquadRole);
        Assert.Equal(2, updated.Priority);
        Assert.Equal(PromotionLevel.Elite1, updated.PreferredPromotion);
    }

    [Fact]
    public async Task Update_ReturnsNullWhenMissing()
    {
        Assert.Null(await _service.UpdateAsync("no-existe", new FavoriteUpdateDto { Rating = 2 }));
    }

    // ---------- DELETE ----------

    [Fact]
    public async Task Delete_RemovesFavorite()
    {
        var created = await _service.CreateAsync(Dto("Exusiai"));

        Assert.True(await _service.DeleteAsync(created.Id));
        Assert.Null(await _service.GetByIdAsync(created.Id));
        Assert.Empty(await _service.GetAllAsync());
    }

    [Fact]
    public async Task Delete_ReturnsFalseWhenMissing()
    {
        Assert.False(await _service.DeleteAsync("no-existe"));
    }

    [Fact]
    public async Task Delete_AllowsReAddingSameOperator()
    {
        var created = await _service.CreateAsync(Dto("Exusiai"));
        await _service.DeleteAsync(created.Id);

        var again = await _service.CreateAsync(Dto("Exusiai"));
        Assert.NotEqual(created.Id, again.Id);
    }

    // ---------- Repositorio ----------

    [Fact]
    public async Task Repository_RejectsDuplicateIds()
    {
        var fav = new FavoriteSquadMember("Ifrit", id: "fixed-id");
        await _repository.AddAsync(fav);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repository.AddAsync(new FavoriteSquadMember("Ifrit", id: "fixed-id")));
    }

    [Fact]
    public async Task Repository_UpdateReturnsFalseForUnknownEntity()
    {
        Assert.False(await _repository.UpdateAsync(new FavoriteSquadMember("Ifrit")));
    }
}
