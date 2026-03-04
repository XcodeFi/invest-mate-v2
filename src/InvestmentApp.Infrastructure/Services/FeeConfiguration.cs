using InvestmentApp.Application.Interfaces;
using InvestmentApp.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using AppInterfaces = InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Infrastructure.Services;

public class FeeConfiguration : IFeeConfiguration
{
    private readonly TradingFeesConfig _config;

    public FeeConfiguration(IOptions<TradingFeesConfig> config)
    {
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
    }

    public IReadOnlyList<AppInterfaces.TransactionFeeTier> TransactionFeeTiers =>
        _config.TransactionFee.Tiers.Select(t => new AppInterfaces.TransactionFeeTier
        {
            MinValue = t.MinValue,
            MaxValue = t.MaxValue,
            BuyFee = t.BuyFee,
            SellFee = t.SellFee
        }).ToList().AsReadOnly();

    public decimal UnlistedStockFee => _config.TransactionFee.UnlistedStockFee;
    public decimal ListedBondFee => _config.TransactionFee.ListedBondFee;
    public decimal UnlistedBondFee => _config.TransactionFee.UnlistedBondFee;

    public decimal PersonalIncomeTax => _config.SecuritiesTax.PersonalIncomeTax;
    public decimal StockTransferTax => _config.SecuritiesTax.TransferTax.StockTransfer;
    public decimal BondTransferTax => _config.SecuritiesTax.TransferTax.BondTransfer;
    public decimal MinTransferFee => _config.SecuritiesTax.TransferTax.MinTransferFee;

    public decimal MonthlyStockCustodyFee => _config.CustodyFee.MonthlyStockFee;
    public decimal MonthlyBondCustodyFee => _config.CustodyFee.MonthlyBondFee;

    public decimal VATRate => _config.VAT.Rate;
    public IReadOnlyList<string> VATApplicableServices => _config.VAT.ApplicableServices.AsReadOnly();

    public decimal AnnualManagementFee => _config.ManagementFee.AnnualFee;
    public decimal MinManagementFee => _config.ManagementFee.MinFee;

    public string LastUpdated => _config.LastUpdated;
    public string Source => _config.Source;
}