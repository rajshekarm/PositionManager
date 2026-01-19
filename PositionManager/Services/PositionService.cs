using System.Collections.Concurrent;
using PositionManager.Models;
using PositionManager.Calculators;

namespace PositionManager.Services;

public class PositionService
{
    private readonly ConcurrentDictionary<string, Position> _positions = new();
    private readonly ConcurrentDictionary<string, decimal> _currentPrices = new();
    private readonly GreeksCalculator _greeksCalculator = new();
    private readonly object _lockObject = new();

    public event EventHandler<Position>? PositionUpdated;
    public event EventHandler<PortfolioSummary>? PortfolioUpdated;

    /// <summary>
    /// Process a fill from the execution engine
    /// </summary>
    public async Task<Position> ProcessFill(Fill fill)
    {
        Position position;

        lock (_lockObject)
        {
            var key = GetPositionKey(fill);

            if (!_positions.TryGetValue(key, out position!))
            {
                // Create new position
                position = CreatePosition(fill);
                _positions[key] = position;
            }

            UpdatePositionFromFill(position, fill);
        }

        // Trigger events
        PositionUpdated?.Invoke(this, position);
        await UpdatePortfolioSummary();

        return position;
    }

    /// <summary>
    /// Update market price for a symbol
    /// </summary>
    public async Task UpdatePrice(string symbol, decimal price)
    {
        _currentPrices[symbol] = price;

        var affectedPositions = _positions.Values
            .Where(p => GetSymbolForPricing(p) == symbol)
            .ToList();

        foreach (var position in affectedPositions)
        {
            position.CurrentPrice = price;

            // Recalculate Greeks for options
            if (position is OptionPosition optionPosition)
            {
                var underlyingPrice = _currentPrices.GetValueOrDefault(optionPosition.UnderlyingSymbol, price);
                _greeksCalculator.CalculateGreeks(optionPosition, underlyingPrice);
            }

            PositionUpdated?.Invoke(this, position);
        }

        await UpdatePortfolioSummary();
    }

    /// <summary>
    /// Get all current positions
    /// </summary>
    public List<Position> GetAllPositions()
    {
        return _positions.Values.OrderBy(p => p.AssetClass).ThenBy(p => p.Symbol).ToList();
    }

    /// <summary>
    /// Get positions by asset class
    /// </summary>
    public List<Position> GetPositionsByAssetClass(AssetClass assetClass)
    {
        return _positions.Values.Where(p => p.AssetClass == assetClass).ToList();
    }

    /// <summary>
    /// Get portfolio summary with risk metrics
    /// </summary>
    public PortfolioSummary GetPortfolioSummary()
    {
        var positions = _positions.Values.ToList();

        if (!positions.Any())
        {
            return new PortfolioSummary
            {
                LastUpdated = DateTime.UtcNow
            };
        }

        var totalMarketValue = positions.Sum(p => p.MarketValue);

        var assetClassSummaries = positions
            .GroupBy(p => p.AssetClass)
            .Select(g => CreateAssetClassSummary(g, totalMarketValue))
            .ToList();

        // Aggregate options Greeks
        var optionPositions = positions.OfType<OptionPosition>().ToList();
        GreeksSummary? totalGreeks = null;

        if (optionPositions.Any())
        {
            totalGreeks = new GreeksSummary
            {
                TotalDelta = optionPositions.Sum(p => p.Delta * p.Quantity),
                TotalGamma = optionPositions.Sum(p => p.Gamma * p.Quantity),
                TotalVega = optionPositions.Sum(p => p.Vega * p.Quantity),
                TotalTheta = optionPositions.Sum(p => p.Theta * p.Quantity),
                TotalRho = optionPositions.Sum(p => p.Rho * p.Quantity)
            };
        }

        return new PortfolioSummary
        {
            TotalMarketValue = totalMarketValue,
            TotalPnL = positions.Sum(p => p.TotalPnL),
            RealizedPnL = positions.Sum(p => p.RealizedPnL),
            UnrealizedPnL = positions.Sum(p => p.UnrealizedPnL),
            TotalPositions = positions.Count,
            AssetClassBreakdown = assetClassSummaries,
            TotalGreeks = totalGreeks,
            LastUpdated = DateTime.UtcNow
        };
    }

    private Position CreatePosition(Fill fill)
    {
        Position position = fill.AssetClass switch
        {
            AssetClass.Option => new OptionPosition
            {
                Type = fill.OptionType!.Value,
                Strike = fill.Strike!.Value,
                Expiration = fill.Expiration!.Value,
                UnderlyingSymbol = ExtractUnderlyingSymbol(fill.Symbol),
                ImpliedVolatility = 0.30m // Default 30% IV
            },
            AssetClass.Future => new FuturePosition
            {
                ContractSize = 50, // Default (e.g., ES = $50 per point)
                Expiration = fill.Expiration ?? DateTime.UtcNow.AddMonths(3),
                MarginRequired = 12000 // Default
            },
            _ => new Position()
        };

        position.Symbol = fill.Symbol;
        position.AssetClass = fill.AssetClass;
        position.CurrentPrice = fill.Price;

        return position;
    }

    private void UpdatePositionFromFill(Position position, Fill fill)
    {
        var quantityChange = fill.Side == Side.Buy ? fill.Quantity : -fill.Quantity;

        if (fill.Side == Side.Buy)
        {
            // Buying - update average cost basis
            var totalCost = (position.Quantity * position.AverageCostBasis) +
                           (fill.Quantity * fill.Price);
            var totalQuantity = position.Quantity + fill.Quantity;

            position.AverageCostBasis = totalQuantity != 0 ? totalCost / totalQuantity : 0;
            position.Quantity = totalQuantity;
        }
        else // Sell
        {
            // Selling - realize P&L
            var pnlPerShare = fill.Price - position.AverageCostBasis;
            var realizedPnL = pnlPerShare * fill.Quantity - fill.Commission;

            position.RealizedPnL += realizedPnL;
            position.Quantity -= fill.Quantity;

            // If position closed, reset cost basis
            if (position.Quantity == 0)
            {
                position.AverageCostBasis = 0;
            }
        }

        position.CurrentPrice = fill.Price;
        position.Fills.Add(fill);

        // Update Greeks for options
        if (position is OptionPosition optionPosition)
        {
            var underlyingPrice = _currentPrices.GetValueOrDefault(
                optionPosition.UnderlyingSymbol,
                fill.Price
            );
            _greeksCalculator.CalculateGreeks(optionPosition, underlyingPrice);
        }
    }

    private AssetClassSummary CreateAssetClassSummary(
        IGrouping<AssetClass, Position> group,
        decimal totalPortfolioValue)
    {
        var positions = group.ToList();
        var marketValue = positions.Sum(p => p.MarketValue);

        var summary = new AssetClassSummary
        {
            AssetClass = group.Key,
            MarketValue = marketValue,
            TotalPnL = positions.Sum(p => p.TotalPnL),
            UnrealizedPnL = positions.Sum(p => p.UnrealizedPnL),
            RealizedPnL = positions.Sum(p => p.RealizedPnL),
            PositionCount = positions.Count,
            PercentOfPortfolio = totalPortfolioValue != 0
                ? (marketValue / totalPortfolioValue) * 100
                : 0
        };

        // Add futures-specific data
        if (group.Key == AssetClass.Future)
        {
            summary.TotalNotionalValue = positions
                .OfType<FuturePosition>()
                .Sum(p => p.NotionalValue);
        }

        // Add options Greeks
        if (group.Key == AssetClass.Option)
        {
            var optionPositions = positions.OfType<OptionPosition>().ToList();
            summary.Greeks = new GreeksSummary
            {
                TotalDelta = optionPositions.Sum(p => p.Delta * p.Quantity),
                TotalGamma = optionPositions.Sum(p => p.Gamma * p.Quantity),
                TotalVega = optionPositions.Sum(p => p.Vega * p.Quantity),
                TotalTheta = optionPositions.Sum(p => p.Theta * p.Quantity),
                TotalRho = optionPositions.Sum(p => p.Rho * p.Quantity)
            };
        }

        return summary;
    }

    private async Task UpdatePortfolioSummary()
    {
        var summary = GetPortfolioSummary();
        PortfolioUpdated?.Invoke(this, summary);
        await Task.CompletedTask;
    }

    private string GetPositionKey(Fill fill)
    {
        return fill.AssetClass switch
        {
            AssetClass.Option => $"{fill.Symbol}_{fill.Strike}_{fill.OptionType}_{fill.Expiration:yyyyMMdd}",
            _ => fill.Symbol
        };
    }

    private string GetSymbolForPricing(Position position)
    {
        return position is OptionPosition opt ? opt.UnderlyingSymbol : position.Symbol;
    }

    private string ExtractUnderlyingSymbol(string optionSymbol)
    {
        // Simple extraction - in production, use proper option symbol parser
        var parts = optionSymbol.Split('_');
        return parts.Length > 0 ? parts[0] : optionSymbol;
    }

    public void ClearAllPositions()
    {
        _positions.Clear();
        _currentPrices.Clear();
    }
}