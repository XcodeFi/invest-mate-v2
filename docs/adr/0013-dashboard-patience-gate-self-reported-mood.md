# ADR-0013 — Trang chủ chặn hành động dựa trên tâm trạng người dùng tự chấm

- **Status:** Accepted
- **Date:** 2026-08-11
- **Related plan:** `docs/plans/done/p1-dashboard-patience-hero.md`
- **Affected layers:** Domain / Application / Infrastructure / Api / Frontend

## Context

Chủ sở hữu ứng dụng đặt ra ba nguyên tắc, nguyên văn: *"Đầu tư chứng khoán chứ không phải là chơi chứng khoán"*, *"Tiền là của mình, nó là con số, nhưng mất tiền là thật"*, *"Khi cảm xúc vào, thì không hành động."*

Câu thứ ba là một luật dừng chứ không phải một lời nhắc. `/dashboard` hiện tại xếp chồng 11 khối số liệu và mở đầu bằng Hàng đợi quyết định — danh sách việc nên làm hôm nay. Không có bất kỳ khoảng lặng nào giữa lúc mở app và lúc bấm vào một hành động.

Ứng dụng đã có `DisciplineScoreCalculator` chấm kỷ luật theo dữ liệu (SL-Integrity, Plan Quality, Review Timeliness) và `BehavioralAnalysisService` phát hiện FOMO/PanicSell/RevengeTrading **sau khi việc đã xảy ra**. Cả hai đều là hậu kiểm. Chưa có gì can thiệp vào **trước** lúc hành động.

Ràng buộc: chỉ một người dùng thật, ưu tiên cải thiện quản trị rủi ro và kỷ luật hơn là mở rộng tính năng. Không được biến app thành thứ gây khó chịu tới mức người dùng bỏ dùng.

## Options Considered

### Option A — Chỉ nhắc, không chặn

Hero tĩnh với hoạt hoạ và châm ngôn xoay theo ngày. Không hỏi gì, không chặn gì.

- **Pros:**
  - Không bao giờ gây khó chịu, không có đường nào để người dùng bực và tắt đi
  - Thuần frontend, không schema, không endpoint, ship trong một buổi
  - Không có dữ liệu nhạy cảm nào phải lưu
- **Cons:**
  - Không thực hiện được nguyên tắc thứ ba. "Khi cảm xúc vào thì không hành động" mà lời nhắc không chạm được vào nút nào thì nó chỉ là trang trí
  - Sau vài ngày sẽ thành mù thị giác — người dùng lướt qua không đọc
  - Không tạo ra dữ liệu nào để sau này biết nó có tác dụng hay không

### Option B — Người dùng tự chấm tâm trạng, hệ thống phủ mờ Hàng đợi quyết định

Mỗi ngày hỏi một lần: Bình tĩnh / FOMO / Sợ / Cay cú. Chọn khác Bình tĩnh thì Hàng đợi quyết định bị phủ một lớp mờ, mở được bằng một cú bấm có ý thức, và cú bấm đó được ghi lại.

- **Pros:**
  - Thực hiện đúng nguyên tắc: có ma sát thật giữa cảm xúc và hành động
  - Hành động tự khai là một nghi thức dừng lại — bản thân việc phải đặt tên cho trạng thái của mình đã làm chậm nhịp
  - `OverrodeAt` cho một vòng phản hồi: đếm được bao nhiêu lần tự nhận có cảm xúc rồi vẫn đi tiếp
  - Không cấm gì, nên không có tình huống người dùng bị khoá khỏi tiền của chính mình
- **Cons:**
  - Tự khai thì tự lừa được. Chấm "Bình tĩnh" cho xong là hệ thống mù hoàn toàn
  - Thêm một collection, một index, bốn endpoint, và một loại dữ liệu riêng tư mới
  - Phủ chỉ ở dashboard; vào thẳng `/trade-plan` từ menu vẫn không bị chặn
  - Hỏi mỗi ngày là một thứ phải trả lời thêm, có thể thành phiền

### Option C — Suy ra từ hành vi, không hỏi

Không hỏi gì. Đọc số lệnh trong ngày, khoảng cách tới lần cắt lỗ gần nhất, điểm kỷ luật đang tụt — rồi tự kết luận người dùng đang có cảm xúc và phủ mờ.

- **Pros:**
  - Khách quan, không tự lừa được
  - Không bắt người dùng trả lời gì thêm
  - Tái dùng `DisciplineScoreCalculator` đã có
- **Cons:**
  - Chấm sai thì rất khó chịu và không cãi được — bị phủ mờ vì một suy đoán mình không đồng ý
  - Chỉ thấy được cảm xúc đã biến thành hành vi. FOMO trước khi đặt lệnh đầu tiên thì không có dấu vết nào để đọc — đúng lúc cần chặn nhất thì nó im
  - Cần định nghĩa ngưỡng cho từng tín hiệu, mỗi ngưỡng là một chỗ để sai
  - Bỏ mất tác dụng của chính nghi thức tự đặt tên cho trạng thái

## Decision

**Chọn Option B.**

Option A không làm được việc được giao. Option C nghe khách quan hơn nhưng mù đúng vào thời điểm quan trọng nhất: cơn FOMO tồn tại trước khi có lệnh nào để đọc, nên hệ thống chỉ phản ứng sau khi thiệt hại đã bắt đầu — hệt như `BehavioralAnalysisService` đang làm.

Đánh đổi chấp nhận: hệ thống chỉ trung thực bằng đúng mức người dùng trung thực với chính mình. Với ứng dụng một người dùng, tự viết cho mình, đó là đánh đổi hợp lý — người duy nhất bị thiệt khi nói dối là người nói dối. `OverrodeAt` giữ lại là để sau này còn kiểm chứng được giả định đó.

## Consequences

**Positive:**

- Nguyên tắc "khi cảm xúc vào thì không hành động" có một hiện thân chạm được vào giao diện, không còn là câu nói suông
- Có dữ liệu để về sau đối chiếu "những hôm chấm FOMO tôi đã làm gì" — ghép được với `BehavioralAnalysisService` đang có
- `OverrodeAt` là thước đo duy nhất trả lời được câu quan trọng nhất: luật dừng có tác dụng thật không. Bỏ nó đi thì tính năng này không bao giờ tự chứng minh được
- Widget Giao dịch nhanh bị gỡ khỏi trang chủ, bớt một lối đặt lệnh vội

**Negative / Trade-offs:**

- Tự khai có thể tự lừa. Không có cách nào phát hiện trực tiếp; chỉ suy gián tiếp qua tỷ lệ override
- Phủ chỉ áp cho Hàng đợi quyết định. Vào thẳng `/trade-plan` hoặc `/trade-wizard` từ menu vẫn không bị chặn — có ý thức chấp nhận ở bản này, mở rộng bằng route guard là việc riêng
- Thêm collection `mood_check_ins` + unique index `(UserId, DateKey)` cần tạo trên môi trường thật
- `DateKey` lưu dạng **chuỗi** `"YYYY-MM-DD"` chứ không phải `DateTime`. Dự án đã dính bẫy Mongo dịch nửa đêm giờ VN thành 17:00 hôm trước, làm lệch một ngày mọi so sánh mốc trong khi unit test vẫn xanh vì không đi vòng qua database. Đổi lại: không query khoảng ngày bằng toán tử ngày của Mongo được, phải so chuỗi — chấp nhận vì mọi truy vấn ở đây đều là khớp đúng một ngày
- "Hôm nay" do server tính (`UtcNow + 07:00`), frontend không gửi ngày lên. Đổi lại: máy người dùng ở múi giờ khác sẽ thấy ngày VN chứ không phải ngày địa phương — đúng ý, vì đây là app cho thị trường VN
- Người dùng phải trả lời thêm một câu mỗi ngày

**Follow-ups:**

- Migration: tạo unique index `(UserId, DateKey)` trên `mood_check_ins`, **đặt tên rõ `ux_user_datekey`** — repository phân biệt lỗi trùng bằng TÊN index chứ không bằng "có phải DuplicateKey không", để collection sau này thêm index unique thứ hai thì lỗi của nó không đội lốt trùng ngày. Collection mới nên không cần migrate dữ liệu
- Tests: hai ca ownership (lệnh và bản ghi tâm trạng của user khác không lọt vào) và ca ranh giới 00:30 giờ VN
- Docs: `architecture.md`, `business-domain.md`, `features.md`, `project-context.md`, CHANGELOG, user guide + mục Help
- Chưa làm: màn đối chiếu "hôm FOMO tôi đã làm gì", và route guard cho `/trade-plan`

## References

- Plan: `docs/plans/done/p1-dashboard-patience-hero.md`
- Spec: `docs/superpowers/specs/2026-08-11-dashboard-patience-hero-design.md`
- PR: #157
- Liên quan: ADR-0011 (gate hồ sơ công ty chặn lúc tạo plan) — cùng họ "chặn trước khi hành động", khác ở chỗ ADR-0011 chặn theo dữ liệu khách quan còn ADR-0013 chặn theo tự khai
