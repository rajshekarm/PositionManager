using PositionManager.Services;
using PositionManager.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add SignalR
builder.Services.AddSignalR();

// Register our services as singletons (in-memory storage)
builder.Services.AddSingleton<PositionService>();
builder.Services.AddSingleton<PositionHubService>();
builder.Services.AddHostedService<MarketDataSimulator>();

// Add CORS for development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Wire up events for real-time updates
var positionService = app.Services.GetRequiredService<PositionService>();
var hubService = app.Services.GetRequiredService<PositionHubService>();

positionService.PositionUpdated += async (sender, position) =>
{
    await hubService.BroadcastPositionUpdate(position);
};

positionService.PortfolioUpdated += async (sender, summary) =>
{
    await hubService.BroadcastPortfolioUpdate(summary);
};

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHub<PositionHub>("/hubs/positions");

// Startup message
app.Logger.LogInformation("=".PadRight(80, '='));
app.Logger.LogInformation("Position Management & Analytics System");
app.Logger.LogInformation("=".PadRight(80, '='));
app.Logger.LogInformation("API: https://localhost:{Port}/swagger", app.Urls.FirstOrDefault()?.Split(':').LastOrDefault());
app.Logger.LogInformation("SignalR Hub: /hubs/positions");
app.Logger.LogInformation("=".PadRight(80, '='));

app.Run();