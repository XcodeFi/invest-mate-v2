# Bắt đầu sử dụng Investment Mate

> Hướng dẫn nhanh giúp bạn bắt đầu quản lý danh mục đầu tư chỉ trong vài phút.

---

## Tổng quan ứng dụng

Investment Mate là công cụ quản lý giao dịch chứng khoán dành cho nhà đầu tư cá nhân. Ứng dụng giúp bạn:

- **Theo dõi danh mục** — tổng giá trị, lãi/lỗ, hiệu suất theo thời gian
- **Giao dịch có kỷ luật** — wizard 5 bước, kế hoạch giao dịch, checklist
- **Phân tích kỹ thuật** — 10 chỉ báo tự động, tín hiệu mua/bán
- **Quản lý rủi ro** — stop-loss, position sizing, cảnh báo drawdown
- **Trợ lý AI** — phân tích danh mục, tư vấn giao dịch, bản tin hàng ngày

---

## Bước 1: Tạo danh mục đầu tiên

1. Vào trang **Danh mục** (`/portfolios`)
2. Nhấn **"Tạo danh mục"**
3. Điền tên (VD: "Danh mục chính") và vốn ban đầu
4. Nhấn **Lưu**

Danh mục sẽ xuất hiện trên Dashboard và sẵn sàng để ghi giao dịch.

---

## Bước 2: Thiết lập Risk Profile

Trước khi giao dịch, thiết lập hồ sơ rủi ro cá nhân:

1. Vào **Risk Dashboard** (`/risk-dashboard`) hoặc **Rủi ro chi tiết** (`/risk`)
2. Thiết lập:
   - **% rủi ro mỗi lệnh**: thường 1–2% tổng vốn
   - **% tối đa một vị thế**: thường 10–20% danh mục
   - **Drawdown tối đa chấp nhận**: thường 10–15%
3. Lưu lại — hệ thống sẽ cảnh báo khi bạn vượt giới hạn

---

## Bước 3: Ghi giao dịch đầu tiên

Có 2 cách ghi giao dịch:

### Cách 1: Nhanh — Trang Giao dịch
- Vào **Giao dịch** (`/trades`) → **Tạo mới**
- Chọn danh mục, nhập mã CP, chọn Mua/Bán, điền giá + số lượng
- Hệ thống tự tính phí giao dịch

### Cách 2: Kỷ luật — Wizard 5 bước
- Vào **Wizard GD** (`/trade-wizard`)
- Bước 1: Chọn chiến lược (hoặc bỏ qua)
- Bước 2: Lập kế hoạch (Entry, SL, TP, Position Size)
- Bước 3: Hoàn thành checklist GO/NO-GO
- Bước 4: Xác nhận & ghi giao dịch
- Bước 5: Viết nhật ký

---

## Bước 4: Xem Dashboard

Quay lại **Dashboard** (`/dashboard`) để xem:

- **🎣 Màn tĩnh tâm (đầu trang)**: một khoảng lặng trước khi bạn bấm vào bất cứ thứ gì.
  - **Cảnh người đi câu** phản ánh **số ngày bạn chưa đặt lệnh**. Vừa giao dịch xong thì nước còn động; càng lâu không động tay, mặt hồ càng phẳng và trời càng ngả hoàng hôn. Số ngày hiển thị luôn là số thật.
  - **Châm ngôn** đổi theo trạng thái bạn tự chấm.
  - **Mỗi ngày một câu hỏi**: *Giờ anh đang thế nào?* — Bình tĩnh / FOMO (sợ bỏ lỡ) / Sợ / Cay cú.
  - Chấm khác **Bình tĩnh** → Việc cần xử lý hôm nay bị **phủ mờ**, kèm câu *"Danh sách này tối nay vẫn ở đây."* Bạn vẫn xem được, nhưng phải bấm thêm một lần. Không ai cấm bạn — chỉ là bắt bạn dừng lại nửa giây.
  - Bấm **đổi** chỉ mở lại bảng chọn, lớp phủ vẫn còn. Và nếu bạn chấm Bình tĩnh rồi quay lại FOMO thì phải bấm qua lớp phủ lại từ đầu.
  - Tâm trạng lưu theo tài khoản nên mở máy khác vẫn thấy, sang ngày mới thì hỏi lại.
- **🚨 Decision Queue (vị trí #2)**: Việc cần xử lý hôm nay — gộp Stop-loss / Scenario trigger / Thesis review thành 1 list. Mỗi item có 2 button:
  - **🔪 BÁN THEO KẾ HOẠCH** — mở màn hình ghi lệnh bán với form đã điền sẵn theo kế hoạch (mã, danh mục, giá hiện tại, số lượng). Bạn sửa lại số lượng muốn bán rồi mới bấm lưu — không có gì được ghi lúc bấm nút này. Chỉ hiện khi thẻ có gắn Kế hoạch.
  - **✋ GIỮ + GHI LÝ DO** — bắt buộc nhập ≥ 20 ký tự để buộc nghĩ kỹ trước khi bỏ qua tín hiệu.
  - Empty state: khi không có alert → `✅ Hôm nay đang kỷ luật + 🔥 streak X ngày`.
- **NetWorth + Reality Gap CAGR**: cảnh báo lệch so với mục tiêu CAGR.
- **Discipline Score**: điểm kỷ luật thesis.
- **Summary cards**: Tổng giá trị, Đã đầu tư, Lãi/Lỗ, CAGR.
- **Vị thế nổi bật**: Top 6 cổ phiếu đang nắm giữ.
- **Watchlist**: phá pre-trade routine, kỷ luật entry.
- Equity Curve đầy đủ ở trang `/analytics`. Chỉ số thị trường (VNINDEX/HNX/UPCOM/VN30) ở trang `/market-data`.

---

## Bước 5: Thiết lập nhiệm vụ hàng ngày

Vào **Nhiệm vụ ngày** (`/daily-routine`):

- Chọn template phù hợp (Swing Trading, DCA, Research, hoặc tự tạo)
- Hệ thống tự gợi ý template theo ngữ cảnh (thị trường giảm → Crisis, cuối tuần → Research)
- Hoàn thành nhiệm vụ hàng ngày để xây dựng kỷ luật giao dịch
- Theo dõi streak 🔥 (số ngày liên tiếp hoàn thành)

---

## Các trang chính

| Trang | Mô tả |
|-------|-------|
| Dashboard | Tổng quan danh mục, equity curve, cảnh báo |
| Giao dịch | Lịch sử mua/bán, import CSV |
| Kế hoạch GD | Lập kế hoạch entry/SL/TP, checklist |
| Thị trường | Tra cứu giá, phân tích kỹ thuật 10 chỉ báo |
| Phân tích | Equity curve, win rate, monthly returns |
| Risk Dashboard | Drawdown, VaR, cảnh báo rủi ro |
| Watchlist | Theo dõi cổ phiếu quan tâm |
| Nhật ký | Ghi chép suy nghĩ, cảm xúc mỗi giao dịch |
| AI Assistant | Trợ lý thông minh phân tích danh mục |

---

## Mẹo sử dụng

- **Giá tự động**: Nhập mã CP → hệ thống tự lấy giá hiện tại từ 24hmoney
- **Phím tắt**: Mã CP luôn tự động viết HOA — không cần Caps Lock
- **Mobile**: Ứng dụng hoạt động tốt trên điện thoại — cài PWA để dùng như app
- **AI miễn phí**: Dùng nút "Copy Prompt" để dùng với Claude/Gemini mà không cần API key
