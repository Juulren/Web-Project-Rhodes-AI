using Microsoft.AspNetCore.Mvc;
using RhodesAI.Backend.Controllers;
using RhodesAI.Backend.Models.Domain;
using RhodesAI.Backend.Models.DTOs;
using RhodesAI.Backend.Repositories.Implementations;
using RhodesAI.Backend.Repositories.Interfaces;
using RhodesAI.Backend.Services.Implementations;

namespace RhodesAI.Tests;

/// <summary>
/// Contrato de Liskov: cualquier IRepository de favoritos debe pasar exactamente las mismas pruebas.
/// Cada implementación concreta hereda esta suite.
/// </summary>
public abstract class FavoriteRepositoryContract
{
    protected abstract IRepository<FavoriteSquadMember, string> CreateRepository();

    [Fact]
    public async Task AddThenGetReturnsTheSameData()
    {
        var repo = CreateRepository();
        var fav = new FavoriteSquadMember("Exusiai", "DPS antiaéreo", 1, 5, PromotionLevel.Elite2, "S3 contra drones");

        await repo.AddAsync(fav);
        var loaded = await repo.GetByIdAsync(fav.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Exusiai", loaded!.OperatorName);
        Assert.Equal("DPS antiaéreo", loaded.SquadRole);
        Assert.Equal(1, loaded.Priority);
        Assert.Equal(5, loaded.Rating);
        Assert.Equal("S3 contra drones", loaded.TacticalNotes);
    }

    [Fact]
    public async Task GetAllOrdersByPriority()
    {
        var repo = CreateRepository();
        await repo.AddAsync(new FavoriteSquadMember("Myrtle", priority: 4));
        await repo.AddAsync(new FavoriteSquadMember("Saria", priority: 1));
        await repo.AddAsync(new FavoriteSquadMember("Exusiai", priority: 2));

        var names = (await repo.GetAllAsync()).Select(f => f.OperatorName);

        Assert.Equal(new[] { "Saria", "Exusiai", "Myrtle" }, names);
    }

    [Fact]
    public async Task AddRejectsDuplicateIds()
    {
        var repo = CreateRepository();
        await repo.AddAsync(new FavoriteSquadMember("Ifrit", id: "fixed"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.AddAsync(new FavoriteSquadMember("Ifrit", id: "fixed")));
    }

    [Fact]
    public async Task UpdatePersistsChangesAndReportsUnknownEntities()
    {
        var repo = CreateRepository();
        var fav = new FavoriteSquadMember("Saria");
        await repo.AddAsync(fav);

        fav.UpdateTacticalNotes("Curar la línea frontal");
        Assert.True(await repo.UpdateAsync(fav));
        Assert.Equal("Curar la línea frontal", (await repo.GetByIdAsync(fav.Id))!.TacticalNotes);

        Assert.False(await repo.UpdateAsync(new FavoriteSquadMember("Nadie")));
    }

    [Fact]
    public async Task DeleteRemovesOnlyExistingEntities()
    {
        var repo = CreateRepository();
        var fav = new FavoriteSquadMember("Saria");
        await repo.AddAsync(fav);

        Assert.True(await repo.DeleteAsync(fav.Id));
        Assert.False(await repo.DeleteAsync(fav.Id));
        Assert.Null(await repo.GetByIdAsync(fav.Id));
        Assert.Empty(await repo.GetAllAsync());
    }

    [Fact]
    public async Task CrudServiceBehavesTheSameOverThisRepository()
    {
        var service = new FavoriteCrudService(CreateRepository());

        var created = await service.CreateAsync(new FavoriteCreateDto { OperatorName = "Exusiai" });
        await Assert.ThrowsAsync<RhodesAI.Backend.Services.Exceptions.DuplicateFavoriteException>(
            () => service.CreateAsync(new FavoriteCreateDto { OperatorName = "EXUSIAI" }));
        var updated = await service.UpdateAsync(created.Id, new FavoriteUpdateDto { Rating = 4 });

        Assert.Equal(4, updated!.Rating);
        Assert.True(await service.DeleteAsync(created.Id));
    }
}

public class InMemoryFavoriteRepositoryContractTests : FavoriteRepositoryContract
{
    protected override IRepository<FavoriteSquadMember, string> CreateRepository() => new InMemoryFavoriteRepository();
}

public sealed class JsonFileFavoriteRepositoryContractTests : FavoriteRepositoryContract, IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "rhodes-tests", Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_directory, "favorites.json");

    protected override IRepository<FavoriteSquadMember, string> CreateRepository() => new JsonFileFavoriteRepository(FilePath);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task DataSurvivesANewRepositoryInstance()
    {
        var fav = new FavoriteSquadMember("Saria", "Tanque sanador", 2, 4, PromotionLevel.Elite1, "Junto a los Guards");
        await new JsonFileFavoriteRepository(FilePath).AddAsync(fav);

        var reloaded = await new JsonFileFavoriteRepository(FilePath).GetByIdAsync(fav.Id);

        Assert.NotNull(reloaded);
        Assert.Equal(fav.Id, reloaded!.Id);
        Assert.Equal(PromotionLevel.Elite1, reloaded.PreferredPromotion);
        Assert.Equal("Junto a los Guards", reloaded.TacticalNotes);
        Assert.Equal(fav.CreatedAt, reloaded.CreatedAt);
        Assert.Equal(fav.UpdatedAt, reloaded.UpdatedAt);
    }

    [Fact]
    public async Task MissingDirectoryIsCreatedOnFirstWrite()
    {
        Assert.False(Directory.Exists(_directory));
        await CreateRepository().AddAsync(new FavoriteSquadMember("Ifrit"));
        Assert.True(File.Exists(FilePath));
    }

    [Fact]
    public async Task CorruptFileStartsEmptyAndIsKeptAsBackup()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(FilePath, "{ esto no es json");

        var repo = CreateRepository();

        Assert.Empty(await repo.GetAllAsync());
        Assert.True(File.Exists(FilePath + ".corrupt"));
        await repo.AddAsync(new FavoriteSquadMember("Ifrit"));
        Assert.Single(await new JsonFileFavoriteRepository(FilePath).GetAllAsync());
    }

    [Fact]
    public void RestoreKeepsIdentityAndAuditDates()
    {
        var created = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var updated = created.AddHours(3);

        var fav = FavoriteSquadMember.Restore("abc", "Saria", "Tanque", 2, 4, PromotionLevel.Elite1, "Nota", created, updated);

        Assert.Equal("abc", fav.Id);
        Assert.Equal(created, fav.CreatedAt);
        Assert.Equal(updated, fav.UpdatedAt);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FavoriteSquadMember.Restore("abc", "Saria", "", 9, 4, PromotionLevel.Base, "", created, updated));
    }
}

/// <summary>Códigos HTTP del CRUD que consume script.js cuando el backend está activo.</summary>
public class FavoritesControllerTests
{
    private readonly FavoritesController _controller = new(new FavoriteCrudService(new InMemoryFavoriteRepository()));

    [Fact]
    public async Task CreateReturns201ThenConflictOnDuplicate()
    {
        var created = Assert.IsType<CreatedAtActionResult>((await _controller.Create(new FavoriteCreateDto { OperatorName = "Exusiai" })).Result);
        Assert.Equal(nameof(FavoritesController.GetById), created.ActionName);

        var duplicate = (await _controller.Create(new FavoriteCreateDto { OperatorName = "exusiai" })).Result;
        Assert.IsType<ConflictObjectResult>(duplicate);
    }

    [Fact]
    public async Task CreateReturns400ForInvalidData()
    {
        Assert.IsType<BadRequestObjectResult>((await _controller.Create(new FavoriteCreateDto { OperatorName = "" })).Result);
    }

    [Fact]
    public async Task UpdateAndDeleteReturn404ForUnknownIds()
    {
        Assert.IsType<NotFoundResult>((await _controller.Update("nada", new FavoriteUpdateDto { Rating = 2 })).Result);
        Assert.IsType<NotFoundResult>(await _controller.Delete("nada"));
    }

    [Fact]
    public async Task UpdateReturns400WhenNotesAreTooLong()
    {
        var created = (FavoriteSquadMember)((CreatedAtActionResult)(await _controller.Create(new FavoriteCreateDto { OperatorName = "Saria" })).Result!).Value!;

        var result = (await _controller.Update(created.Id, new FavoriteUpdateDto { TacticalNotes = new string('x', 501) })).Result;

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task DeleteReturns204()
    {
        var created = (FavoriteSquadMember)((CreatedAtActionResult)(await _controller.Create(new FavoriteCreateDto { OperatorName = "Saria" })).Result!).Value!;
        Assert.IsType<NoContentResult>(await _controller.Delete(created.Id));
    }
}
