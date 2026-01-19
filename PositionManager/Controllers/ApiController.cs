using Microsoft.AspNetCore.Mvc;
using PositionManager.Models;
using PositionManager.Services;

namespace PositionManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PositionsController : ControllerBase
{
    private readonly PositionService _positionService;
    private readonly ILogger<PositionsController> _logger;

    public PositionsController(PositionService positionService, ILogger<PositionsController> logger)
    {
        _positionService = positionService;
        _logger = logger;
    }

    [HttpGet]
    public ActionResult<List<Position>> GetAllPositions()
    {
        var positions = _positionService.GetAllPositions();
        return Ok(positions);
    }

    [HttpGet("asset-class/{assetClass}")]
    public ActionResult<List<Position>> GetPositionsByAssetClass(AssetClass assetClass)
    {
        var positions = _positionService.GetPositionsByAssetClass(assetClass);
        return Ok(positions);
    }

    [HttpGet("summary")]
    public ActionResult<PortfolioSummary> GetPortfolioSummary()
    {
        var summary = _positionService.GetPortfolioSummary();
        return Ok(summary);
    }

    [HttpPost("fill")]
    public async Task<ActionResult<Position>> ProcessFill([FromBody] Fill fill)
    {
        try
        {
            fill.Timestamp = DateTime.UtcNow;
            var position = await _positionService.ProcessFill(fill);

            _logger.LogInformation(
                "Fill processed: {Symbol} {Side} {Quantity} @ {Price}",
                fill.Symbol, fill.Side, fill.Quantity, fill.Price
            );

            return Ok(position);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing fill");
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("price")]
    public async Task<IActionResult> UpdatePrice([FromBody] PriceUpdate priceUpdate)
    {
        try
        {
            await _positionService.UpdatePrice(priceUpdate.Symbol, priceUpdate.Price);

            _logger.LogDebug("Price updated: {Symbol} = {Price}", priceUpdate.Symbol, priceUpdate.Price);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating price");
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("clear")]
    public IActionResult ClearAllPositions()
    {
        _positionService.ClearAllPositions();
        _logger.LogWarning("All positions cleared");
        return Ok();
    }
}

[ApiController]
[Route("api/[controller]")]
public class RiskController : ControllerBase
{
    private readonly PositionService _positionService;

    public RiskController(PositionService positionService)
    {
        _positionService = positionService;
    }

    [HttpGet("greeks")]
    public ActionResult<GreeksSummary> GetTotalGreeks()
    {
        var summary = _positionService.GetPortfolioSummary();

        if (summary.TotalGreeks == null)
        {
            return NotFound("No option positions found");
        }

        return Ok(summary.TotalGreeks);
    }

    [HttpGet("breakdown")]
    public ActionResult<List<AssetClassSummary>> GetAssetClassBreakdown()
    {
        var summary = _positionService.GetPortfolioSummary();
        return Ok(summary.AssetClassBreakdown);
    }
}