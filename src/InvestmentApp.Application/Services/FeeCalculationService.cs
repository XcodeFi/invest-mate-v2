using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.ValueObjects;

namespace InvestmentApp.Application.Services;

public class FeeCalculationService : IFeeCalculationService
{
    private readonly IFeeConfiguration _feeConfig;

    public FeeCalculationService(IFeeConfiguration feeConfig)
    {
        _feeConfig = feeConfig ?? throw new ArgumentNullException(nameof(feeConfig));
    }

    public Money CalculateTransactionFee(Money transactionAmount, bool isBuy, bool isListed = true, SecurityType securityType = SecurityType.Stock)
    {
        decimal feeRate = 0;

        // Special cases for unlisted securities
        if (!isListed)
        {
            if (securityType == SecurityType.Stock)
            {
                feeRate = _feeConfig.UnlistedStockFee;
            }
            else if (securityType == SecurityType.Bond)
            {
                // For unlisted bonds, it's a fixed fee, not percentage
                return new Money(_feeConfig.UnlistedBondFee, transactionAmount.Currency);
            }
        }
        else
        {
            // Listed securities - use tiered pricing
            if (securityType == SecurityType.Bond)
            {
                feeRate = _feeConfig.ListedBondFee;
            }
            else
            {
                // Find appropriate tier for stocks
                var tier = _feeConfig.TransactionFeeTiers
                    .FirstOrDefault(t => transactionAmount.Amount >= t.MinValue && transactionAmount.Amount < t.MaxValue);

                if (tier != null)
                {
                    feeRate = isBuy ? tier.BuyFee : tier.SellFee;
                }
                else
                {
                    // Fallback to highest tier
                    var highestTier = _feeConfig.TransactionFeeTiers.OrderByDescending(t => t.MaxValue).FirstOrDefault();
                    feeRate = highestTier != null ? (isBuy ? highestTier.BuyFee : highestTier.SellFee) : 0;
                }
            }
        }

        var feeAmount = transactionAmount.Amount * feeRate;
        return new Money(feeAmount, transactionAmount.Currency);
    }

    public Money CalculateSecuritiesTax(Money transactionAmount, SecurityType securityType, bool isBuy)
    {
        // Personal Income Tax only applies to selling stocks
        if (!isBuy && securityType == SecurityType.Stock)
        {
            var taxAmount = transactionAmount.Amount * _feeConfig.PersonalIncomeTax;
            return new Money(taxAmount, transactionAmount.Currency);
        }

        return new Money(0, transactionAmount.Currency);
    }

    public Money CalculateTransferFee(Money transferAmount, SecurityType securityType)
    {
        decimal transferRate = 0;

        if (securityType == SecurityType.Stock)
        {
            transferRate = _feeConfig.StockTransferTax;
        }
        else if (securityType == SecurityType.Bond)
        {
            transferRate = _feeConfig.BondTransferTax;
        }

        var feeAmount = transferAmount.Amount * transferRate;
        feeAmount = Math.Max(feeAmount, _feeConfig.MinTransferFee);

        return new Money(feeAmount, transferAmount.Currency);
    }

    public Money CalculateAnnualManagementFee(Money portfolioValue)
    {
        var feeAmount = portfolioValue.Amount * _feeConfig.AnnualManagementFee;
        feeAmount = Math.Max(feeAmount, _feeConfig.MinManagementFee);

        return new Money(feeAmount, portfolioValue.Currency);
    }
    public Money CalculateCustodyFee(int stockCount, int bondCount, int months = 1)
    {
        var stockFee = stockCount * _feeConfig.MonthlyStockCustodyFee * months;
        var bondFee = bondCount * _feeConfig.MonthlyBondCustodyFee * months;
        var totalFee = stockFee + bondFee;

        return new Money(totalFee, "VND");
    }

    public Money CalculateVAT(Money baseAmount, string serviceType)
    {
        if (_feeConfig.VATApplicableServices.Contains(serviceType))
        {
            var vatAmount = baseAmount.Amount * _feeConfig.VATRate;
            return new Money(vatAmount, baseAmount.Currency);
        }

        return new Money(0, baseAmount.Currency);
    }
    public TradingFeesSummary GetFeesSummary(Money transactionAmount, SecurityType securityType, bool isBuy, bool isListed = true)
    {
        var transactionFee = CalculateTransactionFee(transactionAmount, isBuy, isListed, securityType);
        var tax = CalculateSecuritiesTax(transactionAmount, securityType, isBuy);

        var subtotalFees = new Money(transactionFee.Amount + tax.Amount, transactionAmount.Currency);

        // Calculate VAT on applicable fees
        var vatOnTransactionFee = CalculateVAT(transactionFee, "TransactionFee");
        var vatOnTax = CalculateVAT(tax, "SecuritiesTax");

        var totalFees = new Money(subtotalFees.Amount + vatOnTransactionFee.Amount + vatOnTax.Amount, transactionAmount.Currency);

        return new TradingFeesSummary
        {
            TransactionAmount = transactionAmount,
            TransactionFee = transactionFee,
            Tax = tax,
            SubtotalFees = subtotalFees,
            VAT = new Money(vatOnTransactionFee.Amount + vatOnTax.Amount, transactionAmount.Currency),
            TotalFees = totalFees,
            NetAmount = new Money(transactionAmount.Amount - totalFees.Amount, transactionAmount.Currency),
            FeeConfigSource = _feeConfig.Source,
            LastUpdated = _feeConfig.LastUpdated,
            SecurityType = securityType,
            IsListed = isListed,
            IsBuy = isBuy
        };
    }
}