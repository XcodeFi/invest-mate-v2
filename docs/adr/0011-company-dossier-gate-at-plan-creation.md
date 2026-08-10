# ADR-0011 — Chặn tạo trade plan bằng hồ sơ công ty đã ký, chặn ngay lúc tạo

- **Status:** Accepted
- **Date:** 2026-08-10
- **Related plan:** `docs/superpowers/plans/2026-08-09-company-dossier-guard.md` (chặng 1 — entity + gate + trang hồ sơ)
- **Affected layers:** Domain / Application / Api / Frontend

## Context

App ép kỷ luật **giá** (stop-loss, risk budget) và kỷ luật **luận điểm vào lệnh** (`TradePlan.Thesis` + `InvalidationCriteria`, gate tại `Draft → Ready`), nhưng không ép kỷ luật **hiểu doanh nghiệp**. `Thesis` gắn với một lệnh và trả lời "vì sao mua LÚC NÀY" — không phải chỗ để ghi "doanh nghiệp này kiếm tiền bằng gì, moat ở đâu, cái gì phá được nó", thứ sống theo mã và theo quý. Gate `Thesis ≥ 30 ký tự` cũng chỉ đếm được độ dài, không đếm được hiểu biết: "HPG đầu ngành thép, triển vọng tốt" đủ 30 ký tự và không chứa thông tin kiểm chứng được nào.

Spec đầy đủ + bảng quyết định Q1–Q15: [`docs/superpowers/specs/2026-08-09-company-dossier-design.md`](../superpowers/specs/2026-08-09-company-dossier-design.md).

Aggregate mới `CompanyDossier` (khóa `UserId + Symbol`, sống độc lập với `TradePlan`) ghi lại hiểu biết đó một lần cho mỗi mã. ADR này ghi quyết định trung tâm — **chặn ở đâu trong lifecycle của plan** — và bảy quyết định phụ đi cùng nó, vì cả tám đều đi ngược tiền lệ hoặc trực giác trong codebase và người đọc code sau này sẽ tưởng là nhầm nếu không có chỗ giải thích.

## Options Considered

### Option A — Chặn ngay lúc tạo `TradePlan` (chọn)

- **Pros:**
  - Không cho một plan "trống hiểu biết" tồn tại dù chỉ ở trạng thái Draft — không có khoảng thời gian nào giữa lúc gõ entry/SL/TP và lúc buộc phải hiểu doanh nghiệp.
  - Vá luôn cửa hậu "tạo Draft rồi để đấy, không bao giờ chuyển Ready" — nếu chặn ở `Draft → Ready` như gate thesis, một Draft vô thời hạn vẫn có thể bị dùng làm nơi ghi chú giá không qua gate.
- **Cons:**
  - Đi ngược tiền lệ: gate thesis hiện có chặn ở transition, không chặn ở creation.
  - Nút "Tạo Trade Plan từ gợi ý" ở market-data không còn tạo được plan trực tiếp cho mã chưa có hồ sơ — phải điều hướng.
  - Khi giá đang chạm điểm mua mà hồ sơ chưa có, người dùng chịu áp lực viết vội.

### Option B — Chặn ở `Draft → Ready` (giữ tiền lệ gate thesis)

- **Pros:** Nhất quán với gate hiện có; Draft vẫn dùng được như nơi nháp tự do.
- **Cons:** Không chặn được việc dùng Draft như một dạng ghi chú giá không giới hạn thời gian; và tách thời điểm "viết entry/SL/TP" khỏi thời điểm "phải hiểu doanh nghiệp" xa nhau hơn cần thiết — user dễ điền đầy đủ giá trước, để đó, rồi hiểu doanh nghiệp thành thủ tục cuối cùng thay vì điều kiện đầu vào.

### Option C — Nhúng field hồ sơ trực tiếp vào `TradePlan` (không tạo aggregate mới)

- **Pros:** Không thêm entity, ship nhanh nhất.
- **Cons:** Không có khái niệm "hồ sơ cũ" sống theo mã — viết một lần cho HPG, mua HPG lần thứ năm vẫn phải gõ lại từ đầu; kỷ luật thành nghi thức copy-paste.

## Decision

**Chọn Option A.** Đây là quyết định của chủ sở hữu app, chấp nhận đánh đổi đã biết (xem Consequences) để đổi lấy việc không có khoảng hở nào trong lifecycle plan mà thiếu hiểu biết doanh nghiệp vẫn lọt qua.

Đi cùng quyết định trung tâm này là bảy quyết định phụ, mỗi cái đứng riêng đã đủ để ai đọc code sau này tưởng là bug nếu không ghi lại:

| # | Quyết định | Vì sao |
|---|---|---|
| D2 | **Agent viết được hồ sơ qua MCP, không xác nhận được.** `ConfirmedAt` chỉ đặt bởi `Confirm()`, chỉ với tới qua `POST /company-dossiers/{symbol}/confirm` (JWT) — không MCP tool nào đặt được nó. | Nếu agent vừa viết vừa xác nhận thì gate đo "Claude đã viết gì đó", không đo hiểu biết của người bỏ tiền. Đây là điểm tựa của toàn bộ thiết kế: một gate mà agent tự thỏa mãn được thì không đo được gì. |
| D3 | **Chỉ `Confirm()` đẩy `ReviewedAt`.** Sửa nội dung — kể cả người dùng tự sửa qua UI — không chạm đồng hồ hạn tươi. | Nếu sửa nội dung cũng đẩy đồng hồ, một hồ sơ đã `Expired` (180 ngày) chỉ cần sửa một ký tự trong ô ghi chú tự do là hồi sinh về `Fresh` mà không ai đọc tin mới, không ký lại gì — luật hết hạn thành vô nghĩa. Phân biệt theo **ai sửa** (Confirm vs Update), không theo **có sửa hay không**. |
| D4 | **Đổi `Symbol` khi sửa plan thì luôn chạy lại gate, chấm theo mã mới** — không cần vượt ngưỡng 5%. | Trỏ plan sang mã khác là mở vị thế mới ở một công ty khác, không phải điều chỉnh size. Đường sửa phải nhất quán với đường tạo (đường tạo chặn cả lệnh nhỏ theo D-BusinessModel-min). Đây cũng là lần thứ ba một "đầu vào của phép so đọc ở thời điểm sai" mở lại cửa hậu tương tự `Quantity`/`AccountBalance` partial-update — cùng một họ lỗi, vá cùng một cách: đọc giá trị **sau khi** áp field mới, không phải giá trị cũ trên entity đang load. |
| D5 | **Không có grandfathering, không có `LegacyExempt` tương đương.** Từ lúc deploy, mọi plan **mới** đều cần hồ sơ, kể cả mã đã giữ nhiều tháng. | Hệ quả trực tiếp của việc chặn ở creation: plan đang chạy được yên (không bị soi lại), nhưng plan mới không có ngoại lệ nào. Gate thesis từng có `LegacyExempt` cho giai đoạn chuyển tiếp; gate này cố ý không có, vì hồ sơ là thứ viết một lần dùng mãi (theo mã, không theo lệnh) chứ không phải gánh nặng lặp lại mỗi lệnh như thesis. |
| D6 | **Chặng 1 làm tắt đường ghi trade plan của agent**, cho tới khi chặng 2 có tool `upsert_company_dossier`. | Gate sống trên `CreateTradePlanCommand`, mà cả cửa ApiKey (`AiAgentController`) lẫn MCP đều dispatch vào đó — nên agent bị chặn với mọi mã và không có cách tự sửa cho tới khi chặng 2 phơi được `upsert_company_dossier`. Đây là lý do chặng 2 không được cắt. Thêm nữa, `DossierGateException` nổ trong một MCP tool không đi qua `ExceptionMiddleware` (middleware chỉ áp cho pipeline HTTP thường), nên agent hiện tại chỉ nhận câu thông báo lỗi và mất `missing[]` — chặng 2 phải xử lý riêng. |
| D7 | **`CompanyDossier` hardcode `TimeSpan.FromHours(7)`** cho ngày VN, khác với `GetPendingThesisReviewsQuery` dùng `TimeZoneInfo.FindSystemTimeZoneById` với chuỗi fallback kết thúc ở `TimeZoneInfo.Utc`. | Lệch có chủ đích, không phải quên đồng bộ convention: Việt Nam là offset cố định +07:00, không có DST từ 1975, nên `TimeSpan.FromHours(7)` luôn đúng. Sibling dùng `TimeZoneInfo` vì lịch sử, nhưng fallback `Utc` của nó sẽ dịch mốc ngày đi 7 giờ trên một host thiếu cả hai id múi giờ (`SE Asia Standard Time` và `Asia/Ho_Chi_Minh`) — tức là cách "nhất quán" lại là cách kém an toàn hơn ở đây. |
| D8 | **`Moats`/`RiskFactors` là `List<T> { get; private set; }`, không phải `IReadOnlyList` trên field `private readonly`.** | Nhượng bộ bắt buộc của MongoDB Driver 3.6.0: `IReadOnlyList` trên field `private readonly` deserialize về danh sách rỗng. Cái giá là caller ngoài entity có thể mutate list và bỏ qua bước `Normalize()` (dense rank, tối đa 1 deal-breaker) nếu gọi trực tiếp thay vì qua `UpdateByOwner`/`UpdateByAgent`. |

## Consequences

**Positive:**

- Không có khoảng hở lifecycle nào giữa "đã điền giá" và "đã hiểu doanh nghiệp" — plan không tồn tại được nếu thiếu cả hai.
- Viết hồ sơ một lần cho một mã, mọi plan sau cho mã đó dùng lại — không phải gõ lại "doanh nghiệp này kiếm tiền bằng gì" mỗi lần mua thêm.
- Tách rõ vai trò: agent hỗ trợ tra cứu/soạn nội dung, con người chịu trách nhiệm ký. Không đường nào (kể cả MCP ở chặng 2) đặt được `ConfirmedAt` ngoài endpoint JWT.

**Negative / Trade-offs:**

- Nút "Tạo Trade Plan từ gợi ý" ở market-data không tạo được plan trực tiếp cho mã chưa có hồ sơ đã ký — phải điều hướng sang `/company-dossier/{symbol}?returnTo=trade-plan`, giữ entry/SL/TP qua `sessionStorage`.
- Khi giá đang chạm điểm mua mà hồ sơ chưa có, người dùng chịu áp lực viết vội. Bước ký (D2) không giảm được áp lực này — chỉ chặng 2 (agent soạn hộ nội dung) mới giảm được.
- **Lệnh đầu tiên sau khi deploy chắc chắn bị chặn với mọi mã**, kể cả mã đang giữ nhiều tháng (hệ quả của D5). Đây không phải lỗi, nhưng phải thông báo trước khi deploy.
- Đường ghi trade plan của agent (ApiKey + MCP) **tắt hoàn toàn** cho tới khi chặng 2 landed (D6) — mọi request tạo plan qua agent nhận `DossierGateException` với `missing[]` bị mất do middleware không áp cho MCP.

**Follow-ups:**

- Chặng 2 (Task 9–11 của plan): phơi `IComprehensiveStockDataProvider` qua REST + MCP, thêm tool `upsert_company_dossier`/`get_company_fundamentals`/`get_dossier_gate_status`, và xử lý riêng `DossierGateException` trong pipeline MCP để agent không mất `missing[]`. Không được cắt (D6).
- Chặng 3 (Task 12–14): đề xuất `InvalidationRule` từ Top-3 `RiskFactor`, mục "Hồ sơ cần soát lại" ở `/pending-reviews`, badge dashboard. Có thể cắt nếu cần rút gọn.
- 3 hồ sơ test (VNM/MWG/HPG) còn trên DB prod từ vòng verify thủ công — không có endpoint DELETE, cần xóa tay qua Mongo nếu muốn dọn.

## References

- Plan: [`docs/superpowers/plans/2026-08-09-company-dossier-guard.md`](../superpowers/plans/2026-08-09-company-dossier-guard.md)
- Spec (Q1–Q15 đầy đủ): [`docs/superpowers/specs/2026-08-09-company-dossier-design.md`](../superpowers/specs/2026-08-09-company-dossier-design.md)
- Handoff: [`docs/handoffs/HANDOFF-2026-08-09-company-dossier-guard.md`](../handoffs/HANDOFF-2026-08-09-company-dossier-guard.md)
