using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using RhodesAI.Backend.Controllers;
using RhodesAI.Backend.Models.Domain;
using RhodesAI.Backend.Models.DTOs;
using RhodesAI.Backend.Services.Exceptions;
using RhodesAI.Backend.Services.Implementations;
using RhodesAI.Backend.Services.Interfaces;

namespace RhodesAI.Tests;

/// <summary>
/// Pruebas de la capa de datos de operadores: adaptador del JSON de RhodesAPI, cliente HTTP con manejo de fallos
/// y reglas de búsqueda del servicio.
/// </summary>
public class OperatorServiceTests
{
    // Muestra real de RhodesAPI: block "block: 2", coste "09" y afiliación en arreglo.
    private const string SariaJson = """
    {
      "name": "Saria", "rarity": 6,
      "class": ["Defender", "Healing", "Guardian"],
      "affiliation": ["Columbia", "Rhine Lab"],
      "description": "Saria, former Rhine Lab Defense Department head.",
      "biography": "Saria is a former head of the Rhine Lab Defense Department.",
      "art": [
        { "name": "Base", "link": "https://ak.gamepress.gg/sites/default/files/2019-10/char_202_demkni_1.png" },
        { "name": "E2", "link": "https://ak.gamepress.gg/sites/default/files/2019-10/char_202_demkni_2.png" }
      ],
      "statistics": {
        "base": { "hp": "1309", "atk": "200", "def": "248", "resist": "18", "deploy": "70", "cost": "09", "interval": "1.2", "block": "block: 2" },
        "e0max": { "hp": "1687", "atk": "260", "def": "325" },
        "e1max": { "hp": "2124", "atk": "325", "def": "414" },
        "e2max": { "hp": "2630", "atk": "390", "def": "575" }
      }
    }
    """;

    private const string MyrtleJson = """
    {
      "name": "Myrtle", "rarity": 4, "class": ["Vanguard", "Standard Bearer"], "affiliation": "Rhodes Island",
      "description": "", "biography": "A Liberi from Laterano.",
      "art": [{ "name": "Base", "link": "https://ak.gamepress.gg/sites/default/files/2019-10/char_151_myrtle_1.png" }],
      "statistics": {
        "base": { "hp": "800", "atk": "200", "def": "150", "resist": "0", "deploy": "70", "cost": "10", "interval": "1.3", "block": "1" },
        "e0max": { "hp": "1000", "atk": "260", "def": "180", "block": "1" },
        "e1max": { "hp": "1250", "atk": "320", "def": "220", "block": "1" }
      }
    }
    """;

    private const string UlpianusJson = """
    {
      "name": "Ulpianus", "rarity": 6, "class": ["Guard", "Primal"], "affiliation": ["Aegir", "Abyssal"],
      "description": "An Abyssal Hunter.", "biography": "",
      "art": [{ "name": "Base", "link": "https://ak.gamepress.gg/sites/default/files/2024-01/%E7%AB%8B%E7%BB%98_1.png" }],
      "statistics": {
        "base": { "hp": "", "atk": "", "def": "", "resist": "", "deploy": "", "cost": "", "interval": "", "block": "" },
        "e0max": { "hp": "1900", "atk": "500", "def": "300", "block": "1" },
        "e2max": { "hp": "", "atk": "", "def": "", "block": "" }
      }
    }
    """;

    private const string ReserveJson = """
    { "name": "Reserve Operator - Defender", "rarity": 0, "class": ["Defender"], "affiliation": "Rhodes Island",
      "statistics": { "base": { "hp": "500", "atk": "100", "def": "200" }, "e0max": { "hp": "600", "atk": "120", "def": "220" } } }
    """;

    private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement.Clone();

    // =====================================================================
    // Adaptador (normalización del JSON de RhodesAPI)
    // =====================================================================

    [Fact]
    public void Adapter_ParsesNoisyNumbersLikeBlockPrefixAndLeadingZero()
    {
        var op = RhodesOperatorAdapter.Map(Json(SariaJson))!;

        Assert.Equal(2, op.Stats.Block);
        Assert.Equal(9, op.Stats.Cost);
        Assert.Equal(18, op.Stats.Res);
        Assert.Equal(1.2, op.Stats.AttackInterval);
        Assert.Equal(2630, op.Stats.Hp);
    }

    [Fact]
    public void Adapter_UsesFirstAffiliationAndLastClassAsArchetype()
    {
        var op = RhodesOperatorAdapter.Map(Json(SariaJson))!;

        Assert.Equal(OperatorClass.Defender, op.Class);
        Assert.Equal("Columbia", op.Affiliation);
        Assert.Equal("Guardian", op.Archetype);
        Assert.IsType<CombatOperator>(op);
    }

    [Fact]
    public void Adapter_FallsBackToEliteOneWhenEliteTwoIsMissing()
    {
        var op = RhodesOperatorAdapter.Map(Json(MyrtleJson))!;

        Assert.Equal(1250, op.Stats.Hp);
        Assert.False(op.StatsByPromotion.ContainsKey(PromotionLevel.Elite2));
    }

    [Fact]
    public void Adapter_SkipsEmptyTiersAndDefaultsInterval()
    {
        var op = RhodesOperatorAdapter.Map(Json(UlpianusJson))!;

        Assert.Equal(1900, op.Stats.Hp);
        Assert.Equal(1.0, op.Stats.AttackInterval);
        Assert.Single(op.StatsByPromotion);
    }

    [Fact]
    public void Adapter_ReturnsNullInsteadOfThrowingForInvalidRecords()
    {
        Assert.Null(RhodesOperatorAdapter.Map(Json(ReserveJson)));
        Assert.Null(RhodesOperatorAdapter.Map(Json("""{ "name": "X", "rarity": 5, "class": ["Pilot"], "statistics": {} }""")));
        Assert.Null(RhodesOperatorAdapter.Map(Json("""{ "name": "", "rarity": 5, "class": ["Guard"] }""")));
        Assert.Null(RhodesOperatorAdapter.Map(Json("[]")));
    }

    [Fact]
    public void Adapter_DescriptionFallsBackToBiography()
    {
        var op = RhodesOperatorAdapter.Map(Json(MyrtleJson))!;
        Assert.Equal("A Liberi from Laterano.", op.Description);
    }

    [Fact]
    public void Adapter_RewritesDeadGamepressArtToPortraitRepository()
    {
        var saria = RhodesOperatorAdapter.Map(Json(SariaJson))!;
        var myrtle = RhodesOperatorAdapter.Map(Json(MyrtleJson))!;

        Assert.Equal(RhodesOperatorAdapter.PortraitBaseUrl + "char_202_demkni_2.png", saria.ArtUrl);
        Assert.Equal(RhodesOperatorAdapter.PortraitBaseUrl + "char_151_myrtle_2.png", myrtle.ArtUrl);
    }

    [Fact]
    public void Adapter_LeavesArtEmptyWhenLinkHasNoCharacterId()
    {
        var op = RhodesOperatorAdapter.Map(Json(UlpianusJson))!;
        Assert.Equal(string.Empty, op.ArtUrl);
    }

    // =====================================================================
    // Cliente HTTP (fallos de RhodesAPI)
    // =====================================================================

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(respond(request));
        }
    }

    private static (RhodesApiHttpClient Client, FakeHandler Handler) ClientReturning(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new FakeHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://rhodes.test/api/") };
        var client = new RhodesApiHttpClient(http, new MemoryCache(new MemoryCacheOptions()), NullLogger<RhodesApiHttpClient>.Instance);
        return (client, handler);
    }

    private static HttpResponseMessage JsonResponse(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task Client_WrapsHttpErrorsInUpstreamApiException(HttpStatusCode status)
    {
        var (client, _) = ClientReturning(_ => JsonResponse("""{ "error": "boom" }""", status));

        var ex = await Assert.ThrowsAsync<UpstreamApiException>(() => client.GetOperatorsAsync());
        Assert.Equal(status, ex.StatusCode);
        Assert.Contains(((int)status).ToString(), ex.Message);
    }

    [Fact]
    public async Task Client_WrapsInvalidJsonInUpstreamApiException()
    {
        var (client, _) = ClientReturning(_ => JsonResponse("<html>caído</html>"));
        await Assert.ThrowsAsync<UpstreamApiException>(() => client.GetOperatorsAsync());
    }

    [Fact]
    public async Task Client_WrapsNetworkFailuresInUpstreamApiException()
    {
        var (client, _) = ClientReturning(_ => throw new HttpRequestException("No such host is known."));
        var ex = await Assert.ThrowsAsync<UpstreamApiException>(() => client.GetOperatorsAsync());
        Assert.Null(ex.StatusCode);
    }

    [Fact]
    public async Task Client_DiscardsInvalidRecordsAndCachesTheCatalog()
    {
        var (client, handler) = ClientReturning(_ => JsonResponse($"[{SariaJson}, {ReserveJson}, {MyrtleJson}]"));

        var first = await client.GetOperatorsAsync();
        var second = await client.GetOperatorsAsync();

        Assert.Equal(new[] { "Saria", "Myrtle" }, first.Select(o => o.Name));
        Assert.Same(first, second);
        Assert.Equal(1, handler.Calls);
    }

    // =====================================================================
    // Servicio y controlador
    // =====================================================================

    private sealed class FakeApiClient(Func<IReadOnlyList<Operator>> source) : IRhodesApiClient
    {
        public Task<IReadOnlyList<Operator>> GetOperatorsAsync(CancellationToken ct = default) => Task.FromResult(source());
    }

    private static RhodesOperatorService ServiceWith(params Operator[] operators) =>
        new(new FakeApiClient(() => operators), [new EliteTwoStatCalculator()]);

    private static Operator Op(string name, int rarity, OperatorClass cls, string affiliation = "", string description = "") =>
        new CombatOperator(name, rarity, cls,
            new Dictionary<PromotionLevel, OperatorStats> { [PromotionLevel.Base] = new(1000, 300, 200) },
            PromotionLevel.Base, affiliation, description);

    [Fact]
    public async Task Service_FilterCombinesSearchClassAndRarity()
    {
        var service = ServiceWith(
            Op("Ch'en", 6, OperatorClass.Guard, "Great Lungmen"),
            Op("Hoshiguma", 6, OperatorClass.Defender, "Great Lungmen"),
            Op("Swire", 5, OperatorClass.Guard, "Great Lungmen"),
            Op("Exusiai", 6, OperatorClass.Sniper, "Penguin Logistics"));

        var result = await service.FilterAsync(new OperatorFilterDto { Search = "lungmen", Class = OperatorClass.Guard, Rarity = 6 });

        Assert.Equal("Ch'en", Assert.Single(result).Name);
    }

    [Fact]
    public async Task Service_SearchMatchesDescriptionKeywords()
    {
        var service = ServiceWith(Op("Ptilopsis", 5, OperatorClass.Medic, description: "Heals allies over an area"),
            Op("Ifrit", 6, OperatorClass.Caster, description: "Burns everything"));

        var result = await service.FilterAsync(new OperatorFilterDto { Search = "HEALS" });

        Assert.Equal("Ptilopsis", Assert.Single(result).Name);
    }

    [Fact]
    public async Task Service_GetByNameIgnoresCaseAndAcceptsSlug()
    {
        var service = ServiceWith(Op("Silver Ash", 6, OperatorClass.Guard));

        Assert.NotNull(await service.GetByNameAsync("SILVER ASH"));
        Assert.NotNull(await service.GetByNameAsync("silver-ash"));
        Assert.Null(await service.GetByNameAsync("Nobody"));
    }

    [Fact]
    public async Task Controller_Returns503WithProblemDetailsWhenRhodesApiFails()
    {
        var failing = new RhodesOperatorService(
            new FakeApiClient(() => throw new UpstreamApiException("RhodesAPI respondió 500.", HttpStatusCode.InternalServerError)),
            []);
        var controller = new OperatorsController(failing, failing);

        var result = Assert.IsType<ObjectResult>(await controller.GetAll(CancellationToken.None));

        Assert.Equal(503, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Contains("500", problem.Detail);
    }
}
