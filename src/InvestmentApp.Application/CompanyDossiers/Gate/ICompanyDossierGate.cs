namespace InvestmentApp.Application.CompanyDossiers.Gate;

/// <param name="Reason">"missing" | "unconfirmed" | "expired" | "insufficient" | null khi pass</param>
public record DossierGateResult(bool Passed, string? Reason, List<string> Missing)
{
    public static DossierGateResult Ok() => new(true, null, new());
    public static DossierGateResult Fail(string reason, params string[] missing)
        => new(false, reason, missing.ToList());
}

/// <summary>
/// Kế thừa InvalidOperationException có chủ đích: nếu nhánh riêng trong ExceptionMiddleware
/// bị xóa thì hành vi thoái về 409 Conflict, không thành 500.
/// </summary>
public class DossierGateException : InvalidOperationException
{
    public string Symbol { get; }
    public DossierGateResult Result { get; }

    public DossierGateException(string symbol, DossierGateResult result)
        : base($"Chưa đủ hồ sơ công ty cho mã {symbol}")
    {
        Symbol = symbol;
        Result = result;
    }
}

public interface ICompanyDossierGate
{
    Task<DossierGateResult> EvaluateAsync(string userId, string symbol,
        decimal planSize, decimal? accountBalance, CancellationToken ct);

    Task EnsureAsync(string userId, string symbol,
        decimal planSize, decimal? accountBalance, CancellationToken ct);
}
