namespace PositionManager.Models;

public class PortfolioSummary
{
    public decimal TotalMarketValue { get; set; }
    public decimal TotalPnL { get; set; }
    public decimal RealizedPnL { get; set; }
    public decimal UnrealizedPnL { get; set; }
    public int TotalPositions { get; set; }
    public DateTime LastUpdated { get; set; }

    public List<AssetClassSummary> AssetClassBreakdown { get; set; } = new();
    public GreeksSummary? TotalGreeks { get; set; }
}

public class AssetClassSummary
{
    public AssetClass AssetClass { get; set; }
    public decimal MarketValue { get; set; }
    public decimal TotalPnL { get; set; }
    public decimal UnrealizedPnL { get; set; }
    public decimal RealizedPnL { get; set; }
    public int PositionCount { get; set; }
    public decimal PercentOfPortfolio { get; set; }

    // For futures
    public decimal? TotalNotionalValue { get; set; }

    // For options
    public GreeksSummary? Greeks { get; set; }
}

public class GreeksSummary
{
    public decimal TotalDelta { get; set; }
    public decimal TotalGamma { get; set; }
    public decimal TotalVega { get; set; }
    public decimal TotalTheta { get; set; }
    public decimal TotalRho { get; set; }

    public override string ToString()
    {
        return $"Delta: {TotalDelta:N2}, Gamma: {TotalGamma:N2}, " +
               $"Vega: {TotalVega:N2}, Theta: {TotalTheta:N2}";
    }
}

public class PriceUpdate
{
    public string Symbol { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTime Timestamp { get; set; }
}

public class PositionUpdateMessage
{
    public string Symbol { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal AverageCostBasis { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal MarketValue { get; set; }
    public decimal UnrealizedPnL { get; set; }
    public decimal TotalPnL { get; set; }
    public string PositionType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}