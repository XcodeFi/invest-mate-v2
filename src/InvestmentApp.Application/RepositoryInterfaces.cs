using MediatR;
using System.Threading;
using System.Threading.Tasks;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.ValueObjects;

namespace InvestmentApp.Application.Interfaces;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}

public interface IPortfolioRepository : IRepository<Portfolio>
{
    Task<IEnumerable<Portfolio>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<Portfolio?> GetByIdWithTradesAsync(string id, CancellationToken cancellationToken = default);
}

public interface ITradeRepository : IRepository<Trade>
{
    Task<IEnumerable<Trade>> GetByPortfolioIdAsync(string portfolioId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Trade>> GetByPortfolioIdAndSymbolAsync(string portfolioId, string symbol, CancellationToken cancellationToken = default);
}

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
}

public interface IStockPriceRepository : IRepository<StockPrice>
{
    Task<StockPrice?> GetBySymbolAndDateAsync(string symbol, DateTime date, CancellationToken cancellationToken = default);
    Task<IEnumerable<StockPrice>> GetBySymbolAsync(string symbol, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<IEnumerable<StockPrice>> GetLatestPricesAsync(IEnumerable<string> symbols, CancellationToken cancellationToken = default);
    Task UpsertAsync(StockPrice stockPrice, CancellationToken cancellationToken = default);
}

public interface IMarketIndexRepository : IRepository<MarketIndex>
{
    Task<MarketIndex?> GetBySymbolAndDateAsync(string indexSymbol, DateTime date, CancellationToken cancellationToken = default);
    Task<IEnumerable<MarketIndex>> GetBySymbolAsync(string indexSymbol, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task UpsertAsync(MarketIndex marketIndex, CancellationToken cancellationToken = default);
}

public interface ICapitalFlowRepository : IRepository<CapitalFlow>
{
    Task<IEnumerable<CapitalFlow>> GetByPortfolioIdAsync(string portfolioId, CancellationToken cancellationToken = default);
    Task<IEnumerable<CapitalFlow>> GetByPortfolioIdAsync(string portfolioId, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<decimal> GetTotalFlowByPortfolioIdAsync(string portfolioId, CancellationToken cancellationToken = default);
}

public interface IPortfolioSnapshotRepository : IRepository<PortfolioSnapshotEntity>
{
    Task<PortfolioSnapshotEntity?> GetByPortfolioIdAndDateAsync(string portfolioId, DateTime date, CancellationToken cancellationToken = default);
    Task<IEnumerable<PortfolioSnapshotEntity>> GetByPortfolioIdAsync(string portfolioId, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<PortfolioSnapshotEntity?> GetLatestByPortfolioIdAsync(string portfolioId, CancellationToken cancellationToken = default);
    Task UpsertAsync(PortfolioSnapshotEntity snapshot, CancellationToken cancellationToken = default);
}

public interface IRiskProfileRepository : IRepository<RiskProfile>
{
    Task<RiskProfile?> GetByPortfolioIdAsync(string portfolioId, CancellationToken cancellationToken = default);
    Task UpsertAsync(RiskProfile riskProfile, CancellationToken cancellationToken = default);
}

public interface IStopLossTargetRepository : IRepository<StopLossTarget>
{
    Task<IEnumerable<StopLossTarget>> GetByPortfolioIdAsync(string portfolioId, CancellationToken cancellationToken = default);
    Task<StopLossTarget?> GetByTradeIdAsync(string tradeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StopLossTarget>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StopLossTarget>> GetUntriggeredAsync(CancellationToken cancellationToken = default);
}

public interface IStrategyRepository : IRepository<Strategy>
{
    Task<IEnumerable<Strategy>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Strategy>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken = default);
}

public interface ITradeJournalRepository : IRepository<TradeJournal>
{
    Task<TradeJournal?> GetByTradeIdAsync(string tradeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<TradeJournal>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<TradeJournal>> GetByPortfolioIdAsync(string portfolioId, CancellationToken cancellationToken = default);
}

public interface ITradePlanRepository : IRepository<TradePlan>
{
    Task<IEnumerable<TradePlan>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<TradePlan?> GetByTradeIdAsync(string tradeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<TradePlan>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<TradePlan?> GetActiveByPortfolioAndSymbolAsync(string portfolioId, string symbol, CancellationToken cancellationToken = default);
}

public interface IAlertRuleRepository : IRepository<AlertRule>
{
    Task<IEnumerable<AlertRule>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<AlertRule>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<AlertRule>> GetActiveAsync(CancellationToken cancellationToken = default);
}

public interface IAlertHistoryRepository : IRepository<AlertHistory>
{
    Task<IEnumerable<AlertHistory>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<AlertHistory>> GetUnreadByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default);
}

public interface IExchangeRateRepository : IRepository<ExchangeRate>
{
    Task<ExchangeRate?> GetLatestAsync(string baseCurrency, string targetCurrency, CancellationToken cancellationToken = default);
    Task<IEnumerable<ExchangeRate>> GetAllLatestAsync(string baseCurrency, CancellationToken cancellationToken = default);
    Task UpsertAsync(ExchangeRate rate, CancellationToken cancellationToken = default);
}

public interface IBacktestRepository : IRepository<Backtest>
{
    Task<IEnumerable<Backtest>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Backtest>> GetPendingAsync(CancellationToken cancellationToken = default);
}

public interface IDailyRoutineRepository : IRepository<DailyRoutine>
{
    Task<DailyRoutine?> GetByUserIdAndDateAsync(string userId, DateTime date, CancellationToken cancellationToken = default);
    Task<DailyRoutine?> GetAnyByUserIdAndDateAsync(string userId, DateTime date, CancellationToken cancellationToken = default);
    Task<IEnumerable<DailyRoutine>> GetByUserIdRangeAsync(string userId, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<DailyRoutine?> GetLatestByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task HardDeleteAsync(string id, CancellationToken cancellationToken = default);
}

public interface IWatchlistRepository : IRepository<Watchlist>
{
    Task<IEnumerable<Watchlist>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<Watchlist?> GetDefaultByUserIdAsync(string userId, CancellationToken cancellationToken = default);
}

public interface IRoutineTemplateRepository : IRepository<RoutineTemplate>
{
    Task<IEnumerable<RoutineTemplate>> GetAllForUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<RoutineTemplate>> GetBuiltInAsync(CancellationToken cancellationToken = default);
}

public interface IFeeCalculationService
{
    Money CalculateTransactionFee(Money transactionAmount, bool isBuy, bool isListed = true, SecurityType securityType = SecurityType.Stock);
    Money CalculateSecuritiesTax(Money transactionAmount, SecurityType securityType, bool isBuy);
    Money CalculateTransferFee(Money transferAmount, SecurityType securityType);
    Money CalculateCustodyFee(int stockCount, int bondCount, int months = 1);
    Money CalculateVAT(Money baseAmount, string serviceType);
    Money CalculateAnnualManagementFee(Money portfolioValue);
    TradingFeesSummary GetFeesSummary(Money transactionAmount, SecurityType securityType, bool isBuy, bool isListed = true);
}

public interface IFeeConfiguration
{
    IReadOnlyList<TransactionFeeTier> TransactionFeeTiers { get; }
    decimal UnlistedStockFee { get; }
    decimal ListedBondFee { get; }
    decimal UnlistedBondFee { get; }
    decimal PersonalIncomeTax { get; }
    decimal StockTransferTax { get; }
    decimal BondTransferTax { get; }
    decimal MinTransferFee { get; }
    decimal MonthlyStockCustodyFee { get; }
    decimal MonthlyBondCustodyFee { get; }
    decimal VATRate { get; }
    IReadOnlyList<string> VATApplicableServices { get; }
    decimal AnnualManagementFee { get; }
    decimal MinManagementFee { get; }
    string LastUpdated { get; }
    string Source { get; }
}

public class TransactionFeeTier
{
    public decimal MinValue { get; set; }
    public decimal MaxValue { get; set; }
    public decimal BuyFee { get; set; }
    public decimal SellFee { get; set; }
}

public class TradingFeesSummary
{
    public Money TransactionAmount { get; set; } = new Money(0, "VND");
    public Money TransactionFee { get; set; } = new Money(0, "VND");
    public Money Tax { get; set; } = new Money(0, "VND");
    public Money SubtotalFees { get; set; } = new Money(0, "VND");
    public Money VAT { get; set; } = new Money(0, "VND");
    public Money TotalFees { get; set; } = new Money(0, "VND");
    public Money NetAmount { get; set; } = new Money(0, "VND");
    public string FeeConfigSource { get; set; } = string.Empty;
    public string LastUpdated { get; set; } = string.Empty;
    public SecurityType SecurityType { get; set; } = SecurityType.Stock;
    public bool IsListed { get; set; } = true;
    public bool IsBuy { get; set; } = true;
}
