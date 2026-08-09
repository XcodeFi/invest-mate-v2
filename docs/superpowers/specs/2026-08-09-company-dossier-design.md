# Hồ sơ công ty — điều kiện chặn trước khi lập kế hoạch mua

**Ngày:** 2026-08-09
**Trạng thái:** Chờ duyệt spec

## 1. Vấn đề

App đang ép kỷ luật ở hai tầng: kỷ luật **giá** (risk budget, tighten-only stop-loss) và kỷ luật **luận điểm vào lệnh** (`Thesis` + `InvalidationCriteria`, gate tại [TradePlan.cs:171](../../../src/InvestmentApp.Domain/Entities/TradePlan.cs#L171)). Không có tầng nào ép kỷ luật **hiểu doanh nghiệp**.

Hệ quả cụ thể:

1. **`Thesis` không phải chỗ để hiểu doanh nghiệp.** Nó gắn với một lệnh, sống vài tuần, trả lời "vì sao mua LÚC NÀY". Câu "doanh nghiệp này kiếm tiền bằng gì, lợi thế bền ở đâu, cái gì phá được nó" sống theo mã và theo quý — viết vào `Thesis` thì mua HPG lần thứ năm vẫn phải gõ lại từ đầu, và thực tế sẽ thành copy-paste.
2. **Gate hiện tại đếm được độ dài, không đếm được hiểu biết.** `Thesis ≥ 30 ký tự` chặn được ô rỗng, không chặn được "HPG đầu ngành thép, triển vọng tốt, kỳ vọng tăng giá" — đủ 45 ký tự và không chứa thông tin nào kiểm chứng được.
3. **Rủi ro doanh nghiệp không được liệt kê trước khi xuống tiền.** `InvalidationRule` có tồn tại, nhưng nó được viết *sau khi* đã quyết định mua — tức là đi tìm lý do bán cho một quyết định đã có, thay vì liệt kê rủi ro rồi mới quyết.
4. **Dữ liệu doanh nghiệp đã có nhưng không tới được nơi cần.** `IComprehensiveStockDataProvider` (24hmoney) trả P/E, P/B, ROE, ROA, EPS, doanh thu/lợi nhuận theo quý, cổ phiếu cùng ngành, cổ tức, kế hoạch kinh doanh, cơ cấu cổ đông, ban lãnh đạo, đơn vị kiểm toán. Toàn bộ khối này hiện **chỉ chảy vào `AiController /ai/comprehensive-analysis`**, mà endpoint đó stream ra văn bản AI chứ không trả số. Không endpoint REST nào và không MCP tool nào (trên 45 tool hiện có) trả được số thô.

## 2. Quyết định đã chốt

| # | Quyết định | Lý do |
|---|---|---|
| Q1 | **Một hồ sơ cho mỗi mã** (`CompanyDossier`, khóa `UserId` + `Symbol`), sống lâu, có hạn tươi. Không nhúng vào `TradePlan`. | Viết một lần cho HPG, mọi plan HPG sau dùng lại. Nhúng vào plan thì kỷ luật thành nghi thức gõ lại. |
| Q2 | **Xếp hạng rủi ro thủ công**, mỗi yếu tố **bắt buộc có dấu hiệu quan sát được**. Không dùng thang điểm Impact × Likelihood. | Mọi thang điểm tự cho mình đều là self-attestation — chính lý do quyết định R8 của plan Vin-discipline đã bỏ `AllocationBucket`. Dấu hiệu quan sát được thì kiểm chứng được. |
| Q3 | **Chặn ngay lúc tạo plan**, không chờ tới `Draft → Ready`. | Quyết định của chủ sở hữu app. Đánh đổi đã ghi ở mục 2.2. |
| Q4 | **Ngưỡng đủ theo size**, cùng công thức 5% tài khoản với gate hiện có. | Lệnh dò đường nhỏ không bị chặn; lệnh lớn bị ép đủ. Size là dữ kiện khách quan, không tự khai được. |
| Q5 | **90 ngày nhắc, 180 ngày hết hiệu lực.** Không neo theo kỳ BCTC ở V1. | Khớp nhịp báo cáo quý mà không phụ thuộc provider trả đúng kỳ. Provider lỗi không được biến thành "không mua được gì". |
| Q6 | **Chỉ guard plan mới.** Plan đang chạy không bị soi lại, kể cả khi hồ sơ hết hiệu lực. | Chặn plan đang chạy là tước quyền cứu lệnh lỗ — cùng lý do whitelist M4 của gate hiện có. |
| Q7 | **Luồng sửa plan chỉ bị kiểm tra khi size vượt ngưỡng 5%** (từ dưới lên trên). | Vá đúng cửa hậu "tạo nhỏ rồi sửa lớn" mà không phá nguyên tắc Q6. |
| Q8 | **Agent viết được hồ sơ qua MCP, nhưng không xác nhận được.** `ConfirmedAt` chỉ đặt được từ UI. | Nếu agent vừa viết vừa xác nhận thì gate đo "Claude đã viết gì đó", không đo hiểu biết của người bỏ tiền. Tách phần gõ khỏi phần chịu trách nhiệm. |
| Q9 | **Nội dung hồ sơ = 4 khối**: cỗ máy kiếm tiền · moat · rủi ro đã xếp hạng · ghi chú tự do (không gate). Số liệu 24hmoney hiển thị cạnh ô viết, **không tính vào gate**. | Nếu gate đếm "đã fetch được dữ liệu" thì provider trả 200 là mở được lệnh. |

### 2.1 Hướng đã cân nhắc và loại

- **Nhúng field vào `TradePlan`.** Không thêm entity, ship nhanh nhất — nhưng không có khái niệm "hồ sơ cũ", và buộc gõ lại mỗi lần mua cùng một mã.
- **Snapshot đóng băng hồ sơ vào plan lúc rời `Draft`.** Về sau review lệnh biết chính xác lúc mua mình hiểu gì — nhưng trùng dữ liệu và thêm versioning; để dành nếu nhu cầu review lịch sử xuất hiện thật.
- **Neo hạn tươi theo BCTC quý mới.** Đúng bản chất nhất, nhưng thêm job + cache + xử lý provider trả sai kỳ, và biến sự cố provider thành khóa cửa mua. Để V2.
- **Due-diligence đầy đủ (ban lãnh đạo, cơ cấu sở hữu, pha loãng, tập trung khách hàng, đòn bẩy, dòng tiền) làm điều kiện gate.** Đầy đủ nhất về phân tích, nhưng với một người dùng và trần 8h/tuần thì rủi ro lớn nhất là ngừng dùng — guard bị vô hiệu bằng cách không ai đi qua nó. Toàn bộ danh mục này về ô ghi chú tự do, không gate.

### 2.2 Đánh đổi đã biết của Q3, ghi lại để không ai tưởng là nhầm

Chặn ở lúc tạo plan đi ngược tiền lệ trong codebase (gate hiện tại chặn ở `Draft → Ready`). Hai hệ quả đã chấp nhận:

- Nút **"Tạo Trade Plan từ gợi ý"** ở market-data không còn tạo được plan trực tiếp cho mã chưa có hồ sơ. Xử lý ở mục 8.2 — điều hướng sang trang hồ sơ và giữ nguyên entry/SL/TP đã auto-fill.
- Khi giá đang chạm điểm mua mà hồ sơ chưa có, người dùng chịu áp lực viết vội. Đây là chi phí có thật của Q3. Bước xác nhận ở Q8 không giảm được áp lực này; MCP (mục 7) là cách giảm duy nhất.

### 2.3 Chốt thêm ở vòng review spec (2026-08-09)

| # | Quyết định | Lý do |
|---|---|---|
| Q10 | **Ký một lần là đủ; chỉ phải ký lại khi hồ sơ hết hiệu lực, hoặc khi agent sửa nội dung.** Người dùng tự sửa qua UI thì `ConfirmedAt` giữ nguyên. | Tự sửa thì đang đọc chính cái mình viết — bắt ký lại là nghi thức rỗng. Nhưng agent sửa thì người dùng chưa đọc bản mới, nên phải ký lại; nếu không thì `upsert_company_dossier` trên một hồ sơ đã ký chính là cửa hậu của Q8. Phân biệt theo **ai sửa**, không theo **có sửa hay không**. |
| Q11 | **Tối đa 1 `RiskFactor` được đánh dấu `IsDealBreaker`.** | `Rank` đã đo mức độ; cờ này chỉ cần trả lời "cái nào là công tắc bán hết". Cho đánh dấu nhiều thì từ "hủy diệt" mất nghĩa. |
| Q12 | **Tầng nhỏ vẫn bắt buộc `BusinessModel` không rỗng** (một câu là đủ). | Một câu ~15 giây, và đó là câu chặn "mua theo tin". Lệnh nhỏ hôm nay thường thành position lớn tháng sau do DCA — lúc đó gate luồng sửa mới bắt viết thì tiền đã vào rồi. |

## 3. Mô hình dữ liệu

Collection `company_dossiers`, field BSON **PascalCase** (project không đăng ký convention camelCase nào).

```csharp
CompanyDossier : AggregateRoot
    UserId         : string
    Symbol         : string          // tự ToUpper().Trim() như Trade/TradePlan
    BusinessModel  : string          // "doanh nghiệp kiếm tiền bằng gì"
    Moats          : List<MoatItem>
    RiskFactors    : List<RiskFactor>   // sắp theo Rank
    Notes          : string?            // tự do, không gate
    ReviewedAt     : DateTime
    ConfirmedAt    : DateTime?          // null = chưa ai ký; chỉ đặt được từ UI
    AgentDraftedAt : DateTime?          // MCP upsert đặt mốc này
    CreatedAt, UpdatedAt : DateTime

MoatItem                      // value object
    Description : string

RiskFactor                    // value object
    Rank             : int                      // 1 = nguy hiểm nhất, dense 1..N
    Description      : string
    ObservableSignal : string                   // "biết nó đang xảy ra bằng gì"
    IsDealBreaker    : bool                     // tối đa 1 mỗi hồ sơ
    SuggestedTrigger : InvalidationTrigger?
```

- `SuggestedTrigger` dùng lại **enum đã có** [`InvalidationTrigger`](../../../src/InvestmentApp.Domain/Entities/InvalidationRule.cs#L34) (`EarningsMiss` / `TrendBreak` / `NewsShock` / `ThesisTimeout` / `Manual`) — nhờ vậy một `RiskFactor` map 1:1 sang một `InvalidationRule` (mục 6).
- Index **unique** `(UserId, Symbol)`. Cả hai field luôn có giá trị nên không cần sparse.
- Collection property khai `List<T> { get; private set; }` — **không** dùng `IReadOnlyList` trên field `private readonly`, driver sẽ deserialize về rỗng.
- `Rank` phải **dense** (1..N, không khuyết) sau mọi thao tác thêm/xóa/đổi thứ tự. Entity chịu trách nhiệm chuẩn hóa, không để tầng gọi tự giữ.

### Bất biến của entity

| Bất biến | Vi phạm thì |
|---|---|
| `Symbol` đã normalize, không rỗng | `ArgumentException` |
| `ObservableSignal` của mọi `RiskFactor` không rỗng | `ArgumentException` — không có dấu hiệu thì không phải rủi ro, chỉ là nỗi lo |
| Tối đa 1 `RiskFactor` có `IsDealBreaker = true` (Q11) | `InvalidOperationException` |
| `Rank` dense 1..N | entity tự chuẩn hóa, không throw |
| Sửa nội dung **qua agent** ⇒ `ConfirmedAt = null`; sửa qua UI ⇒ giữ nguyên (Q10) | — (xem mục 4) |

Entity phải phơi **hai phương thức sửa nội dung riêng biệt** — ví dụ `UpdateByOwner(...)` và `UpdateByAgent(...)` — chứ không phải một phương thức nhận cờ `isAgent`. Cờ boolean truyền từ ngoài vào sớm muộn sẽ có chỗ gọi truyền sai; hai phương thức thì gọi sai là compile được nhưng đọc code thấy ngay.

## 4. Hạn tươi và bước ký

Tính server-side, **day-granularity theo `Asia/Ho_Chi_Minh`** bằng `TimeZoneInfo` — cùng cách `GetPendingThesisReviewsQuery` đang làm.

| Trạng thái | Điều kiện | Hệ quả |
|---|---|---|
| `Unconfirmed` | `ConfirmedAt == null` | Gate coi như **chưa có hồ sơ** |
| `Fresh` | đã ký, `ReviewedAt + 90 ngày > hôm nay` | Qua gate nếu đủ nội dung |
| `NeedsReview` | `ReviewedAt + 90 ngày ≤ hôm nay` | Vẫn qua gate, nhưng hiện ở `/pending-reviews` + badge |
| `Expired` | `ReviewedAt + 180 ngày ≤ hôm nay` | **Chặn tạo plan mới** cho mã đó |

`ConfirmedAt` **chỉ được đặt bởi đúng một phương thức** `Confirm()`, và phương thức đó chỉ với tới được qua `POST /company-dossiers/{symbol}/confirm` (JWT). Không đường nào khác đặt được nó — đó là cách thực thi Q8 ở tầng domain thay vì trông vào kỷ luật của tầng gọi.

**Chỉ `Confirm()` đẩy đồng hồ hạn tươi.** Không thao tác nào khác chạm `ReviewedAt`. Nếu sửa nội dung cũng đẩy đồng hồ thì hồ sơ đã `Expired` chỉ cần sửa một ký tự trong ô ghi chú tự do là quay về `Fresh` — không đọc tin nào, không ký gì, đúng cái mốc 180 ngày sinh ra để chặn.

| Hành động | `ReviewedAt` | `ConfirmedAt` |
|---|---|---|
| Người dùng sửa nội dung — `PUT` (JWT) | **giữ nguyên** | **giữ nguyên** |
| Agent sửa nội dung — `upsert_company_dossier` (MCP) | **giữ nguyên** | **về `null`**, đặt `AgentDraftedAt = now` |
| `POST .../confirm` — nút ký | `now` | `now` |

Ba hệ quả của Q10:

- Người dùng tự sửa thì không phải ký lại — đang đọc chính cái mình viết. Hồ sơ đang `Fresh` sửa xong vẫn `Fresh`.
- Agent sửa thì hồ sơ tụt về `Unconfirmed`, gate chặn cho tới khi người dùng mở trang, đọc, ký. Trang chi tiết phải hiển thị rõ **"Agent đã cập nhật lúc … — chưa xác nhận"**, để không ai tưởng đã lưu là đã xong.
- Hồ sơ `Expired` (180 ngày) phải ký lại mới về `Fresh`, **kể cả khi người dùng vừa sửa nội dung**. Sửa không đẩy đồng hồ, nên hồ sơ hết hạn vẫn hết hạn cho tới lúc bấm ký. Ở trạng thái này nhãn nút đổi thành **"Đã cập nhật tin mới và xác nhận"** thay vì "Vẫn đúng" — vì mục đích của lần ký này là xác nhận đã soát tin mới, không phải xác nhận nội dung cũ vẫn đúng.

## 5. Gate

### 5.1 Vị trí

Gate nằm ở **Application layer**, không nằm trong entity `TradePlan` — nó cần đọc một aggregate khác, mà entity không được phép fetch repository.

| Điểm bắn | Khi nào |
|---|---|
| `CreateTradePlanCommandHandler` ([CreateTradePlanCommand.cs:69](../../../src/InvestmentApp.Application/TradePlans/Commands/CreateTradePlan/CreateTradePlanCommand.cs#L69)) | **Đầu** `Handle`, trước khi construct entity |
| `UpdateTradePlanCommandHandler` | Chỉ khi **tỷ lệ** cũ `< 5%` **và** tỷ lệ mới `≥ 5%` |

Điều được so là **tỷ lệ**, không phải size tuyệt đối — và cả hai vế phải dùng đúng số dư của thời điểm tương ứng:

```
oldRatio  dùng plan.Quantity × plan.EntryPrice        / plan.AccountBalance
newRatio  dùng (request.Quantity ?? plan.Quantity)
          × (request.EntryPrice ?? plan.EntryPrice)   / (request.AccountBalance ?? plan.AccountBalance)
```

Hai chỗ dễ sai, cả hai đều mở lại đúng cửa hậu mà luồng này sinh ra để bịt:

- **Update là partial.** `Quantity` là `int?`, `EntryPrice` là `decimal?`, và `TradePlan.Update` chỉ gán khi `HasValue`. Không fallback về giá trị cũ thì sửa mỗi `Quantity` sẽ cho size bằng 0 và gate không bao giờ bắn.
- **`AccountBalance` cũng nằm trong cùng payload sửa.** Tính ngưỡng theo số dư cũ thì một request vừa nâng size vừa hạ số dư sẽ lọt: plan 2M trên 100M, sửa lên 4M kèm hạ số dư còn 50M → ngưỡng cũ 5M nên 4M < 5M, gate không bắn, còn tỷ lệ thật là 8%. Form FE gửi lại toàn bộ field mỗi lần lưu nên đây không phải trường hợp hiếm.

`AccountBalance` null hoặc ≤ 0 ở **cả hai** thời điểm ⇒ không có ngưỡng nào để vượt ⇒ không chạy gate. Đây là hệ quả có ý thức của Q4 (không biết số dư thì coi như lệnh nhỏ), không phải bỏ sót.

Đặt ở đầu `Handle` là có chủ đích: nhánh auto-transition `Draft → Ready → InProgress` khi `request.Status == "Executed"` ([cùng file, dòng 156–163](../../../src/InvestmentApp.Application/TradePlans/Commands/CreateTradePlan/CreateTradePlanCommand.cs#L156-L163)) nằm *sau* điểm bắn, nên tự động được bao. Không có đường lách qua wizard.

### 5.2 Ngưỡng

Phản chiếu đúng công thức `EnsureDisciplineGate` ([TradePlan.cs:177-180](../../../src/InvestmentApp.Domain/Entities/TradePlan.cs#L177-L180)):

```
requireFullDiscipline = AccountBalance.HasValue
                     && AccountBalance.Value > 0
                     && Quantity × EntryPrice >= AccountBalance.Value × 0.05
```

`AccountBalance` null **hoặc ≤ 0** ⇒ coi như tầng nhỏ. Điều kiện `> 0` là bắt buộc, không phải phòng xa: thiếu nó thì `AccountBalance = 0` cho `threshold = 0`, mọi lệnh đều `>= 0`, nên **mọi** lệnh rơi vào tầng lớn — trong khi số dư bằng 0 nghĩa là chưa biết gì, đúng như null. Hai gate phải khớp nhau ở điểm này, nếu không cùng một lệnh sẽ bị hai gate phân loại ngược nhau.

| | Nhỏ (`size < 5%` hoặc không biết số dư) | Lớn (`size ≥ 5%`) |
|---|---|---|
| `BusinessModel` | không rỗng | ≥ 30 ký tự |
| `Moats` | ≥ 1 | ≥ 1, `Description` ≥ 30 ký tự |
| `RiskFactors` | ≥ 1, có `ObservableSignal` | ≥ 3, mỗi `ObservableSignal` ≥ 20 ký tự |
| Trạng thái hồ sơ | đã ký, chưa `Expired` | đã ký, chưa `Expired` |

Đếm ký tự phải có test với **tiếng Việt có dấu** — cùng lý do test #10 của gate hiện tại tồn tại.

### 5.3 Lỗi trả về

Gate throw `DossierGateException : InvalidOperationException` mang theo kết quả đánh giá. `ExceptionMiddleware` cần **một nhánh riêng đặt trước switch chung**, vì switch hiện tại map `InvalidOperationException → 409 Conflict` (đã kiểm tra [ExceptionMiddleware.cs:62](../../../src/InvestmentApp.Api/Middleware/ExceptionMiddleware.cs#L62)) — không phải 400. Kế thừa từ `InvalidOperationException` để nếu nhánh mới bị xóa thì hành vi thoái về 409 chứ không thành 500.

```json
{
  "code": "DOSSIER_GATE_FAILED",
  "symbol": "HPG",
  "reason": "missing | unconfirmed | expired | insufficient",
  "missing": ["riskFactors: cần ≥ 3, đang có 1", "businessModel: cần ≥ 30 ký tự, đang có 12"]
}
```

`missing` phải nói **cần bao nhiêu và đang có bao nhiêu**. Thông báo "chưa đủ hồ sơ" không nói thiếu gì sẽ buộc người dùng đoán.

### 5.4 Không có retro-check

Plan tạo trước ngày deploy không bị kiểm tra lại, không cần cờ legacy trên `TradePlan`. Gate chỉ tồn tại trên đường tạo mới và đường sửa-vượt-ngưỡng. Mọi phương thức điều khiển rủi ro của plan đang chạy (`UpdateStopLossWithHistory`, `TriggerScenarioNode`, `TriggerExitTarget`, `ExecuteLot`, `AbortWithThesisInvalidation`) **không** bị chạm tới.

## 6. Hồ sơ trả lại thời gian nó lấy

Đây là phần làm guard đáng đi qua thay vì đáng ghét, và là lý do `SuggestedTrigger` tồn tại.

Khi lập plan cho mã **đã có hồ sơ đã ký**, form đề xuất sẵn danh sách `InvalidationRule` sinh từ **Top-3 `RiskFactor`**:

```
Trigger = RiskFactor.SuggestedTrigger ?? Manual
Detail  = $"{Description} — dấu hiệu: {ObservableSignal}"
```

Người dùng **tick cái nào muốn dùng**, không auto-add. Viết hồ sơ một lần, mọi plan sau nhanh hơn hiện tại — kể cả so với trước khi có feature này, vì phần `InvalidationCriteria` đang phải gõ tay mỗi lần.

`Detail` sinh ra phải đạt ngưỡng ≥ 20 ký tự của gate hiện có. Với `ObservableSignal` ≥ 20 ký tự ở tầng lớn thì luôn đạt; ở tầng nhỏ có thể không, khi đó form hiển thị nguyên văn để người dùng tự bổ sung thay vì lặng lẽ tạo một rule bị từ chối.

## 7. Phơi dữ liệu doanh nghiệp

Một query duy nhất, expose hai cửa — đúng pattern sibling-surface-per-auth-scheme project đang dùng:

| Cửa | Đường | Auth | Dùng cho |
|---|---|---|---|
| REST | `GET /api/v1/market/stock/{symbol}/fundamentals` | JWT | Panel số liệu cạnh ô viết (mục 8.1) |
| MCP | `get_company_fundamentals` | ApiKey | Agent lấy số trước khi viết hồ sơ |

`GetCompanyFundamentalsQuery(symbol)` bọc `IComprehensiveStockDataProvider`, trả DTO map từ `ComprehensiveStockData` (company overview, indicators, income statements theo quý, peers, cổ tức, kế hoạch kinh doanh, analyst reports, giao dịch NN).

Hai chi tiết phải đúng ngay từ đầu:

1. `using InvestmentApp.Application.Interfaces` — **không** phải `Application.Common.Interfaces`. File nằm ở `Common/Interfaces/` nhưng namespace khai là `Application.Interfaces`.
2. `NoOpFundamentalDataProvider` tồn tại trong Infrastructure. Nếu provider chưa cấu hình, `Company` và `Indicators` về null. Query phải **trả lỗi rõ ràng** khi cả hai null, không trả object rỗng — nếu không agent sẽ viết hồ sơ từ null và sinh ra một hồ sơ đủ hình thức mà rỗng nội dung, tức là qua được gate mà không có hiểu biết nào.

Dữ liệu một phần cũng phải nói rõ là một phần: nếu `IncomeStatements` rỗng trong khi `Indicators` có, DTO phải mang cờ cho biết phần nào không lấy được, không để agent hoặc UI hiểu "rỗng" là "bằng không".

## 8. Frontend

### 8.1 Trang hồ sơ

- `/company-dossier` (danh sách, kèm trạng thái tươi và thiếu gì) và `/company-dossier/:symbol` (chi tiết).
- Standalone component, inline template, `ngModel`, `appUppercase` cho ô symbol. Toàn bộ nhãn tiếng Việt có dấu đầy đủ.
- Panel số liệu 24hmoney **nằm cạnh ô viết**, không chồng lên, không tính vào gate.
- Xếp hạng rủi ro bằng nút **▲▼**, không thêm CDK drag-drop.
- Nút ký: **"Tôi đã đọc và chịu trách nhiệm"**. Đặt ở cuối trang, sau nội dung — không đặt cạnh nút lưu, để nó không bị bấm theo phản xạ.
- Modal nào có nút này thì thứ tự nút là `[Hủy] → [primary]`, primary bên phải.

### 8.2 Luồng bị Q3 làm gãy

Nút "Tạo Trade Plan từ gợi ý" ở market-data: nếu mã chưa có hồ sơ đã ký → điều hướng `/company-dossier/{symbol}?returnTo=trade-plan`, entry/SL/TP đã auto-fill giữ trong `sessionStorage`, quay lại không mất gì.

### 8.3 Nơi khác

- `/pending-reviews` (đã có) thêm mục **"Hồ sơ cần soát lại"** (`NeedsReview` và `Expired`, `Expired` xếp trước).
- Dashboard: badge số lượng, ẩn khi bằng 0.
- Form Trade Plan: banner đỏ khi nhận 400 `DOSSIER_GATE_FAILED`, liệt kê `missing`, kèm link sang trang hồ sơ.

## 9. MCP surface

File mới `src/InvestmentApp.Api/Mcp/CompanyDossierTools.cs` — static class, param phẳng, `IMediator` + `IHttpContextAccessor`, `http.GetUserId()`, đúng khuôn [DecisionTools.cs](../../../src/InvestmentApp.Api/Mcp/DecisionTools.cs).

| Tool | Loại | Ghi chú |
|---|---|---|
| `list_company_dossiers` | ReadOnly | kèm trạng thái tươi + thiếu gì để qua gate |
| `get_company_dossier` | ReadOnly | `symbol` |
| `get_company_fundamentals` | ReadOnly | mục 7 |
| `upsert_company_dossier` | write | đặt `AgentDraftedAt`, **không** đặt `ConfirmedAt` |
| `get_dossier_gate_status` | ReadOnly | `symbol`, tùy chọn `quantity`/`entryPrice` → cho agent biết trước lệnh này có qua được không, khỏi tạo rồi ăn 400 |

**Không có tool xác nhận hồ sơ.** Đây là điểm tựa của Q8, không phải sơ suất — ai đọc code sau này cũng phải hiểu là cố ý.

Ba bẫy MCP của project này phải tránh ngay khi viết:

1. **Param phẳng ở tầng ngoài.** `symbol`, `businessModel`, `moats`, `riskFactors` là param riêng biệt — không bọc trong một object `command`. `riskFactors` là mảng object thì bình thường; cái phải tránh là một wrapper bọc tất cả, vì nó làm mọi caller phẳng fail cùng lúc.
2. **Param tùy chọn đặt sau `ct` và phải có `= null`.** Nullable một mình không đủ — thiếu default là schema đánh dấu `required`.
3. **Mọi service inject phải được đăng ký DI**, nếu không cả object graph của nó rò vào `inputSchema`.

Kèm một **discovery test assert thẳng vào `InputSchema` thô**: mảng `required` của `upsert_company_dossier` chỉ chứa các field bắt buộc thật, và không tồn tại key `command`.

## 10. Kiểm thử (TDD, viết trước implement)

**Domain** — `CompanyDossierTests.cs`, `CompanyDossierFreshnessTests.cs`

1. Symbol normalize `" hpg "` → `"HPG"`; rỗng → throw.
2. `RiskFactor` thiếu `ObservableSignal` → throw.
3. Thêm `RiskFactor` thứ hai với `IsDealBreaker = true` → throw.
4. Xóa `RiskFactor` giữa danh sách → `Rank` dense lại 1..N.
5. Đổi thứ tự → `Rank` khớp thứ tự mới.
6. `UpdateByAgent(...)` trên hồ sơ đã ký → `ConfirmedAt` về null, `AgentDraftedAt` được đặt (Q10).
7. `UpdateByOwner(...)` trên hồ sơ đã ký → `ConfirmedAt` **giữ nguyên**, `ReviewedAt` được đặt lại (Q10).
8. `Confirm()` → `ReviewedAt` và `ConfirmedAt` cùng `now`.
9. `Confirm()` trên hồ sơ `Expired` → về `Fresh`.
10. Biên hạn tươi: 89 / 90 / 179 / 180 ngày → `Fresh` / `NeedsReview` / `NeedsReview` / `Expired`.
11. `ConfirmedAt == null` → `Unconfirmed` bất kể `ReviewedAt` mới thế nào.

**Application** — `CompanyDossierGateTests.cs`

12. Không có hồ sơ → `reason = "missing"`.
13. Có hồ sơ đủ nội dung nhưng `ConfirmedAt == null` → `reason = "unconfirmed"`.
14. Hồ sơ 180 ngày → `reason = "expired"`.
15. Hồ sơ đã ký, agent vừa `upsert` → `reason = "unconfirmed"`, gate chặn (Q10 — cửa hậu đã bịt).
16. Hồ sơ đã ký, người dùng vừa `PUT` → qua gate, không bắt ký lại (Q10).
17. Tầng nhỏ: `BusinessModel` không rỗng + 1 moat + 1 rủi ro có dấu hiệu → qua.
18. Tầng nhỏ thiếu `BusinessModel` → chặn (Q12).
19. Tầng lớn: 2 rủi ro → chặn, `missing` nói "cần ≥ 3, đang có 2".
20. Tầng lớn: `ObservableSignal` 19 ký tự → chặn.
21. `AccountBalance` null → áp tầng nhỏ.
22. `BusinessModel` tiếng Việt có dấu 30 ký tự → qua (không bị đếm lệch).
23. `CreateTradePlanCommand` với `Status = "Executed"` → gate chạy trước auto-transition, không lách được.
24. `UpdateTradePlanCommand` sửa size từ 2% lên 12%, hồ sơ mỏng → chặn.
25. `UpdateTradePlanCommand` sửa size từ 2% lên 3% → **không** chạy gate.
26. `UpdateTradePlanCommand` trên plan đang `InProgress`, không đổi size → không chạy gate.
27. `GetCompanyFundamentalsQuery` khi provider trả null cả `Company` và `Indicators` → lỗi rõ ràng, không trả object rỗng.
28. `GetCompanyFundamentalsQuery` khi `IncomeStatements` rỗng nhưng `Indicators` có → DTO đánh dấu phần thiếu.
29. Đề xuất `InvalidationRule` từ Top-3 `RiskFactor` — đúng thứ tự Rank, `Detail` ghép đúng format.

**Api**

30. `POST /trade-plans` mã chưa có hồ sơ → 400, body đúng shape mục 5.3.
31. `POST /company-dossiers` trùng `(UserId, Symbol)` → 409, không phải 500.
32. `POST /company-dossiers/{symbol}/confirm` → `ConfirmedAt` được đặt.
33. `GET /market/stock/{symbol}/fundamentals` trả đủ các nhóm của mục 7.
34. Discovery test: `InputSchema` thô của `upsert_company_dossier` — `required` đúng danh sách, không có key `command`.
35. Discovery test: **không tồn tại** MCP tool nào đặt được `ConfirmedAt` (Q8 — assert theo danh sách tool, để lần sau ai thêm tool confirm là test đỏ).

**Frontend** — 4–6 spec: banner `DOSSIER_GATE_FAILED` liệt kê đúng `missing`; nút ▲▼ đổi thứ tự; nút ký disabled khi nội dung chưa đủ; luồng `returnTo=trade-plan` giữ được entry/SL/TP; nhãn nút ký đổi thành "Đã cập nhật tin mới và xác nhận" khi hồ sơ `Expired`; hiển thị "Agent đã cập nhật lúc … — chưa xác nhận".

## 11. Tài liệu phải cập nhật trước khi commit

- [`docs/architecture.md`](../../architecture.md) — entity `CompanyDossier`, repository, controller, MCP tool file, trang FE mới.
- [`docs/business-domain.md`](../../business-domain.md) — entity map + quan hệ `CompanyDossier` ↔ `TradePlan`, quy tắc gate.
- [`docs/features.md`](../../features.md) — section mới "Hồ sơ công ty & điều kiện chặn lập kế hoạch".
- [`docs/project-context.md`](../../project-context.md) — quyết định UX Q3 và đánh đổi của nó.
- `frontend/src/assets/CHANGELOG.md` — version thực tế khi ship.
- `frontend/src/assets/docs/` — hướng dẫn người dùng: cách viết moat và rủi ro có dấu hiệu quan sát được, kèm ví dụ thị trường VN. **Phải đăng ký Help topic**, không chỉ thêm file.
- **ADR mới** `docs/adr/0010-company-dossier-gate-at-plan-creation.md` — chặn ở lúc tạo thay vì lúc arm, đi ngược tiền lệ đang có; và quyết định agent viết được nhưng không ký được. Cả hai đều là trade-off cross-layer, không ghi lại thì người đọc code sau tưởng là nhầm.

## 12. Thứ tự triển khai đề nghị

Spec này lớn hơn một lần ship gọn, và trần dev là 8h/tuần. Ba chặng, mỗi chặng tự dùng được:

1. **Entity + gate + trang hồ sơ.** Hết chặng này guard đã hoạt động, nhưng còn phải gõ tay.
2. **Phơi fundamentals + MCP.** Hết chặng này agent điền hộ được — đây là chặng làm guard hết đau.
3. **Đề xuất `InvalidationRule` từ Top-3 + `/pending-reviews` + badge.** Phần trả lại thời gian và phần nhắc soát lại.

Nếu phải cắt, cắt chặng 3. Không cắt chặng 2 — thiếu nó thì chặng 1 là một cái cửa chặn không có chìa.

## 13. Ngoài phạm vi V1

- Auto-detect hết tươi theo kỳ BCTC mới (Q5 để dành).
- Snapshot hồ sơ đóng băng vào plan lúc arm.
- Điểm số chất lượng hồ sơ / xếp hạng hồ sơ giữa các mã.
- Gate trên ô ghi chú tự do.
- Chặn hoặc soi lại plan đang chạy.
- Đưa các mục due-diligence (ban lãnh đạo, cơ cấu sở hữu, pha loãng, tập trung khách hàng, đòn bẩy, dòng tiền) thành điều kiện gate.
