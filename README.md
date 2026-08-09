# Investment Mate v2 - Hệ thống Quản lý Danh mục Đầu tư Doanh nghiệp

[![.NET](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/)
[![MongoDB](https://img.shields.io/badge/MongoDB-7.0-green.svg)](https://www.mongodb.com/)
[![Angular](https://img.shields.io/badge/Angular-19-red.svg)](https://angular.io/)
[![License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Hệ thống quản lý danh mục đầu tư doanh nghiệp được xây dựng theo kiến trúc Clean Architecture, CQRS, và Domain-Driven Design (DDD). Hỗ trợ xác thực Google OAuth, tính toán P&L theo phương pháp chi phí trung bình, và xử lý nền tảng thời gian thực.

## ✨ Tính năng Chính

> Xem chi tiết đầy đủ tại [docs/features.md](docs/features.md)

### 🧙 Wizard Giao dịch (5 bước)

- **Quy trình kỷ luật**: Chiến lược → Kế hoạch → Checklist → Ghi GD → Nhật ký
- **GO/NO-GO enforcement**: Không thể bỏ qua checklist bắt buộc
- **Auto-fill giá**: Nhập mã CP → tự điền giá hiện tại từ API

### 📊 Dashboard Cockpit

- **4 Summary Cards**: Tổng giá trị, Vốn đầu tư, P&L, CAGR
- **Compound Growth Tracker**: CAGR thực tế vs mục tiêu, ước tính 5/10/20 năm
- **Mini Equity Curve**: Line chart Chart.js với range filter 30D/90D/1Y/All
- **Risk Alert Banner**: Stop-loss proximity, drawdown, cảnh báo tập trung danh mục

### 📋 Kế hoạch Giao dịch với Template

- **Auto-fill giá**: Debounced symbol lookup → điền Entry Price tự động
- **Position Sizing tự động**: Tính từ Risk Profile (maxRisk%, maxPosition%)
- **Template save/load**: Lưu kế hoạch thành template → tái sử dụng với 1 click
- **Risk violations enforcement**: Cảnh báo vi phạm + yêu cầu xác nhận

### 📈 Analytics & Báo cáo

- **Bar chart P&L**: Lãi/lỗ theo cổ phiếu
- **Donut chart**: Phân bổ danh mục
- **Equity Curve**: Đường tăng trưởng vốn theo ngày
- **Monthly Returns Matrix**: Hiệu suất theo năm × tháng, color-coded
- **Monthly Review** (`/monthly-review`): Báo cáo tháng tự động

### 🛡️ Quản lý Rủi ro

- **Risk Profile**: Max position%, max risk/lệnh, R:R tối thiểu, max drawdown alert
- **Risk Dashboard**: Tổng quan sức khỏe rủi ro, stop-loss tracking, correlation
- **Concentration Alert**: Tự động cảnh báo khi cổ phiếu vượt giới hạn Risk Profile
- **Stress Test**: Mô phỏng 5 kịch bản VNINDEX (-20% → +15%)

### 🔐 Xác thực & Bảo mật

- **Google OAuth 2.0**: Đăng nhập an toàn với tài khoản Google
- **JWT Tokens**: Quản lý phiên làm việc bảo mật
- **User-scoped data**: Templates, Risk Profiles, Journals tách biệt theo user

### 💰 P&L & Portfolio

- **Average Cost Method**: Phương pháp chi phí trung bình chuẩn xác
- **Realized vs Unrealized P&L**: Phân biệt lãi/lỗ đã và chưa thực hiện
- **Capital Flows**: Theo dõi dòng vốn vào/ra
- **Daily Snapshots**: Lịch sử snapshot cho Equity Curve

### 🏗️ Kiến trúc Kỹ thuật

- **Clean Architecture**: Tách biệt rõ ràng các layer
- **CQRS + MediatR**: Command Query Responsibility Segregation
- **Domain-Driven Design**: Thiết kế theo domain business
- **Background Processing**: Worker service xử lý P&L nền tảng

## 🛠️ Công nghệ Sử dụng

### Backend (.NET 8)
- **ASP.NET Core Web API**: RESTful API endpoints
- **MediatR**: CQRS implementation
- **MongoDB**: NoSQL database với indexing tối ưu
- **FluentValidation**: Validation pipeline
- **Serilog**: Structured logging
- **JWT Bearer Authentication**: Token-based security

### Frontend (Angular 19)
- **Angular 19**: Modern web framework
- **TypeScript**: Type-safe development
- **RxJS**: Reactive programming
- **Angular Material**: UI components
- **Chart.js**: Data visualization

### Infrastructure
- **Docker**: Containerization
- **MongoDB**: Document database
- **Redis**: Caching layer (planned)
- **GitHub Actions**: CI/CD pipeline (planned)

## 📁 Cấu trúc Dự án

```
InvestmentApp.sln
├── src/
│   ├── InvestmentApp.Api/           # API Layer - Controllers, Middleware
│   ├── InvestmentApp.Application/   # Application Layer - Commands, Queries, Services
│   ├── InvestmentApp.Domain/        # Domain Layer - Entities, Value Objects, Events
│   ├── InvestmentApp.Infrastructure/# Infrastructure Layer - Repositories, External Services
│   └── InvestmentApp.Worker/        # Background Worker - P&L calculations, snapshots
├── tests/                           # Unit & Integration Tests
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

# Setup frontend (khi có)
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
# Chạy tất cả tests
dotnet test

# Chạy tests với coverage
dotnet test --collect:"XPlat Code Coverage"

# Chạy integration tests
dotnet test --filter Category=Integration
```

## 🔒 Bảo mật

- **OAuth 2.0**: Google authentication
- **JWT Tokens**: Stateless authentication
- **Input Validation**: Comprehensive validation
- **Audit Logging**: Complete activity tracking
- **CORS**: Configured cross-origin policies
- **Rate Limiting**: API rate limiting (planned)

## 📈 Hiệu suất

- **Database Indexing**: Optimized MongoDB indexes
- **Caching**: Redis caching layer (planned)
- **Background Processing**: Asynchronous P&L calculations
- **Pagination**: Efficient data pagination
- **Compression**: Response compression

## 🤝 Đóng góp

1. Fork project
2. Tạo feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to branch (`git push origin feature/AmazingFeature`)
5. Tạo Pull Request

## 📝 License

Distributed under the MIT License. See `LICENSE` for more information.

## 👥 Tác giả

- **Trường Phạm** - [XcodeFi](https://github.com/XcodeFi)

## 🙏 Lời cảm ơn

- Microsoft cho .NET ecosystem
- MongoDB team
- Angular team
- Open source community

---

**Lưu ý**: Đây là dự án doanh nghiệp với yêu cầu bảo mật và hiệu suất cao. Đảm bảo tuân thủ các best practices trong production deployment.</content>
<parameter name="filePath">d:\invest-mate-v2\project\README.md