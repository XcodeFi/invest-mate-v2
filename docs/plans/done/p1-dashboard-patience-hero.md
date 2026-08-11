# P1 — Màn tĩnh tâm trên trang chủ

Spec: [`docs/superpowers/specs/2026-08-11-dashboard-patience-hero-design.md`](../../superpowers/specs/2026-08-11-dashboard-patience-hero-design.md)
ADR: [`docs/adr/0013-dashboard-patience-gate-self-reported-mood.md`](../../adr/0013-dashboard-patience-gate-self-reported-mood.md)

Một chặng duy nhất, không chia phase.

## Mục tiêu

Chèn một khoảng lặng vào đầu `/dashboard`: hoạt hoạ người đi câu bám theo số ngày chưa đặt lệnh, châm ngôn xoay theo tâm trạng tự chấm, và một luật dừng phủ Hàng đợi quyết định khi người dùng tự nhận đang có cảm xúc. Đồng thời gỡ widget Giao dịch nhanh khỏi trang chủ.

## Tầng bị ảnh hưởng

Domain · Application · Infrastructure · Api · Frontend

## Việc theo tầng

### Domain
- `Entities/MoodCheckIn.cs` — `UserId`, `DateKey` (string), `Mood` (enum `MoodState`: Calm/Fomo/Fear/Revenge), `CheckedAt`, `OverrodeAt`
- Phương thức `SetMood(mood, now)` — đổi tâm trạng thì **xoá `OverrodeAt` về null**
- Phương thức `MarkOverridden(now)`
- `Enums/MoodState.cs`

### Application
- `Mood/Queries/GetTodayMood/` — query + handler + DTO
- `Mood/Commands/SetMood/` — command + handler (upsert theo `UserId + DateKey`)
- `Mood/Commands/MarkMoodOverride/` — command + handler
- `Common/VietnamDate.cs` — hàm thuần `ToDateKey(DateTime utcNow)` → `"YYYY-MM-DD"` theo `+07:00`
- `Trades/Queries/GetLastTradeActivity/` — query + handler + DTO (`LastTradeDate`, `DaysSince`)
- `RepositoryInterfaces.cs` — `IMoodCheckInRepository`; thêm `GetLastTradeDateAsync(userId, ct)` vào `ITradeRepository`

### Infrastructure
- `Repositories/MoodCheckInRepository.cs` — collection `mood_check_ins`, unique index `(UserId, DateKey)`
- `Repositories/TradeRepository.cs` — cài `GetLastTradeDateAsync`
- Đăng ký DI trong `Program.cs`

### Api
- `Controllers/MoodController.cs` — `GET /api/v1/mood/today`, `POST /api/v1/mood`, `POST /api/v1/mood/override`
- `Controllers/TradesController.cs` — thêm `GET /api/v1/trades/last-activity`

### Frontend
- `features/dashboard/widgets/fishing-scene.component.ts` — SVG + CSS thuần, nhận `calm: 0..1` và `dim: boolean`
- `features/dashboard/widgets/patience-quotes.ts` — 22 câu + `pickQuote(mood, seed)`
- `features/dashboard/widgets/patience-hero.component.ts` — ghép, gọi API, phát `moodChange`
- `core/services/mood.service.ts` — 3 endpoint (nhớ tự gắn `getHeaders()`, dự án không có auth interceptor)
- `core/services/trade.service.ts` — thêm `getLastActivity()`
- `features/dashboard/dashboard.component.ts` — chèn hero, bọc `<app-decision-queue>` trong lớp phủ, **xoá widget Giao dịch nhanh** (template 610–704 + khối method cuối class + dọn `UppercaseDirective`, `isBuyTrade`, `RiskService`/`RiskProfile`, 7 field `qt*`)

## Test

| Nơi | Ca |
|---|---|
| `Domain.Tests` | `SetMood` đổi tâm trạng → `OverrodeAt` về null; `MarkOverridden` đóng dấu |
| `Application.Tests` | `GetLastTradeActivity`: chưa có lệnh → null; trả `TradeDate` lớn nhất; **lệnh user khác không lọt vào** |
| `Application.Tests` | `GetTodayMood`: qua ngày mới → null; **bản ghi user khác không lọt vào** |
| `Application.Tests` | `SetMood`: chấm hai lần trong ngày → 1 bản ghi |
| `Application.Tests` | `VietnamDate.ToDateKey`: 00:30 giờ VN (17:30 UTC hôm trước) → ra ngày VN hôm nay |
| `Application.Tests` | `MarkMoodOverride` khi chưa chấm → không tạo bản ghi ma |
| Frontend `.spec.ts` | `pickQuote` đúng nhóm / cùng seed cùng câu / không rỗng |
| Frontend `.spec.ts` | `fishing-scene` kẹp `calm` về 0..1; `prefers-reduced-motion` gắn class |
| Frontend `.spec.ts` | `patience-hero` hiện đúng số ngày; `null` → "Chưa có lệnh nào" |
| Frontend `.spec.ts` | `dashboard` mood ≠ Calm → lớp phủ hiện; bấm "Vẫn xem" → gọi override |

## Rủi ro

- **Xoá Quick Trade làm mồ côi import** — build đỏ nếu dọn thiếu hoặc dọn nhầm. `FormsModule` và `MarketDataService` phải giữ (còn phục vụ ô CAGR và giá lô vị thế).
- **Ranh giới ngày** — `DateKey` chuỗi + server tự tính là để tránh bẫy Mongo dịch nửa đêm giờ VN thành 17:00 hôm trước. Test phải phủ đúng ca 00:30.
- **Service mới quên `getHeaders()`** — build xanh rồi 401 lúc chạy, dự án không có auth interceptor toàn cục.
- **`dashboard.component.ts` 1254 dòng** — sửa ở nhiều chỗ trong một file lớn; xoá theo đúng ranh giới comment `// ─── Quick Trade Widget ───` để không cắt nhầm.

## Checkpoint — hoàn tất 2026-08-11

- **Test:** backend 1846 pass / 4 project (Domain 800 · Application 438 · Infrastructure 389 · Api 219); frontend 316 pass.
- **Code review:** 4 phát hiện, sửa cả 4, soát lại phần vừa sửa bằng agent thứ hai → sạch. Nặng nhất là lớp phủ vắng mặt suốt vòng gọi API — đúng khoảnh khắc luật dừng cần chặn nhất.
- **Verify thật:** 15/15 kịch bản trên browser với DB prod, gồm ca hoãn API 3,5 giây chứng minh Hàng đợi không lộ ra trong lúc chờ. Báo cáo: `scratch/qa-reports/qa-verify-dashboard-patience-hero-20260811-0510z.md`.
- **Hai lỗi chỉ nhìn mới thấy** (build và test đều xanh): `viewBox` lệch tỷ lệ khung làm cắt cụt nhân vật; biên độ sóng để cứng trong path nên `calm` không làm hồ lặng được. Đã ghi thành pitfall trong `project-context.md`.
- **Chưa làm, có ý thức:** route guard cho `/trade-plan` + `/trade-wizard`; màn đối chiếu "hôm FOMO tôi đã làm gì".

## ADR

Cần — xem ADR-0013. Lý do: thêm collection + index mới, thêm 4 endpoint công khai, và có đánh đổi thật giữa 3 cách hiện thực luật dừng.
