using System.Text.Json.Serialization;
using RhodesAI.Backend.Models.Domain;
using RhodesAI.Backend.Repositories.Implementations;
using RhodesAI.Backend.Repositories.Interfaces;
using RhodesAI.Backend.Services.Implementations;
using RhodesAI.Backend.Services.Interfaces;

const string FrontendCors = "RhodesFrontend";

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();

// CORS: el frontend se sirve desde GitHub Pages o desde un servidor estático local.
builder.Services.AddCors(options => options.AddPolicy(FrontendCors, policy => policy
    .SetIsOriginAllowed(origin =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && (uri.IsLoopback || uri.Host.Equals("juulren.github.io", StringComparison.OrdinalIgnoreCase)))
    .AllowAnyHeader()
    .AllowAnyMethod()));

// Inversión de dependencias: los controladores sólo conocen abstracciones.
builder.Services.AddHttpClient<IRhodesApiClient, RhodesApiHttpClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["RhodesApi:BaseUrl"] ?? "https://rhodesapi.up.railway.app/api/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddSingleton<IStatCalculatorStrategy, EliteTwoStatCalculator>();
builder.Services.AddScoped<RhodesOperatorService>();
builder.Services.AddScoped<IOperatorReadService>(sp => sp.GetRequiredService<RhodesOperatorService>());
builder.Services.AddScoped<IOperatorStatsService>(sp => sp.GetRequiredService<RhodesOperatorService>());

// El repositorio es Singleton para conservar los favoritos entre peticiones.
builder.Services.AddSingleton<IRepository<FavoriteSquadMember, string>, InMemoryFavoriteRepository>();
builder.Services.AddScoped<IFavoriteCrudService, FavoriteCrudService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(FrontendCors);
app.MapControllers();

app.Run();
