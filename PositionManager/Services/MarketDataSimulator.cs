using PositionManager.Services;

namespace PositionManager.Services;

/// <summary>
/// Simulates market data feed for testing
/// In production, this would be replaced with real market data feed
/// </summary>
public class MarketDataSimulator : BackgroundService
{
    private readonly PositionService _positionService;
    private readonly ILogger<MarketDataSimulator> _logger;
    private readonly Dictionary<string, decimal> _baselinePrices = new()
    {
        ["AAPL"] = 150.00m,
        ["MSFT"] = 380.00m,
        ["GOOGL"] = 140.00m,
        ["TSLA"] = 245.00m,
        ["NVDA"] = 505.00m,
        ["ES"] = 4800.00m, // S&P 500 Future
    };

    private readonly Random _random = new();

    public MarketDataSimulator(PositionService positionService, ILogger<MarketDataSimulator> logger)
    {
        _positionService = positionService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Market Data Simulator starting...");

        // Initialize prices
        foreach (var (symbol, price) in _baselinePrices)
        {
            await _positionService.UpdatePrice(symbol, price);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SimulatePriceTicks();
                await Task.Delay(1000, stoppingToken); // Update every second
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in market data simulation");
            }
        }
    }

    private async Task SimulatePriceTicks()
    {
        foreach (var symbol in _baselinePrices.Keys.ToList())
        {
            var currentPrice = _baselinePrices[symbol];

            // Random walk: +/- 0.1% to 0.5%
            var changePercent = (decimal)(_random.NextDouble() * 0.005 - 0.0025); // -0.25% to +0.25%
            var priceChange = currentPrice * changePercent;
            var newPrice = currentPrice + priceChange;

            // Keep price positive and within reasonable bounds
            newPrice = Math.Max(newPrice, currentPrice * 0.95m);
            newPrice = Math.Min(newPrice, currentPrice * 1.05m);

            _baselinePrices[symbol] = Math.Round(newPrice, 2);

            await _positionService.UpdatePrice(symbol, newPrice);
        }
    }

    public decimal GetCurrentPrice(string symbol)
    {
        return _baselinePrices.GetValueOrDefault(symbol, 100m);
    }
}