# Project Guidelines — Investment Mate v2

## Tài liệu tham chiếu (đọc theo thứ tự ưu tiên)

1. **Architecture & codebase map:** [`docs/architecture.md`](docs/architecture.md) — đọc TRƯỚC khi làm bất kỳ thay đổi nào. Chứa directory structure, key files, service dependencies, API endpoints.
2. **Nghiệp vụ & Entity map:** [`docs/business-domain.md`](docs/business-domain.md) — entity relationships, business rules, external APIs.
3. **Project context & decisions:** [`docs/project-context.md`](docs/project-context.md) — goals, UX decisions, improvement plan, known pitfalls.
4. **Tính năng theo phase:** [`docs/features.md`](docs/features.md)
5. **Coding patterns:** [`.github/copilot-instructions.md`](.github/copilot-instructions.md)

## Vietnamese Text (UI)

- **Luôn viết tiếng Việt có dấu đầy đủ** cho tất cả text hiển thị: labels, buttons, placeholders, messages, errors, notifications, tooltips.
- Không bao giờ viết không dấu (VD: ~~"Sap xep"~~ → "Sắp xếp", ~~"Tat ca"~~ → "Tất cả").
- Kiểm tra lại trước khi commit.

## Symbol Input

- Mọi input nhập mã chứng khoán (symbol) phải dùng `appUppercase` directive (`UppercaseDirective` trong `shared/directives/uppercase.directive.ts`).
- Không dùng CSS class `uppercase` hay inline `toUpperCase()` cho symbol inputs.
- Backend entity (Trade, TradePlan) đã tự normalize `ToUpper().Trim()` — đây là lớp bảo vệ cuối.

## Tech Stack

- **Frontend:** Angular 19, standalone components, inline templates, Tailwind CSS, ngModel (template-driven forms)
- **Backend:** .NET 9, Clean Architecture (Domain → Application → Infrastructure → Api), MongoDB (Driver 3.6.0)
- **Shared directives:** `NumMaskDirective` (số có dấu phân cách), `UppercaseDirective` (uppercase symbol)
- **Pipes:** `VndCurrencyPipe` (format tiền VND)

## TDD (Test-Driven Development)

- **Bắt buộc viết unit test trước** khi thêm feature mới hoặc thay đổi business logic.
- Quy trình: **Red → Green → Refactor**
  1. Viết test cho behavior mong muốn (test fail)
  2. Implement code để pass test
  3. Refactor nếu cần
- **Backend tests:** xUnit + FluentAssertions + Moq, trong thư mục `tests/`
  - `InvestmentApp.Domain.Tests` — entity, value object tests
  - `InvestmentApp.Application.Tests` — command/query handler tests (Moq repositories)
  - `InvestmentApp.Infrastructure.Tests` — service tests (Moq dependencies)
- **Frontend tests:** Karma + Jasmine (`.spec.ts` files)
- Chạy `dotnet test` trước khi commit để đảm bảo tất cả tests pass.
- **Sau mỗi fix bug hoặc thêm feature:** chạy lại tests liên quan để verify không regression. Nếu sửa Domain → chạy `dotnet test tests/InvestmentApp.Domain.Tests`, sửa Application → chạy `dotnet test tests/InvestmentApp.Application.Tests`, sửa Infrastructure → chạy `dotnet test tests/InvestmentApp.Infrastructure.Tests`.

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

- **Cập nhật architecture** — thêm/xóa service, controller, repository, feature page, shared component, external integration → update [`docs/architecture.md`](docs/architecture.md)
- **Cập nhật project context** — phát hiện bug pattern mới, hoàn thành improvement plan item, quyết định UX/architecture quan trọng → update [`docs/project-context.md`](docs/project-context.md)
