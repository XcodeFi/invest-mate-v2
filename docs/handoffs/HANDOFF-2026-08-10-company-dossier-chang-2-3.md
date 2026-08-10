# Handoff — Hồ sơ công ty chặng 2 + chặng 3 (2026-08-10)

Tiếp nối [`HANDOFF-2026-08-09-company-dossier-guard.md`](HANDOFF-2026-08-09-company-dossier-guard.md) (chặng 1).

## Đã xong

| PR | Nội dung | Trạng thái |
|---|---|---|
| #148 | Hạn mức tập trung ngành 40% bắt đầu hoạt động + tỷ trọng ngành trên form | merged `021d989` |
| #149 | 4 tool MCP hồ sơ công ty + menu + truyền nguyên văn lỗi lưu | merged `ba51070` |
| #150 | Số liệu doanh nghiệp qua REST + MCP, panel cạnh ô viết (chặng 2) | merged `7d4e44f` |
| #151 | Đề xuất điều kiện lý do sai từ hồ sơ + nhắc soát lại (chặng 3) | **OPEN, CI xanh, MERGEABLE CLEAN** |

Cổng hồ sơ công ty **đã trọn cả ba chặng**. ADR-0011 đã đóng trade-off D6 và follow-up chặng 2.

Test: **1797 backend + 204 frontend** (baseline đầu ngày: 1742 + 171).

## Điều đáng nhớ nhất của ngày

**Test không bắt được lỗi nào trong ba lỗi nghiêm trọng nhất — nguồn thật mới bắt.**

| Ca | Hành vi sai | Vì sao test không thấy |
|---|---|---|
| 24hmoney trả HPG 10 sự kiện cổ tức **mọi field null** | Đếm `Count` → coi là có dữ liệu → UI hiện 10 dòng gạch ngang | Test dựng `null` để nghĩa là "thiếu" — đúng giá trị production không bao giờ sinh ra |
| Mã sai (ZZZZ) | Provider trả đủ hai object với mọi field null, **không** trả null → guard `== null` không bao giờ bắn → cửa 404 là **code chết** | Cùng lý do trên |
| Luật 40% tập trung ngành | Chưa từng cảnh báo lần nào từ khi tồn tại: provider tra ngành là NoOp, mọi mã vào rổ "Không xác định", rổ đó hardcode `IsOverweight = false` | Test phủ công thức, không phủ đường dây |

Rút ra: với mọi upstream tổng hợp (scraper, wrapper nhiều lệnh gọi), **"có dữ liệu" phải chấm theo NỘI DUNG, không theo null-ness**, và phải dùng MỘT vị từ dùng chung cho mọi phần — vị từ viết tay theo từng loại sẽ lệch, và loại bị bỏ sót chính là loại làm rò vỏ rỗng.

Đã lưu memory: `learning_pitfall_empty_shell_is_not_absent`, `learning_toolquirk_dotnet_test_skips_locked_project`.

## Cạm bẫy tooling phải biết trước khi chạy test

**API đang chạy sẽ khoá DLL và `dotnet test` bỏ qua `Api.Tests` trong IM LẶNG** (MSB3026 → MSB3021/MSB3027), trong khi các project khác vẫn in `Passed!`. Grep `Passed!|Failed!` lọc mất đúng dòng MSB giải thích khoảng trống đó. Cách kiểm: **đếm số project** trong output (phải có 4) và grep thêm `error MSB`. Tổng test giảm so với lần trước = coi như có project bị bỏ, tới khi chứng minh ngược lại.

## Quyết định thiết kế đã chốt trong hai chặng

- `get_company_fundamentals` (MCP) **không nhận `UserId`** — dữ liệu thị trường chung. Có test ghim.
- Số liệu doanh nghiệp **không** vào điều kiện cổng, chỉ là nguyên liệu. Panel ghi rõ.
- Đề xuất điều kiện lý do sai: **đề xuất, không tự áp**. Không tick sẵn, không tự thêm.
- Trạng thái "đã thêm" đọc **trực tiếp** từ `plan.invalidationCriteria`, không giữ Set song song — Set mirror lệch ở ba đường (xoá tay, resetForm, mở plan đã có điều kiện y hệt).
- Ngưỡng 90 ngày + phép lệch giờ VN sống ở `CompanyDossier.DaysOverdueForReview()`, **một bản duy nhất**. Nhánh này đã một lần phải gộp bản sao thứ tư của ngưỡng 5% (`66acadb`).
- `DaysOverdue` của hồ sơ chưa ký = **0**: đồng hồ hạn tươi chưa chạy.
- Badge dashboard **ẩn hoàn toàn khi bằng 0** — một dòng "0 hồ sơ" mỗi ngày dạy người ta bỏ qua chỗ đó.
- `HasAnyValue` coi giá trị **mặc định** của value type là "không có thông tin". Với property không nullable (`Shareholder.Percentage`, `ForeignTradingDay.BuyVolume`) thì `0` và "không có" là cùng một bit — chọn phía này vì một số 0 thật bị báo "không lấy được" chỉ mất một dòng, còn vỏ rỗng được coi là dữ liệu thì bịa ra cả một khối cổ đông.

## Việc kế tiếp — đã có plan riêng

[`docs/superpowers/plans/2026-08-10-dossier-symbol-links-and-timeline.md`](../superpowers/plans/2026-08-10-dossier-symbol-links-and-timeline.md) — **viết cho session mới**, 5 task:

1. `SymbolLinkDirective` — mã chứng khoán bấm được sang `/symbol-timeline/:symbol` (đích đã chốt với người dùng).
2. Khảo sát + phân loại mọi chỗ hiển thị mã (3 loại, chỉ 2 loại nên gắn link).
3. Áp directive theo bảng — có cạm bẫy "quên `imports` thì Angular bỏ qua directive trong im lặng".
4. Mốc hồ sơ trong timeline (backend) — **giới hạn đã biết: `CompanyDossier` không lưu lịch sử**, nên timeline chỉ dựng được 2 mốc gần nhất, không phải lịch sử tiến hoá luận điểm.
5. Render mốc + tài liệu.

## Còn treo, chưa có plan

- **Snapshot hồ sơ theo từng lần ký** — điều kiện để timeline có lịch sử thật, và cũng là điều kiện để đóng băng hồ sơ vào plan lúc arm.
- **Dropdown nhóm ngành 24hmoney** — dữ kiện đầu tiên nói **chưa cần**: 5/5 mã thử đều tra ra ngành. Nếu làm thì đảo quyết định Q6 của spec gốc → phải sửa Q6 **tại chỗ**, không thêm ghi chú ở cuối.
- **Race `ux_user_symbol`** gây 500 khi tạo hồ sơ trùng (find-then-insert).
- Neo hạn tươi theo kỳ BCTC mới.
- Đưa các mục due-diligence (ban lãnh đạo, cơ cấu sở hữu, pha loãng, tập trung khách hàng, đòn bẩy, dòng tiền) thành điều kiện gate.

## Việc bảo mật cần người quyết

JWT của tài khoản test (`investmate.support@gmail.com`) đã lộ vào transcript nhiều lần trong ngày qua các lần dump network header. Token **hợp lệ tới 2026-09-09** và **không thu hồi được** mà không rotate `Jwt:Key` trên prod (sẽ đăng xuất mọi người dùng). Hai khoá API `imk_` đã lộ thì đã thu hồi xong (`stillActiveKeys: 0`).
