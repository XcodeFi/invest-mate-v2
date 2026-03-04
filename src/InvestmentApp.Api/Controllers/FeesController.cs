using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/fees")]
// [Authorize] // Temporarily disabled for testing
public class FeesController : ControllerBase
{
    private readonly IFeeCalculationService _feeService;

    public FeesController(IFeeCalculationService feeService)
    {
        _feeService = feeService;
    }

    /// <summary>
    /// Calculate trading fees for a transaction
    /// </summary>
    [HttpPost("calculate")]
    public ActionResult<FeeCalculationResponse> CalculateFees([FromBody] FeeCalculationRequest request)
    {
        // Calculate transaction amount from quantity and price
        var transactionAmount = request.Quantity * request.Price;
        if (transactionAmount <= 0)
            return BadRequest("Transaction amount must be positive");

        var moneyAmount = new Money(transactionAmount, "VND");

        // Determine security type and trade direction
        var securityType = SecurityType.Stock; // Default to Stock, can be extended later
        var isBuy = request.TradeType.ToLower() == "buy";
        var isListed = true; // Default to listed, can be extended later

        var feesSummary = _feeService.GetFeesSummary(moneyAmount, securityType, isBuy, isListed);

        // Calculate VAT on transaction fee
        var vat = _feeService.CalculateVAT(feesSummary.TransactionFee, "TransactionFee");

        // Calculate personal income tax (only for sell transactions)
        var tax = new Money(0, "VND");
        if (!isBuy)
        {
            // Personal income tax is 0.1% of transaction amount for sell orders
            tax = new Money(transactionAmount * 0.001m, "VND");
        }

        var totalFees = new Money(feesSummary.TransactionFee.Amount + vat.Amount + tax.Amount, "VND");

        var response = new FeeCalculationResponse
        {
            TransactionFee = feesSummary.TransactionFee.Amount,
            Tax = tax.Amount,
            Vat = vat.Amount,
            TotalFees = totalFees.Amount,
            Breakdown = new FeeBreakdown
            {
                TransactionFee = feesSummary.TransactionFee.Amount,
                Tax = tax.Amount,
                Vat = vat.Amount
            }
        };

        return Ok(response);
    }

    /// <summary>
    /// Calculate custody fees for holding securities
    /// </summary>
    [HttpPost("custody")]
    public ActionResult<object> CalculateCustodyFees([FromBody] CustodyFeeRequest request)
    {
        var custodyFee = _feeService.CalculateCustodyFee(request.StockCount, request.BondCount, request.Months);
        var vat = _feeService.CalculateVAT(custodyFee, "CustodyFee");
        var total = new Money(custodyFee.Amount + vat.Amount, custodyFee.Currency);

        return Ok(new
        {
            StockCount = request.StockCount,
            BondCount = request.BondCount,
            Months = request.Months,
            BaseCustodyFee = custodyFee,
            VAT = vat,
            TotalFee = total
        });
    }

    /// <summary>
    /// Calculate transfer fees for securities transfer
    /// </summary>
    [HttpPost("transfer")]
    public ActionResult<object> CalculateTransferFees([FromBody] TransferFeeRequest request)
    {
        if (request.TransferAmount <= 0)
            return BadRequest("Transfer amount must be positive");

        var transferAmount = new Money(request.TransferAmount, request.Currency ?? "VND");
        var transferFee = _feeService.CalculateTransferFee(transferAmount, request.SecurityType);
        var vat = _feeService.CalculateVAT(transferFee, "TransferFee");
        var total = new Money(transferFee.Amount + vat.Amount, transferAmount.Currency);

        return Ok(new
        {
            TransferAmount = transferAmount,
            SecurityType = request.SecurityType,
            BaseTransferFee = transferFee,
            VAT = vat,
            TotalFee = total
        });
    }

    /// <summary>
    /// Get current fee configuration
    /// </summary>
    [HttpGet("config")]
    [AllowAnonymous]
    public ActionResult<object> GetFeeConfig()
    {
        // Return basic fee info without sensitive details
        return Ok(new
        {
            TransactionFee = new
            {
                TieredPricing = new[]
                {
                    new { Range = "≤ 500 triệu VND", BuyFee = "0.15%", SellFee = "0.15%" },
                    new { Range = "500M - 1 tỷ VND", BuyFee = "0.13%", SellFee = "0.13%" },
                    new { Range = "1T - 2 tỷ VND", BuyFee = "0.11%", SellFee = "0.11%" },
                    new { Range = "2T - 5 tỷ VND", BuyFee = "0.09%", SellFee = "0.09%" },
                    new { Range = "> 5 tỷ VND", BuyFee = "0.075%", SellFee = "0.075%" }
                },
                UnlistedStockFee = "0.35%",
                ListedBondFee = "0.1%",
                UnlistedBondFee = "1,000,000 VND (fixed)"
            },
            SecuritiesTax = new
            {
                PersonalIncomeTax = "0.1% (chỉ áp dụng khi bán cổ phiếu)",
                TransferTax = new
                {
                    StockTransfer = "0.15%",
                    BondTransfer = "0.01%",
                    MinTransferFee = "50,000 VND"
                }
            },
            CustodyFee = new
            {
                MonthlyStockFee = "0.27 VND/cổ phiếu/tháng",
                MonthlyBondFee = "0.18 VND/trái phiếu/tháng"
            },
            VAT = new
            {
                Rate = "10%",
                ApplicableServices = new[] { "CustodyFee", "TransferFee", "ManagementFee" }
            },
            ManagementFee = new
            {
                AnnualFee = "2%",
                MinFee = "50,000 VND"
            },
            LastUpdated = "2024-01-01",
            Source = "VCBS Fee Schedule - Updated 2024"
        });
    }
}

public class FeeCalculationRequest
{
    public string Symbol { get; set; } = string.Empty;
    public string TradeType { get; set; } = "Buy"; // "Buy" or "Sell"
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

public class FeeCalculationResponse
{
    public decimal TransactionFee { get; set; }
    public decimal Tax { get; set; }
    public decimal Vat { get; set; }
    public decimal TotalFees { get; set; }
    public FeeBreakdown Breakdown { get; set; } = new();
}

public class FeeBreakdown
{
    public decimal TransactionFee { get; set; }
    public decimal Tax { get; set; }
    public decimal Vat { get; set; }
}

public class CustodyFeeRequest
{
    public int StockCount { get; set; }
    public int BondCount { get; set; }
    public int Months { get; set; } = 1;
}

public class TransferFeeRequest
{
    public decimal TransferAmount { get; set; }
    public string? Currency { get; set; } = "VND";
    public SecurityType SecurityType { get; set; } = SecurityType.Stock;
}