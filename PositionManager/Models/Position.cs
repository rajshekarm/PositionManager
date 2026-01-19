namespace PositionManager.Models;

public enum AssetClass
{
    Stock,
    Future,
    Option
}

public enum Side
{
    Buy,
    Sell
}

public class Position
{
    public string Symbol { get; set; } = string.Empty;
    public AssetClass AssetClass { get; set; }
    public decimal Quantity { get; set; }
    public decimal AverageCostBasis { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal RealizedPnL { get; set; }
    public List<Fill> Fills { get; set; } = new();

    // Calculated properties
    public decimal UnrealizedPnL => (CurrentPrice - AverageCostBasis) * Quantity;
    public decimal MarketValue => CurrentPrice * Math.Abs(Quantity);
    public decimal TotalPnL => RealizedPnL + UnrealizedPnL;

    // For display
    public string PositionType => Quantity >= 0 ? "LONG" : "SHORT";
}

public class OptionPosition : Position
{
    public OptionType Type { get; set; }
    public decimal Strike { get; set; }
    public DateTime Expiration { get; set; }
    public string UnderlyingSymbol { get; set; } = string.Empty;

    // Greeks
    public decimal Delta { get; set; }
    public decimal Gamma { get; set; }
    public decimal Vega { get; set; }
    public decimal Theta { get; set; }
    public decimal Rho { get; set; }
    public decimal ImpliedVolatility { get; set; }

    // Contract multiplier (usually 100 for equity options)
    public int Multiplier { get; set; } = 100;

    public override string ToString()
    {
        return $"{UnderlyingSymbol} {Strike}{Type} {Expiration:MMM dd}";
    }
}

public class FuturePosition : Position
{
    public decimal ContractSize { get; set; }
    public DateTime Expiration { get; set; }
    public decimal NotionalValue => Math.Abs(Quantity) * CurrentPrice * ContractSize;
    public decimal MarginRequired { get; set; }
}

public enum OptionType
{
    Call,
    Put
}

public class Fill
{
    public string FillId { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public Side Side { get; set; }
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
    public decimal Commission { get; set; }
    public AssetClass AssetClass { get; set; }

    // Optional: for options/futures
    public decimal? Strike { get; set; }
    public DateTime? Expiration { get; set; }
    public OptionType? OptionType { get; set; }
}