using System.ComponentModel;
using InvestmentApp.Application.Watchlists.Commands.AddWatchlistItem;
using InvestmentApp.Application.Watchlists.Commands.CreateWatchlist;
using InvestmentApp.Application.Watchlists.Commands.DeleteWatchlist;
using InvestmentApp.Application.Watchlists.Commands.ImportVn30;
using InvestmentApp.Application.Watchlists.Commands.RemoveWatchlistItem;
using InvestmentApp.Application.Watchlists.Commands.UpdateWatchlist;
using InvestmentApp.Application.Watchlists.Commands.UpdateWatchlistItem;
using InvestmentApp.Application.Watchlists.Dtos;
using InvestmentApp.Application.Watchlists.Queries.GetWatchlistDetail;
using InvestmentApp.Application.Watchlists.Queries.GetWatchlists;
using MediatR;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Mcp;

[McpServerToolType]
public static class WatchlistTools
{
    [McpServerTool(Name = "list_watchlists", ReadOnly = true)]
    [Description("Liệt kê danh sách theo dõi (watchlist) của chủ khóa API.")]
    public static async Task<List<WatchlistDto>> ListWatchlists(
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetWatchlistsQuery { UserId = http.GetUserId() }, ct);

    [McpServerTool(Name = "get_watchlist", ReadOnly = true)]
    [Description("Chi tiết một watchlist theo id (gồm các mã bên trong).")]
    public static async Task<WatchlistDetailDto> GetWatchlist(
        [Description("ID watchlist.")] string id,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetWatchlistDetailQuery { Id = id, UserId = http.GetUserId() }, ct);

    [McpServerTool(Name = "create_watchlist", Destructive = true)]
    [Description("Tạo watchlist mới.")]
    public static async Task<WatchlistDto> CreateWatchlist(
        [Description("Tên watchlist.")] string name,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct,
        [Description("Emoji đại diện (bỏ trống = ⭐).")] string? emoji = null,
        [Description("Đặt làm watchlist mặc định (bỏ trống = false).")] bool? isDefault = null,
        [Description("Thứ tự sắp xếp (bỏ trống = 0).")] int? sortOrder = null)
        => await mediator.Send(new CreateWatchlistCommand
        {
            UserId = http.GetUserId(),
            Name = name,
            Emoji = emoji ?? "⭐",
            IsDefault = isDefault ?? false,
            SortOrder = sortOrder ?? 0
        }, ct);

    [McpServerTool(Name = "update_watchlist", Destructive = true)]
    [Description("Cập nhật watchlist theo id (đổi tên, emoji, thứ tự).")]
    public static async Task<string> UpdateWatchlist(
        [Description("ID watchlist.")] string id,
        [Description("Tên mới.")] string name,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct,
        [Description("Emoji đại diện (bỏ trống = ⭐).")] string? emoji = null,
        [Description("Thứ tự sắp xếp (bỏ trống = 0).")] int? sortOrder = null)
    {
        await mediator.Send(new UpdateWatchlistCommand
        {
            Id = id,
            UserId = http.GetUserId(),
            Name = name,
            Emoji = emoji ?? "⭐",
            SortOrder = sortOrder ?? 0
        }, ct);
        return "ok";
    }

    [McpServerTool(Name = "delete_watchlist", Destructive = true)]
    [Description("Xóa một watchlist theo id.")]
    public static async Task<string> DeleteWatchlist(
        [Description("ID watchlist.")] string id,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
    {
        await mediator.Send(new DeleteWatchlistCommand { Id = id, UserId = http.GetUserId() }, ct);
        return "ok";
    }

    [McpServerTool(Name = "add_watchlist_item", Destructive = true)]
    [Description("Thêm một mã vào watchlist. Trả về watchlist sau khi cập nhật.")]
    public static async Task<WatchlistDetailDto> AddWatchlistItem(
        [Description("ID watchlist.")] string id,
        [Description("Mã chứng khoán.")] string symbol,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct,
        [Description("Ghi chú (bỏ trống = không ghi chú).")] string? note = null,
        [Description("Giá mua mục tiêu, VND (bỏ trống = không đặt).")] decimal? targetBuyPrice = null,
        [Description("Giá bán mục tiêu, VND (bỏ trống = không đặt).")] decimal? targetSellPrice = null)
        => await mediator.Send(new AddWatchlistItemCommand
        {
            WatchlistId = id,
            UserId = http.GetUserId(),
            Symbol = symbol,
            Note = note,
            TargetBuyPrice = targetBuyPrice,
            TargetSellPrice = targetSellPrice
        }, ct);

    [McpServerTool(Name = "update_watchlist_item", Destructive = true)]
    [Description("Cập nhật một mã trong watchlist (ghi chú, mục tiêu…).")]
    public static async Task<WatchlistDetailDto> UpdateWatchlistItem(
        [Description("ID watchlist.")] string id,
        [Description("Mã chứng khoán.")] string symbol,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct,
        [Description("Ghi chú (bỏ trống = xóa ghi chú).")] string? note = null,
        [Description("Giá mua mục tiêu, VND (bỏ trống = xóa mục tiêu).")] decimal? targetBuyPrice = null,
        [Description("Giá bán mục tiêu, VND (bỏ trống = xóa mục tiêu).")] decimal? targetSellPrice = null)
        => await mediator.Send(new UpdateWatchlistItemCommand
        {
            WatchlistId = id,
            UserId = http.GetUserId(),
            Symbol = symbol,
            Note = note,
            TargetBuyPrice = targetBuyPrice,
            TargetSellPrice = targetSellPrice
        }, ct);

    [McpServerTool(Name = "remove_watchlist_item", Destructive = true)]
    [Description("Bỏ một mã khỏi watchlist. Trả về watchlist sau khi cập nhật.")]
    public static async Task<WatchlistDetailDto> RemoveWatchlistItem(
        [Description("ID watchlist.")] string id,
        [Description("Mã chứng khoán.")] string symbol,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new RemoveWatchlistItemCommand
        {
            WatchlistId = id, Symbol = symbol, UserId = http.GetUserId()
        }, ct);

    [McpServerTool(Name = "import_vn30", Destructive = true)]
    [Description("Nhập toàn bộ rổ VN30 vào watchlist.")]
    public static async Task<WatchlistDetailDto> ImportVn30(
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct,
        [Description("ID watchlist đích (bỏ trống = tạo/dùng watchlist VN30 mặc định).")] string? watchlistId = null)
        => await mediator.Send(new ImportVn30Command
        {
            UserId = http.GetUserId(),
            WatchlistId = watchlistId
        }, ct);
}
