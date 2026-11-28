# Công cụ hỗ trợ

> Watchlist, nhật ký giao dịch, nhiệm vụ hàng ngày, và trợ lý AI.

---

## Watchlist (`/watchlist`)

Theo dõi cổ phiếu quan tâm trước khi giao dịch:

- **Tạo nhiều danh sách**: VD: "Cổ phiếu theo dõi", "Chờ mua", "VN30"
- **Thêm mã**: Tìm kiếm autocomplete, thêm nhanh
- **Giá realtime**: Giá, % thay đổi, khối lượng cho mỗi mã
- **Import VN30**: Nhập 30 mã VN30 bằng 1 click
- **Ghi chú & giá mục tiêu**: Note + target buy/sell cho từng mã
- **Tạo Trade Plan**: Nút nhanh → `/trade-plan?symbol=X`

---

## Nhật ký giao dịch (`/journals`)

Ghi chép suy nghĩ và cảm xúc mỗi giao dịch:

### 5 loại nhật ký

1. **Pre-trade**: Phân tích trước khi vào lệnh
2. **Post-trade**: Đánh giá sau khi đóng lệnh
3. **Daily Review**: Tổng kết ngày giao dịch
4. **Market Observation**: Quan sát thị trường
5. **Lesson Learned**: Bài học rút ra

### Thông tin ghi nhận

- **Cảm xúc**: Bình tĩnh / Lo lắng / Hưng phấn / FOMO / Sợ hãi
- **Mức tự tin**: 1–10
- **Setup kỹ thuật**: Mô tả điều kiện kỹ thuật
- **Liên kết giao dịch**: Gắn vào trade cụ thể (tùy chọn)
- **Snapshot giá**: Tự lưu giá CP tại thời điểm ghi

### AI phân tích nhật ký

Nút **"AI Phân tích"** → AI đọc 20 nhật ký gần nhất → phân tích:
- Xu hướng cảm xúc
- Pattern hành vi (FOMO, revenge trading?)
- Gợi ý cải thiện tâm lý giao dịch

---

## Nhiệm vụ hàng ngày (`/daily-routine`)

### 5 Template sẵn có

| Template | Thời gian | Số bước | Phù hợp khi |
|----------|:---------:|:-------:|-------------|
| **Swing Trading** | ~30 phút | 12 | Ngày giao dịch bình thường |
| **DCA** | ~15 phút | 8 | Ngày mua DCA theo lịch |
| **Research** | ~45 phút | 10 | Cuối tuần — tìm mã mới |
| **Onboarding** | ~20 phút | 8 | Lần đầu sử dụng app |
| **Crisis** | ~15 phút | 8 | Thị trường giảm mạnh |

### Tính năng đặc biệt

- **Auto-suggest**: Gợi ý template theo ngữ cảnh (VN-Index giảm → Crisis, cuối tuần → Research)
- **Deep links**: Mỗi item có link đến trang liên quan
- **Streak 🔥**: Đếm ngày liên tiếp hoàn thành → gamification
- **Custom template**: Tạo mẫu riêng theo nhu cầu
- **Heatmap 30 ngày**: Xanh = hoàn thành, Vàng = một phần, Xám = chưa làm

---

## Trợ lý AI

### Cách dùng

1. Nhấn nút **AI** trên thanh header (hoặc nút AI trên mỗi trang)
2. Panel chat trượt ra từ bên phải
3. AI tự lấy context từ trang đang xem
4. Trả lời streaming (real-time)
5. Hỗ trợ follow-up questions

### 12 use case

| Trang | AI làm gì |
|-------|-----------|
| Nhật ký | Phân tích pattern cảm xúc & hành vi |
| Danh mục | Đánh giá phân bổ & hiệu suất |
| Kế hoạch GD | Tư vấn entry/SL/TP + risk compliance |
| Báo cáo tháng | Tổng kết hiệu suất + gợi ý cải thiện |
| Thị trường | Đánh giá cổ phiếu (kỹ thuật + cơ bản) |
| Risk Dashboard | Phân tích rủi ro tổng thể |
| Vị thế | Tư vấn hold/sell cho từng vị thế |
| Giao dịch | Phân tích pattern win/loss |
| Watchlist | Quét cơ hội giao dịch |
| Dashboard | Bản tin hàng ngày |

### Dùng không cần API key

Nhấn nút **"Copy Prompt"** → AI tạo prompt đầy đủ + copy vào clipboard → dán vào Claude/Gemini web app. Hoàn toàn miễn phí.

### Cài đặt AI (`/ai-settings`)

- Chọn provider: **Claude** (Anthropic) hoặc **Gemini** (Google)
- Nhập API key riêng cho từng provider
- Chọn model (Sonnet/Opus hoặc Flash/Pro)
- Test kết nối
- Xem thống kê sử dụng token

---

## Symbol Timeline (`/symbol-timeline/:symbol`)

Xem lịch sử toàn bộ hoạt động liên quan đến 1 mã CP:
- Nhật ký, giao dịch, sự kiện thị trường, cảnh báo
- Sắp xếp theo thời gian

### Biểu đồ chỉ báo trên dòng thời gian
Biểu đồ ở đầu trang giống trang **Phân tích thị trường**: bấm một nhóm chỉ báo (Tổng hợp, Xu hướng, Động lượng, Biến động, Dòng tiền, Mốc giá) để vẽ nhóm đó, bấm lại để bỏ. Dưới biểu đồ có khối **📖 Cách đọc** của nhóm đang chọn (đọc hướng dẫn đầy đủ ở mục Phân tích thị trường).
- Nến là nến thật đã điều chỉnh theo sự kiện quyền (cổ tức, chia thưởng, phát hành thêm). Nếu thấy ghi chú "chưa điều chỉnh" hoặc "cập nhật tới ngày …" thì nguồn dữ liệu đang có sự cố.
- **Mốc trên biểu đồ:** T = giao dịch (mũi tên lên là mua, xuống là bán), J = nhật ký, E = sự kiện, A = cảnh báo, H = hồ sơ công ty. Ngày không có phiên (cuối tuần, lễ) thì mốc nằm ở phiên liền trước. Trỏ chuột vào phiên có mốc để xem chi tiết.
- **Giá giao dịch là giá gốc.** Nến đã điều chỉnh nên có thể thấp hơn giá bạn đã khớp nếu sau đó có cổ tức, chia thưởng hoặc phát hành thêm. Khi giá bạn khớp nằm ngoài khoảng giá của phiên đó trên biểu đồ, mốc ghi "(giá gốc)": vị trí mốc vẫn đúng ngày, chỉ mặt bằng giá khác nhau. Nhãn này đến từ việc so giá giao dịch với khoảng giá của phiên, nên một điều chỉnh nhỏ (ví dụ cổ tức tiền mặt nhỏ) có thể không hiện nhãn.
- Các nút **1T / 3T / 6T / 1N** đổi khoảng hiển thị; chỉ báo luôn được tính trên cùng một cửa sổ dài hơn khoảng hiển thị (nên đầu khoảng không bị trống và giá trị phiên cuối không đổi khi đổi khoảng).
- Tầng **Cảm xúc** (cột màu ở dưới cùng) xuất hiện khi bạn có nhật ký ghi cảm xúc: độ cao cột là mức tự tin, màu là cảm xúc.

### Đánh giá sau khi bán (post-trade review)
Sau khi có lệnh BÁN, lệnh sẽ hiện ở Dashboard card "Chờ đánh giá" và trong trang Giao dịch với biểu tượng ✎ vàng ("Chưa đánh giá"). Bấm vào để mở Symbol Timeline ở chế độ review:
- Banner cam hiện lên ghi rõ giao dịch đang được đánh giá (loại, số lượng, giá, ngày).
- Form nhật ký tự mở, tiêu đề + giá + thời điểm đã được điền sẵn, loại nhật ký đặt là **"Sau giao dịch"** (PostTrade).
- Chỉ cần điền cảm xúc + nhận định, bấm **Lưu đánh giá**.
- Sau khi lưu: dấu ✎ vàng chuyển thành ✓ xanh ("Đã đánh giá") ở trang Giao dịch, và lệnh biến mất khỏi card "Chờ đánh giá" ở Dashboard.

Nếu bấm Hủy (✕ trên banner hoặc nút Hủy) sẽ thoát chế độ đánh giá và xóa `tradeId` khỏi URL.

---

## Chiến lược (`/strategies`)

Thư viện chiến lược giao dịch:
- Tạo chiến lược với tên, mô tả, khung thời gian
- SL% và R:R gợi ý → tự điền vào Trade Plan
- Theo dõi hiệu suất: Win rate, P&L theo từng chiến lược
