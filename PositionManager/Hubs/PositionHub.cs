using Microsoft.AspNetCore.SignalR;
using PositionManager.Models;

namespace PositionManager.Hubs;

public class PositionHub : Hub
{
    public async Task SubscribeToPositions()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "PositionUpdates");
    }

    public async Task UnsubscribeFromPositions()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "PositionUpdates");
    }

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("Connected", $"Connection ID: {Context.ConnectionId}");
        await base.OnConnectedAsync();
    }
}

public class PositionHubService
{
    private readonly IHubContext<PositionHub> _hubContext;
    private readonly ILogger<PositionHubService> _logger;

    public PositionHubService(IHubContext<PositionHub> hubContext, ILogger<PositionHubService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task BroadcastPositionUpdate(Position position)
    {
        var message = new PositionUpdateMessage
        {
            Symbol = position.Symbol,
            Quantity = position.Quantity,
            AverageCostBasis = position.AverageCostBasis,
            CurrentPrice = position.CurrentPrice,
            MarketValue = position.MarketValue,
            UnrealizedPnL = position.UnrealizedPnL,
            TotalPnL = position.TotalPnL,
            PositionType = position.PositionType,
            Timestamp = DateTime.UtcNow
        };

        await _hubContext.Clients.Group("PositionUpdates")
            .SendAsync("PositionUpdate", message);
    }

    public async Task BroadcastPortfolioUpdate(PortfolioSummary summary)
    {
        await _hubContext.Clients.Group("PositionUpdates")
            .SendAsync("PortfolioUpdate", summary);

        _logger.LogDebug("Portfolio update broadcasted: Total P&L = {TotalPnL:C}", summary.TotalPnL);
    }
}