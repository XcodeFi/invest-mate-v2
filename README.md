# Investment Mate v2 - Hệ thống Quản lý Danh mục Đầu tư Cá nhân

[![.NET](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/)
[![MongoDB](https://img.shields.io/badge/MongoDB-7.0-green.svg)](https://www.mongodb.com/)
[![Angular](https://img.shields.io/badge/Angular-19-red.svg)](https://angular.io/)
[![License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Ứng dụng quản lý danh mục đầu tư cá nhân, xây dựng theo kiến trúc Clean Architecture, CQRS, và Domain-Driven Design (DDD). Hỗ trợ xác thực Google OAuth, tính toán P&L theo phương pháp chi phí trung bình, và xử lý nền bằng job theo lịch.

Mục tiêu của app không phải "hiển thị số liệu" mà là **ép giữ kỷ luật đầu tư**: mọi lệnh phải đi qua kế hoạch có luận điểm (thesis) và điều kiện thoát, mọi vi phạm rủi ro đều bị chặn hoặc bắt xác nhận.

## ✨ Tính năng Chính

> Xem chi tiết đầy đủ tại [docs/features.md](docs/features.md)

### 🧙 Wizard Giao dịch (5 bước)

- **Quy trình kỷ luật**: Chiến lược → Kế hoạch → Checklist → Ghi GD → Nhật ký
- **GO/NO-GO enforcement**: Không thể bỏ qua checklist bắt buộc
- **Auto-fill giá**: Nhập mã CP → tự điền giá hiện tại từ API

### 📊 Dashboard Decision Engine

- **Decision Queue**: Gom việc cần xử lý (chạm stop-loss, thiếu SL, cơ hội mua, thesis quá hạn review) và cho hành động ngay tại chỗ
- **4 Summary Cards**: Tổng giá trị, Vốn đầu tư, P&L, CAGR
- **Compound Growth Tracker**: CAGR thực tế vs mục tiêu, ước tính 5/10/20 năm
- **Mini Equity Curve**: Line chart Chart.js với range filter 30D/90D/1Y/All
- **Risk Alert Banner**: Stop-loss proximity, drawdown, cảnh báo tập trung danh mục
- **Điểm kỷ luật**: SL-Integrity, Plan Quality, Review Timeliness + streak giữ kỷ luật

### 📋 Kế hoạch Giao dịch với Template

- **Thesis & Invalidation**: Bắt buộc viết luận điểm + điều kiện phá vỡ trước khi rời Draft (gate theo size lệnh)
- **Auto-fill giá**: Debounced symbol lookup → điền Entry Price tự động
- **Position Sizing tự động**: Tính từ Risk Profile (maxRisk%, maxPosition%)
- **Template save/load**: Lưu kế hoạch thành template → tái sử dụng với 1 click
- **Scenario Playbook**: Cây kịch bản nếu/thì cho từng kế hoạch
- **Risk violations enforcement**: Cảnh báo vi phạm + yêu cầu xác nhận

### 📈 Analytics & Báo cáo

- **Bar chart P&L**: Lãi/lỗ theo cổ phiếu
- **Donut chart**: Phân bổ danh mục
- **Equity Curve**: Đường tăng trưởng vốn theo ngày
- **Monthly Returns Matrix**: Hiệu suất theo năm × tháng, color-coded
- **TWR / MWR / CAGR**: Hiệu suất đã khử ảnh hưởng dòng vốn nạp/rút
- **So sánh với gửi tiết kiệm**: Chi phí cơ hội so với lãi suất ngân hàng
- **Monthly Review** (`/monthly-review`): Báo cáo tháng tự động
- **Campaign Review**: Đóng chiến dịch và phân tích hiệu suất theo chiến lược

### 🛡️ Quản lý Rủi ro

- **Risk Profile**: Max position%, max risk/lệnh, R:R tối thiểu, max drawdown alert
- **Risk Dashboard**: Tổng quan sức khỏe rủi ro, stop-loss tracking, correlation
- **Concentration Alert**: Tự động cảnh báo khi cổ phiếu vượt giới hạn Risk Profile
- **Stress Test**: Mô phỏng kịch bản VNINDEX với beta động
- **Risk Budgeting**: Giới hạn số lệnh và mức rủi ro theo ngày

### 💰 P&L & Portfolio

- **Average Cost Method**: Phương pháp chi phí trung bình chuẩn xác
- **Realized vs Unrealized P&L**: Phân biệt lãi/lỗ đã và chưa thực hiện
- **Capital Flows**: Theo dõi dòng vốn vào/ra
- **Daily Snapshots**: Lịch sử snapshot cho Equity Curve

### 🏦 Tài chính cá nhân

- **Net worth**: Gộp chứng khoán, tiền gửi, vàng, tiền nhàn rỗi và nợ
- **Sổ tiết kiệm**: Ngày gửi/đáo hạn, lãi suất, nhắc đáo hạn
- **Nợ**: Theo dõi dư nợ và lịch trả

### 🤖 AI & Agent

- **Trợ lý AI đa nhà cung cấp**: Claude + Gemini cho phân tích và bản tin hằng ngày
- **MCP server**: Bộ tool đọc/ghi để agent bên ngoài truy cập danh mục, kế hoạch, rủi ro
- **Personal Access Token**: Khóa API theo user, có hạn dùng và thu hồi được

### 🔐 Xác thực & Bảo mật

- **Google OAuth 2.0**: Đăng nhập an toàn với tài khoản Google
- **JWT Tokens**: Quản lý phiên làm việc bảo mật
- **User-scoped data**: Templates, Risk Profiles, Journals tách biệt theo user

### 🏗️ Kiến trúc Kỹ thuật

- **Clean Architecture**: Tách biệt rõ ràng các layer
- **CQRS + MediatR**: Command Query Responsibility Segregation
- **Domain-Driven Design**: Thiết kế theo domain business
- **Job theo lịch**: Tính P&L và chụp snapshot chạy nền qua endpoint nội bộ

## 🛠️ Công nghệ Sử dụng

### Backend (.NET 9)
- **ASP.NET Core Web API**: RESTful API endpoints
- **MediatR**: CQRS implementation
- **MongoDB**: NoSQL database với indexing tối ưu
- **FluentValidation**: Validation pipeline
- **Serilog**: Structured logging
- **JWT Bearer Authentication**: Token-based security
- **ModelContextProtocol**: MCP server phục vụ AI agent

### Frontend (Angular 19)
- **Angular 19**: Standalone components, inline templates
- **TypeScript**: Type-safe development
- **RxJS**: Reactive programming
- **Tailwind CSS**: Styling chính
- **Angular Material**: Một số UI component
- **Chart.js**: Data visualization

### Infrastructure
- **Docker**: Containerization (`Dockerfile.api`, `docker-compose.yml`)
- **MongoDB**: Document database
- **GitHub Actions**: CI (`.github/workflows/ci.yml`)

## 📁 Cấu trúc Dự án

```
InvestmentApp.sln
├── src/
│   ├── InvestmentApp.Api/           # API Layer - Controllers, Middleware, MCP tools
│   ├── InvestmentApp.Application/   # Application Layer - Commands, Queries, Services
│   ├── InvestmentApp.Domain/        # Domain Layer - Entities, Value Objects, Events
│   └── InvestmentApp.Infrastructure/# Infrastructure Layer - Repositories, External Services
├── frontend/                        # Angular 19 SPA
├── tests/                           # Unit Tests (4 project theo layer)
├── docs/
│   ├── adr/                         # Architectural Decision Records
│   ├── plans/                       # Kế hoạch đang làm (done/ = đã ship)
│   ├── handoffs/                    # Ghi chú bàn giao cuối phiên làm việc
│   ├── references/                  # Tài liệu tham khảo về chiến lược & chỉ báo
│   ├── superpowers/                 # Plan & spec sinh từ workflow /ship
│   └── archive/                     # Tài liệu cũ, giữ để tra cứu
└── .github/                         # GitHub Actions, Copilot instructions
```

## 🚀 Bắt đầu Nhanh

### Yêu cầu Hệ thống
- .NET 9.0 SDK
- Node.js 18+ & npm
- MongoDB 7.0+
- Docker & Docker Compose (khuyến nghị)

### Cài đặt & Chạy

```bash
# Clone repository
git clone https://github.com/XcodeFi/invest-mate-v2.git
cd invest-mate-v2

# Setup backend
cd src/InvestmentApp.Api
cp appsettings.Development.json appsettings.Development.json.backup
# Cập nhật cấu hình MongoDB và Google OAuth

# Chạy backend
dotnet run

# Setup frontend
cd ../../frontend
npm install
ng serve
```

Chi tiết cài đặt xem [docs/getting-started.md](docs/getting-started.md)

## 📚 Tài liệu

Đọc theo thứ tự ưu tiên khi bắt đầu làm việc với codebase:

- [🏗️ Kiến trúc](docs/architecture.md) - Codebase map, service dependencies, API endpoints
- [🧩 Nghiệp vụ & Entity](docs/business-domain.md) - Entity relationships, business rules, external APIs
- [🧭 Bối cảnh dự án](docs/project-context.md) - Mục tiêu, quyết định UX, các pitfall đã gặp
- [✨ Tính năng theo phase](docs/features.md) - Danh sách tính năng đầy đủ
- [📐 ADR](docs/adr/README.md) - Các quyết định kiến trúc quan trọng (why X over Y)

Tài liệu vận hành & tham khảo:

- [🚀 Bắt đầu](docs/getting-started.md) - Hướng dẫn cài đặt chi tiết
- [📋 Chiến lược & Rủi ro](docs/strategy-templates.md) - 14 chiến lược mẫu & 4 mức rủi ro
- [🎯 Kế hoạch giao dịch](docs/trade-plans.md) - Vòng đời trade plan
- [🤖 AI Integration](docs/ai-integration.md) - AI assistant, agent surface, MCP tools
- [📚 Tài liệu tham khảo](docs/references/README.md) - Phân tích kỹ thuật, chỉ báo, quản lý rủi ro
- [🗂️ Kế hoạch](docs/plans/) - Plan đang triển khai; đã ship nằm ở [`done/`](docs/plans/done/)
- [🤖 AI Agent Guide](AI_AGENT_GUIDE_ENTERPRISE.md) - Hướng dẫn cho AI development

## 🧪 Testing

```bash
# Backend — chạy tất cả tests
dotnet test

# Backend — chạy tests với coverage
dotnet test --collect:"XPlat Code Coverage"

# Backend — chạy riêng 1 layer
dotnet test tests/InvestmentApp.Domain.Tests

# Frontend
cd frontend && npm test
```

Dự án theo TDD: viết test trước khi thêm feature hoặc đổi business logic.

## 🔒 Bảo mật

- **OAuth 2.0**: Google authentication
- **JWT Tokens**: Stateless authentication
- **API key theo user**: Có hạn dùng, thu hồi được, dùng cho AI agent surface
- **Input Validation**: FluentValidation pipeline
- **Audit Logging**: Ghi vết phiên impersonate và hành động do AI agent thực hiện
- **CORS**: Configured cross-origin policies

## 📈 Hiệu suất

- **Database Indexing**: Optimized MongoDB indexes
- **Background Processing**: Tính P&L và snapshot chạy nền
- **Pagination**: Efficient data pagination

## 🤝 Đóng góp

1. Fork project
2. Tạo feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to branch (`git push origin feature/AmazingFeature`)
5. Tạo Pull Request

## 📝 License

Distributed under the MIT License. See `LICENSE` for more information.

## 👥 Tác giả

- [XcodeFi](https://github.com/XcodeFi)

## 🙏 Lời cảm ơn

- Microsoft cho .NET ecosystem
- MongoDB team
- Angular team
- Open source community
