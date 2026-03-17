# Project Guidelines — Investment Mate v2

## Tài liệu tham chiếu

- **Nghiệp vụ & Entity map:** [`docs/business-domain.md`](docs/business-domain.md) — đọc file này trước khi làm bất kỳ thay đổi nào liên quan đến logic nghiệp vụ.
- **Tính năng theo phase:** [`docs/features.md`](docs/features.md)
- **Architecture & patterns:** [`.github/copilot-instructions.md`](.github/copilot-instructions.md)

## Vietnamese Text (UI)

- **Luôn viết tiếng Việt có dấu đầy đủ** cho tất cả text hiển thị: labels, buttons, placeholders, messages, errors, notifications, tooltips.
- Không bao giờ viết không dấu (VD: ~~"Sap xep"~~ → "Sắp xếp", ~~"Tat ca"~~ → "Tất cả").
- Kiểm tra lại trước khi commit.

## Symbol Input

- Mọi input nhập mã chứng khoán (symbol) phải dùng `appUppercase` directive (`UppercaseDirective` trong `shared/directives/uppercase.directive.ts`).
- Không dùng CSS class `uppercase` hay inline `toUpperCase()` cho symbol inputs.
- Backend entity (Trade, TradePlan) đã tự normalize `ToUpper().Trim()` — đây là lớp bảo vệ cuối.

## Tech Stack

- **Frontend:** Angular 18, standalone components, inline templates, Tailwind CSS, ngModel (template-driven forms)
- **Backend:** .NET 9, Clean Architecture (Domain → Application → Infrastructure → Api), MongoDB
- **Shared directives:** `NumMaskDirective` (số có dấu phân cách), `UppercaseDirective` (uppercase symbol)
- **Pipes:** `VndCurrencyPipe` (format tiền VND)

## Code Conventions

- Inline templates trong `@Component({ template: \`...\` })` — không dùng file `.html` riêng.
- Services nằm trong `core/services/`, shared code trong `shared/`.
- Commit message bằng tiếng Anh, UI text bằng tiếng Việt có dấu.

## Trước khi Commit

- **Cập nhật tài liệu liên quan** mỗi khi thay đổi code:
  - Thêm/sửa entity, API, route → update [`docs/business-domain.md`](docs/business-domain.md)
  - Thêm/sửa tính năng → update [`docs/features.md`](docs/features.md)
  - Thêm/sửa convention, directive, pipe → update `CLAUDE.md` (file này)
  - Release/bugfix → update [`frontend/src/assets/CHANGELOG.md`](frontend/src/assets/CHANGELOG.md)
- Không commit nếu tài liệu chưa đồng bộ với code.
