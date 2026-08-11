# Handoff — 2026-08-11 · Lệch hợp đồng 24hmoney + chia sẻ hồ sơ công ty

## Đã ship

| PR | Nội dung | Trạng thái |
|---|---|---|
| [#156](https://github.com/XcodeFi/invest-mate-v2/pull/156) | Hồ sơ công ty: chế độ xem, form dễ gõ, cầu nối clipboard với AI ngoài | Merged |
| [#158](https://github.com/XcodeFi/invest-mate-v2/pull/158) | Bắt lại hợp đồng 24hmoney — 6/8 khối số liệu hỏng âm thầm | Merged |
| [#159](https://github.com/XcodeFi/invest-mate-v2/pull/159) | Chia sẻ hồ sơ công ty với tài khoản khác qua clipboard | Merged |

## Điều quan trọng nhất rút ra

**Panel số liệu hồ sơ công ty đã hỏng nhiều tháng mà không ai biết.** Nhà cung cấp đổi cấu trúc response; code khai theo cấu trúc cũ; mỗi hàm fetch `catch → LogWarning → return null` nên dữ liệu mất trông y hệt "mã này không có số liệu". `AiAssistantService` ăn cùng nguồn nên bản tóm tắt AI cũng suy luận trên dữ liệu thiếu.

Nó lộ ra **tình cờ** — từ log của một server nền còn sót lại sau lần verify trước, chứ không phải từ test hay giám sát.

Báo cáo QA ngày 2026-08-10 đã ghi đúng triệu chứng nhưng xếp là *"[có sẵn, không liên quan]"*. Bài học: một mục "không liên quan" nói rằng **mọi mã đều thiếu dữ liệu** thì không phải là không liên quan.

## Việc còn nợ (chưa có PR)

1. **Bỏ kiểu nuốt lỗi âm thầm ở `HmoneyComprehensiveDataProvider`.** Đây là gốc thật, không phải mấy dòng mapping. Cần phân biệt được "lệch hợp đồng" với "nguồn không có dữ liệu", và cảnh báo khi tỷ lệ khối hỏng vượt ngưỡng. Chi tiết trong [`docs/plans/done/p1-hmoney-contract-drift.md`](../plans/done/p1-hmoney-contract-drift.md).
2. **Rà lại prompt digest của `AiAssistantService`** sau khi đổi shape — xem còn chỗ nào mô tả sai field không.
3. **Repo đang PUBLIC** (`XcodeFi/invest-mate-v2`). Tên nhà cung cấp nằm trong 28 file code, 6 tên file, một namespace và một thư mục; fixture chứa response thật của họ. Xoá tên khỏi tài liệu **không** giấu được gì — chuyển repo sang private mới là biện pháp thật. Đã trao đổi, chưa quyết.
4. **Nhánh remote đã merge chưa xoá** — 20 nhánh trên `origin`. Local đã dọn sạch. Chưa xoá remote vì đó là tài nguyên chung.

## Ghi chú vận hành

- `appsettings.Development` trỏ DB **`InvestmentApp_prod`**. Mọi verify trong phiên này chỉ `GET`; không bấm Lưu, không bấm Ký.
- Fixture 24hmoney nằm ở `tests/InvestmentApp.Infrastructure.Tests/Fixtures/Hmoney/`. Nguồn đổi tiếp thì `curl` lại và ghi đè. Các trường dữ liệu cá nhân của lãnh đạo (ngày sinh, tuổi, học vấn, địa chỉ) đã **xoá trắng** — giữ khoá để cấu trúc không đổi.
- Báo cáo QA: `scratch/qa-reports/qa-verify-hmoney-contract-drift-20260811-0755z.md` và `qa-verify-dossier-share-20260811-0840z.md` (thư mục gitignored).

## Bài học đã lưu vào memory toàn cục

- `learning_pitfall_handwritten_fixture_hides_contract_drift` — fixture tự bịa ghim lại hợp đồng mình tưởng tượng, drift không bao giờ đỏ.
- `learning_pitfall_redaction_noop_on_unrecognised_input` — hàm che trả nguyên văn khi không nhận ra định dạng; cho nhầm field vào là lộ sạch, im lặng.

## Bắt đầu lại từ đâu

Việc còn nợ số 1 là món đáng làm nhất: nó ngăn đúng cái lỗi vừa mất cả buổi để tìm ra tái diễn. Phạm vi gọn — một provider, một kiểu xử lý lỗi, không đụng schema.
