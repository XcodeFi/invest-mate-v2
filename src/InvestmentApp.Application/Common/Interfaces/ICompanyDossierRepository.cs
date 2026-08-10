using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Common.Interfaces;

public interface ICompanyDossierRepository
{
    Task<CompanyDossier?> GetAsync(string userId, string symbol);
    Task<List<CompanyDossier>> GetByUserIdAsync(string userId);
    Task CreateAsync(CompanyDossier dossier);
    Task UpdateAsync(CompanyDossier dossier);
}

/// <summary>
/// Một request khác đã tạo hồ sơ cho cùng (UserId, Symbol) giữa lúc request này
/// tìm thấy null và lúc nó insert. Caller bắt để tìm lại rồi cập nhật lên document
/// đã thắng, thay vì để lỗi driver thoát ra thành 500.
/// Kế thừa InvalidOperationException để ca hiếm thoát được ra ngoài thành 409
/// (xem ánh xạ trong ExceptionMiddleware), không phải 500.
/// </summary>
public class DuplicateDossierException : InvalidOperationException
{
    public DuplicateDossierException(string userId, string symbol, Exception? inner = null)
        : base($"Hồ sơ công ty cho mã {symbol} đã tồn tại.", inner)
    {
        UserId = userId;
        Symbol = symbol;
    }

    public string UserId { get; }
    public string Symbol { get; }
}
