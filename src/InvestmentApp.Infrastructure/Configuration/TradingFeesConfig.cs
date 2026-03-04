using System.ComponentModel.DataAnnotations;

namespace InvestmentApp.Infrastructure.Configuration;

public class TradingFeesConfig
{
    public TransactionFeeConfig TransactionFee { get; set; } = new();
    public SecuritiesTaxConfig SecuritiesTax { get; set; } = new();
    public CustodyFeeConfig CustodyFee { get; set; } = new();
    public VATConfig VAT { get; set; } = new();
    public ManagementFeeConfig ManagementFee { get; set; } = new();
    public string LastUpdated { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}

public class TransactionFeeConfig
{
    public List<TransactionFeeTier> Tiers { get; set; } = new();

    [Range(0, 1)]
    public decimal UnlistedStockFee { get; set; }

    [Range(0, 1)]
    public decimal ListedBondFee { get; set; }

    [Range(0, int.MaxValue)]
    public decimal UnlistedBondFee { get; set; }
}

public class TransactionFeeTier
{
    [Range(0, long.MaxValue)]
    public decimal MinValue { get; set; }

    [Range(0, long.MaxValue)]
    public decimal MaxValue { get; set; }

    [Range(0, 1)]
    public decimal BuyFee { get; set; }

    [Range(0, 1)]
    public decimal SellFee { get; set; }
}

public class SecuritiesTaxConfig
{
    [Range(0, 1)]
    public decimal PersonalIncomeTax { get; set; }  // Thuế TNCN khi bán cổ phiếu

    public TransferTaxConfig TransferTax { get; set; } = new();
}

public class TransferTaxConfig
{
    [Range(0, 1)]
    public decimal StockTransfer { get; set; }

    [Range(0, 1)]
    public decimal BondTransfer { get; set; }

    [Range(0, int.MaxValue)]
    public decimal MinTransferFee { get; set; }
}

public class CustodyFeeConfig
{
    [Range(0, double.MaxValue)]
    public decimal MonthlyStockFee { get; set; }  // VND per stock per month

    [Range(0, double.MaxValue)]
    public decimal MonthlyBondFee { get; set; }   // VND per bond per month
}

public class VATConfig
{
    [Range(0, 1)]
    public decimal Rate { get; set; }

    public List<string> ApplicableServices { get; set; } = new();
}

public class ManagementFeeConfig
{
    [Range(0, 1)]
    public decimal AnnualFee { get; set; }

    [Range(0, int.MaxValue)]
    public decimal MinFee { get; set; }
}