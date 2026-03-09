# Chiến lược Đầu tư & Quản lý Rủi ro - Seed Data

> Tài liệu mô tả hệ thống **Strategy Templates** và **Risk Profile Templates** — dữ liệu gợi ý được khởi tạo sẵn khi ứng dụng chạy lần đầu.
>
> **Mục đích**: Hướng dẫn người dùng lựa chọn chiến lược phù hợp với trình độ, phong cách đầu tư, và điều kiện thị trường.

---

## Mục lục

1. [Tổng quan](#1-tổng-quan)
2. [Kiến trúc & Cách hoạt động](#2-kiến-trúc--cách-hoạt-động)
3. [Strategy Templates (14 chiến lược)](#3-strategy-templates)
   - [Nhóm Value Investing](#31-nhóm-value-investing)
   - [Nhóm Technical Analysis](#32-nhóm-technical-analysis)
   - [Nhóm Portfolio Management](#33-nhóm-portfolio-management)
4. [Risk Profile Templates (4 mức)](#4-risk-profile-templates)
5. [Ma trận chọn chiến lược](#5-ma-trận-chọn-chiến-lược)
6. [Hướng dẫn cho Developer](#6-hướng-dẫn-cho-developer)

---

## 1. Tổng quan

Investment Mate v2 không chỉ là công cụ ghi nhận giao dịch mà là **nền tảng hướng dẫn quản lý đầu tư**. Hệ thống cung cấp:

- **14 chiến lược đầu tư mẫu** với đầy đủ quy tắc Entry/Exit/Risk
- **4 mức Risk Profile** từ Bảo thủ đến Mạo hiểm
- Mỗi template có **gợi ý** (Suggestion) ai nên dùng và khi nào nên dùng
- Phân loại theo **Difficulty Level** (Beginner → Advanced) để người dùng chọn phù hợp

### Phân bố chiến lược

| Category | Số lượng | Mô tả |
|---|---|---|
| **ValueInvesting** | 4 | Phân tích cơ bản, đầu tư dài hạn |
| **Technical** | 5 | Phân tích kỹ thuật, giao dịch trung-ngắn hạn |
| **PortfolioManagement** | 5 | Quản lý danh mục, phân bổ tài sản |

| Difficulty | Số lượng | Đối tượng |
|---|---|---|
| **Beginner** | 2 | Người mới, ít thời gian |
| **Intermediate** | 5 | Có kinh nghiệm cơ bản |
| **Advanced** | 7 | Trader tích cực, chuyên nghiệp |

---

## 2. Kiến trúc & Cách hoạt động

### Entities

```
src/InvestmentApp.Domain/Entities/
├── StrategyTemplate.cs      # Template chiến lược (system-level)
└── RiskProfileTemplate.cs   # Template hồ sơ rủi ro (system-level)
```

**StrategyTemplate** — Không kế thừa `AggregateRoot` vì là data tĩnh, không cần domain events/versioning.

| Property | Type | Mô tả |
|---|---|---|
| `Id` | string | ID cố định (VD: `tpl-strategy-001`) |
| `Name` | string | Tên chiến lược (VD: "Value Investing (Đầu tư giá trị)") |
| `Category` | string | `ValueInvesting` \| `Technical` \| `PortfolioManagement` |
| `Description` | string | Mô tả chi tiết chiến lược |
| `Suggestion` | string | Gợi ý khi nào nên dùng |
| `EntryRules` | string | Quy tắc vào lệnh |
| `ExitRules` | string | Quy tắc thoát lệnh |
| `RiskRules` | string | Quy tắc quản lý rủi ro |
| `TimeFrame` | string | `Scalping` \| `DayTrading` \| `Swing` \| `Position` |
| `MarketCondition` | string | `Trending` \| `Ranging` \| `Volatile` \| `All` |
| `DifficultyLevel` | string | `Beginner` \| `Intermediate` \| `Advanced` |
| `SuitableFor` | List\<string\> | Đối tượng phù hợp |
| `KeyIndicators` | List\<string\> | Chỉ báo kỹ thuật/cơ bản chính |
| `Tags` | List\<string\> | Tags cho tìm kiếm/lọc |
| `SortOrder` | int | Thứ tự hiển thị |

**RiskProfileTemplate** — Mapping 1:1 với `RiskProfile` entity.

| Property | Type | Mô tả |
|---|---|---|
| `Id` | string | ID cố định (VD: `tpl-risk-001`) |
| `Name` | string | Tên mức rủi ro |
| `Description` | string | Mô tả chi tiết |
| `Suggestion` | string | Gợi ý ai nên dùng |
| `MaxPositionSizePercent` | decimal | % tối đa danh mục cho 1 mã |
| `MaxSectorExposurePercent` | decimal | % tối đa cho 1 ngành |
| `MaxDrawdownAlertPercent` | decimal | % drawdown để cảnh báo |
| `DefaultRiskRewardRatio` | decimal | Tỷ lệ R:R mặc định |
| `MaxPortfolioRiskPercent` | decimal | % rủi ro tối đa cho 1 trade |

### Seed Data Flow

```
App Startup (Program.cs)
    │
    ▼
SeedDataService.SeedAllAsync()
    │
    ├── Kiểm tra collection "strategy_templates" có data không
    │   ├── CÓ → Skip (idempotent)
    │   └── KHÔNG → Load từ embedded JSON → InsertMany → Tạo indexes
    │
    └── Kiểm tra collection "risk_profile_templates" có data không
        ├── CÓ → Skip
        └── KHÔNG → Load từ embedded JSON → InsertMany → Tạo indexes
```

### Files liên quan

```
src/InvestmentApp.Domain/Entities/
├── StrategyTemplate.cs
└── RiskProfileTemplate.cs

src/InvestmentApp.Infrastructure/
├── Seed/
│   ├── SeedDataService.cs              # Service seed data
│   └── Data/
│       ├── strategy_templates.json     # 14 chiến lược mẫu
│       └── risk_profile_templates.json # 4 mức rủi ro
└── InvestmentApp.Infrastructure.csproj  # EmbeddedResource config

src/InvestmentApp.Api/
└── Program.cs                           # Gọi SeedAllAsync() khi startup
```

### MongoDB Collections

| Collection | Documents | Indexes |
|---|---|---|
| `strategy_templates` | 14 | Category, DifficultyLevel, TimeFrame, SortOrder |
| `risk_profile_templates` | 4 | SortOrder |

---

## 3. Strategy Templates

### 3.1 Nhóm Value Investing

#### 1. Value Investing (Đầu tư giá trị) — `tpl-strategy-001`

> **Benjamin Graham & Warren Buffett** | Position | All | Intermediate

**Mô tả**: Tìm cổ phiếu bị định giá thấp hơn giá trị thực (intrinsic value), mua với biên an toàn (margin of safety) và nắm giữ dài hạn.

**Khi nào dùng**: Khi bạn kiên nhẫn, thích phân tích tài chính, có thể chờ 1-3 năm. Hiệu quả nhất khi thị trường bi quan.

| Quy tắc | Chi tiết |
|---|---|
| **Entry** | P/E < 15, P/B < 1.5, ROE > 15% (3 năm), D/E < 1.0, FCF dương, Margin of Safety ≥ 30% |
| **Exit** | Giá đạt giá trị nội tại, cơ bản xấu đi, P/E > 25 |
| **Risk** | Max 15-20%/mã, đa dạng 5-8 ngành, không margin, dự phòng 15-20% tiền mặt |

**Chỉ báo chính**: P/E, P/B, ROE, D/E, Free Cash Flow, EPS Growth

---

#### 2. CANSLIM (William O'Neil) — `tpl-strategy-002`

> **Kết hợp cơ bản + kỹ thuật** | Swing | Trending | Advanced

**Mô tả**: C-A-N-S-L-I-M = Current earnings, Annual earnings, New products, Supply & demand, Leader, Institutional sponsorship, Market direction.

**Khi nào dùng**: Thị trường đang uptrend rõ ràng. Cần theo dõi hàng tuần.

| Quy tắc | Chi tiết |
|---|---|
| **Entry** | EPS quý tăng ≥ 25%, EPS năm tăng ≥ 25%/năm, RS ≥ 80, có tổ chức mua, VNINDEX uptrend, breakout từ nền tích lũy |
| **Exit** | Chốt lời +20-25%, cắt lỗ 7-8% BẮT BUỘC, bán khi thị trường downtrend |
| **Risk** | Stop loss 7-8%, max 10-15%/mã, tối đa 4-5 mã, mua dần 50-25-25 |

---

#### 3. Dividend Growth Investing — `tpl-strategy-007`

> **Cổ tức tăng trưởng** | Position | All | Beginner

**Mô tả**: Tập trung cổ phiếu trả cổ tức đều đặn và tăng hàng năm. Thu nhập thụ động + lãi kép.

**Khi nào dùng**: Muốn thu nhập thụ động, gần nghỉ hưu, thích lãi kép dài hạn.

| Quy tắc | Chi tiết |
|---|---|
| **Entry** | Dividend Yield ≥ 5%, trả cổ tức ≥ 3 năm, Payout Ratio 30-70%, ROE > 15%, FCF > Cổ tức |
| **Exit** | Cắt/ngừng cổ tức, Payout > 90%, EPS giảm 2 quý |
| **Risk** | Max 15%/mã, đa dạng 4-5 ngành, tái đầu tư cổ tức (DRIP), không đuổi yield cao bất thường |

---

#### 4. Deep Value / Contrarian — `tpl-strategy-013`

> **Đầu tư ngược dòng** | Position | Ranging | Advanced

**Mô tả**: Mua khi mọi người sợ, bán khi mọi người tham. Cần can đảm và kiên nhẫn cao.

**Khi nào dùng**: Thị trường panic, ngành bị bán tháo quá mức nhưng cơ bản vẫn tốt.

| Quy tắc | Chi tiết |
|---|---|
| **Entry** | P/B < 0.8, P/E thấp nhất 5 năm, FCF dương, volume bán cạn, insider đang mua |
| **Exit** | Giá về P/E trung bình 5 năm, sentiment chuyển lạc quan, chốt lời 30-30-40% |
| **Risk** | Position nhỏ 5-8%/mã, mua dần 3-4 đợt, kiên nhẫn 6-18 tháng, không margin |

---

### 3.2 Nhóm Technical Analysis

#### 5. Trend Following — `tpl-strategy-003`

> **Theo xu hướng** | Swing | Trending | Intermediate

**Mô tả**: Đi theo xu hướng chính, không đoán đỉnh/đáy. Mua khi uptrend xác nhận, giữ đến khi đảo chiều.

| Quy tắc | Chi tiết |
|---|---|
| **Entry** | Golden Cross (MA50 > MA200), MACD cắt lên signal, RSI 50-70, ADX > 25, volume tăng |
| **Exit** | Giá cắt xuống MA50, Death Cross, RSI > 80 + nến đảo chiều, trailing stop 7-10% |
| **Risk** | Stop loss 7-8%, trailing stop theo MA20, max 15%/mã, R:R ≥ 2:1 |

---

#### 6. Breakout Trading — `tpl-strategy-004`

> **Phá vỡ vùng giá** | Swing | Volatile | Advanced

**Mô tả**: Mua khi giá phá vỡ kháng cự với volume lớn sau giai đoạn tích lũy.

| Quy tắc | Chi tiết |
|---|---|
| **Entry** | Volume ≥ 150% TB 20 phiên, nền tích lũy 4-8 tuần, Cup-with-handle/Flat base, đóng cửa trên kháng cự |
| **Exit** | Chốt 50% tại +15-20%, trailing stop 8-10%, bán nếu false breakout |
| **Risk** | Stop loss dưới vùng breakout (3-5%), R:R ≥ 3:1, tối đa 3-4 vị thế |

---

#### 7. Mean Reversion — `tpl-strategy-005`

> **Hồi về trung bình** | Swing | Ranging | Advanced

**Mô tả**: Mua khi oversold, bán khi overbought. Ngược với Trend Following.

| Quy tắc | Chi tiết |
|---|---|
| **Entry** | RSI < 30, giá chạm BB dưới, nến đảo chiều (Hammer, Engulfing), MACD divergence dương |
| **Exit** | Giá chạm MA20, RSI > 60-70, chạm BB trên, time stop 10-15 phiên |
| **Risk** | Stop loss 5-7%, position nhỏ 5-10%/mã, mua dần 40-30-30, không bắt dao rơi khi cơ bản xấu |

---

#### 8. Momentum Trading — `tpl-strategy-008`

> **Giao dịch động lượng** | DayTrading | Trending | Advanced

**Mô tả**: Mua cổ phiếu mạnh nhất, bán cổ phiếu yếu nhất. Tốc độ là yếu tố then chốt.

| Quy tắc | Chi tiết |
|---|---|
| **Entry** | RS ≥ 85, giá tăng > 20% trong 3 tháng, RSI 60-80, giá trên MA20 & MA50, sector outperform |
| **Exit** | Giá cắt MA20, RS < 70, volume giảm khi giá tăng, trailing stop 8-10%, time stop 3 tuần |
| **Risk** | Stop loss 8-10%, max 10-12%/mã, tối đa 5-6 mã, xoay vòng hàng tuần |

---

#### 9. Scalping — `tpl-strategy-011`

> **Lướt sóng ngắn hạn** | Scalping | Volatile | Advanced

**Mô tả**: Nhiều lệnh/ngày, lợi nhuận nhỏ 1-3%. CHỈ cho trader toàn thời gian.

| Quy tắc | Chi tiết |
|---|---|
| **Entry** | Top 30 thanh khoản HOSE, spread ≤ 0.3%, RSI(5) < 30, 30 phút đầu & cuối phiên |
| **Exit** | Chốt +1-3%, cắt -1-1.5% NGAY, thoát trước 14h30, max 3 lần thua → dừng |
| **Risk** | Stop loss 1-1.5%, 20-30% vốn/trade, max loss/ngày 2%, phí ~0.4%/trade |

---

#### 10. Swing Trading Price Action — `tpl-strategy-014`

> **Hành động giá thuần túy** | Swing | All | Intermediate

**Mô tả**: Dựa trên nến Nhật, mô hình giá, hỗ trợ/kháng cự. Đơn giản, không nhiều indicator.

| Quy tắc | Chi tiết |
|---|---|
| **Entry** | Nến đảo chiều tại hỗ trợ (Pin Bar, Engulfing, Morning Star), volume tăng, weekly trend ủng hộ |
| **Exit** | Take profit tại kháng cự tiếp, trailing stop theo swing low, time stop 2 tuần |
| **Risk** | Stop loss dưới hỗ trợ (3-7%), R:R ≥ 2:1, max 10-15%/mã, tối đa 4-5 vị thế |

---

### 3.3 Nhóm Portfolio Management

#### 11. DCA (Dollar Cost Averaging) — `tpl-strategy-006`

> **Bình quân giá** | Position | All | Beginner

**Mô tả**: Đầu tư định kỳ số tiền cố định bất kể giá. Đơn giản nhất, phù hợp người mới.

| Quy tắc | Chi tiết |
|---|---|
| **Entry** | VN30 ETF / bluechip, định kỳ hàng tháng, số tiền cố định, ngày cố định |
| **Exit** | Đạt mục tiêu tài chính, cơ bản thay đổi nghiêm trọng, rebalance hàng năm |
| **Risk** | Đa dạng 5-8 mã/1-2 ETF, không margin, quỹ khẩn cấp 6 tháng trước, rebalance nếu 1 mã > 25% |

---

#### 12. Core-Satellite — `tpl-strategy-009`

> **Lõi - Vệ tinh** | Position | All | Intermediate

**Mô tả**: Core 60-70% (ETF/bluechip dài hạn) + Satellite 30-40% (tăng trưởng/đầu cơ).

| Quy tắc | Chi tiết |
|---|---|
| **Entry** | Core: DCA bluechip ROE > 15%. Satellite: Trend Following/Breakout, RS ≥ 80, EPS ≥ 20% |
| **Exit** | Core: chỉ khi rebalance. Satellite: trailing 10%, chốt +20-25%, chuyển lời về Core mỗi quý |
| **Risk** | Satellite ≤ 40%, mỗi mã Satellite ≤ 8%, Core ≤ 15%/mã, rebalance mỗi quý |

---

#### 13. Sector Rotation — `tpl-strategy-010`

> **Xoay vòng ngành** | Swing | Trending | Advanced

**Mô tả**: Chuyển vốn theo chu kỳ kinh tế. Mỗi giai đoạn có ngành dẫn đầu khác nhau.

| Giai đoạn | Ngành ưu tiên |
|---|---|
| **Phục hồi sớm** | Công nghệ, BĐS, Tài chính |
| **Tăng trưởng** | Tiêu dùng, Công nghiệp, Nguyên vật liệu |
| **Đỉnh chu kỳ** | Năng lượng, Nguyên liệu thô, Hàng hóa |
| **Suy thoái** | Y tế, Tiện ích, Hàng thiết yếu |

| Quy tắc | Chi tiết |
|---|---|
| **Risk** | Luôn 2-3 ngành, tối đa 40%/ngành, giữ 10-15% cash khi chuyển giai đoạn |

---

#### 14. Barbell Strategy — `tpl-strategy-012`

> **Chiến lược tạ đôi (Nassim Taleb)** | Position | Volatile | Intermediate

**Mô tả**: 80-90% cực an toàn + 10-20% cực rủi ro. Bỏ qua vùng giữa.

| Quy tắc | Chi tiết |
|---|---|
| **An toàn (80-90%)** | Trái phiếu, tiền gửi, bluechip phòng thủ, Dividend Yield > 6% |
| **Rủi ro (10-20%)** | Small-cap EPS > 30%, ngành mới nổi, chấp nhận mất 100% phần này |
| **Risk** | Rủi ro TUYỆT ĐỐI ≤ 20%, mỗi mã rủi ro ≤ 5%, rebalance 6 tháng/lần |

---

## 4. Risk Profile Templates

Khi user tạo danh mục mới, có thể chọn 1 trong 4 mức rủi ro làm cấu hình mặc định:

| Thông số | Bảo thủ | Cân bằng | Tích cực | Mạo hiểm |
|---|---|---|---|---|
| **Max Position Size** | 10% | 15% | 25% | 35% |
| **Max Sector Exposure** | 25% | 35% | 50% | 60% |
| **Max Drawdown Alert** | 5% | 10% | 15% | 25% |
| **Risk:Reward Ratio** | 3.0 | 2.0 | 1.5 | 1.0 |
| **Max Portfolio Risk/Trade** | 1% | 2% | 5% | 8% |

### Chi tiết từng mức

#### Bảo thủ (Conservative) — `tpl-risk-001`

- **Mô tả**: Ưu tiên bảo toàn vốn tuyệt đối. Biến động thấp.
- **Dùng khi**: Mới bắt đầu, gần nghỉ hưu, cần vốn trong 1-2 năm, thị trường bất ổn.
- **Chiến lược phù hợp**: DCA, Dividend Growth, phần Core của Core-Satellite.

#### Cân bằng (Balanced) — `tpl-risk-002`

- **Mô tả**: Cân bằng tăng trưởng và bảo toàn. Profile mặc định cho đa số nhà đầu tư.
- **Dùng khi**: Kinh nghiệm cơ bản, đầu tư 3-5 năm, muốn tăng trưởng nhưng không mạo hiểm.
- **Chiến lược phù hợp**: Value Investing, Core-Satellite, Trend Following.

#### Tích cực (Aggressive) — `tpl-risk-003`

- **Mô tả**: Ưu tiên tăng trưởng. Chấp nhận drawdown lớn.
- **Dùng khi**: Kinh nghiệm ≥ 2 năm, vốn dài hạn 5-10 năm, chấp nhận drawdown 15-20%.
- **Chiến lược phù hợp**: CANSLIM, Momentum, Breakout, Sector Rotation.

#### Mạo hiểm (Speculative) — `tpl-risk-004`

- **Mô tả**: Rủi ro rất cao. CHỈ dùng với phần vốn sẵn sàng mất hết.
- **CẢNH BÁO**: KHÔNG BAO GIỜ dùng cho toàn bộ tài sản.
- **Dùng khi**: Trader toàn thời gian, có hệ thống backtest, phần Satellite/Rủi ro trong Barbell Strategy.

---

## 5. Ma trận chọn chiến lược

### Theo trình độ

| Trình độ | Chiến lược khuyến nghị |
|---|---|
| **Mới bắt đầu** | DCA → Dividend Growth |
| **Có kinh nghiệm cơ bản** | Value Investing → Core-Satellite → Trend Following → Price Action |
| **Trader tích cực** | CANSLIM → Breakout → Momentum → Sector Rotation |
| **Chuyên nghiệp** | Mean Reversion → Scalping → Deep Value (kết hợp nhiều chiến lược) |

### Theo điều kiện thị trường

| Thị trường | Chiến lược hiệu quả | Chiến lược tránh |
|---|---|---|
| **Uptrend mạnh** | Trend Following, CANSLIM, Momentum | Mean Reversion, Deep Value |
| **Sideway/Ranging** | Mean Reversion, DCA, Barbell | Trend Following, Momentum |
| **Volatile/Biến động** | Breakout, Scalping, Barbell | DCA (kém hiệu quả hơn) |
| **Downtrend/Suy thoái** | Deep Value, Barbell (phần an toàn), Sector Rotation (ngành phòng thủ) | Momentum, Scalping |

### Theo thời gian đầu tư

| Thời gian | Chiến lược |
|---|---|
| **< 1 ngày** | Scalping |
| **Vài ngày → vài tuần** | Momentum, Breakout, Mean Reversion, Price Action |
| **Vài tuần → vài tháng** | Trend Following, CANSLIM, Sector Rotation |
| **Năm+** | Value Investing, DCA, Dividend Growth, Core-Satellite, Barbell, Deep Value |

---

## 6. Hướng dẫn cho Developer

### Thêm chiến lược mới

1. Thêm entry mới vào `src/InvestmentApp.Infrastructure/Seed/Data/strategy_templates.json`
2. Đặt `Id` theo format `tpl-strategy-XXX`
3. Tăng `SortOrder` tương ứng
4. **Xóa collection** `strategy_templates` trong MongoDB để seed lại (hoặc insert thủ công)

> **Lưu ý**: SeedDataService chỉ chạy khi collection trống. Để cập nhật templates đã seed, cần xóa collection cũ hoặc tạo migration logic.

### Thêm Risk Profile mới

1. Thêm entry vào `src/InvestmentApp.Infrastructure/Seed/Data/risk_profile_templates.json`
2. Format tương tự, `Id`: `tpl-risk-XXX`

### API Endpoints (cần tạo)

Để expose templates cho frontend, cần tạo:

```
GET /api/v1/strategy-templates              # Lấy tất cả
GET /api/v1/strategy-templates?category=...  # Lọc theo category
GET /api/v1/strategy-templates?difficulty=... # Lọc theo difficulty
GET /api/v1/strategy-templates/{id}          # Chi tiết 1 template
GET /api/v1/risk-profile-templates           # Lấy tất cả risk profiles
```

### Luồng sử dụng trên Frontend

```
User tạo Strategy mới
    │
    ▼
Hiển thị danh sách Strategy Templates (gợi ý)
    │
    ▼
User chọn 1 template → Auto-fill form (Name, Entry/Exit/Risk Rules...)
    │
    ▼
User tùy chỉnh theo nhu cầu → Lưu thành Strategy cá nhân
```

```
User tạo Portfolio mới → Chọn Risk Profile
    │
    ▼
Hiển thị 4 Risk Profile Templates (Bảo thủ → Mạo hiểm)
    │
    ▼
User chọn → Áp dụng thông số vào RiskProfile của Portfolio
    │
    ▼
Có thể điều chỉnh từng thông số sau
```

### Reseed Data

```bash
# Kết nối MongoDB
mongosh

# Xóa templates cũ để seed lại
use investmentdb
db.strategy_templates.drop()
db.risk_profile_templates.drop()

# Restart app → SeedDataService sẽ tự seed lại
```

---

> **Cập nhật lần cuối**: March 2026 | Branch: `feature/seed-strategy-templates`
