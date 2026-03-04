# Investment Mate v2 - Hệ thống Quản lý Danh mục Đầu tư Doanh nghiệp

[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![MongoDB](https://img.shields.io/badge/MongoDB-7.0-green.svg)](https://www.mongodb.com/)
[![Angular](https://img.shields.io/badge/Angular-19-red.svg)](https://angular.io/)
[![License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Hệ thống quản lý danh mục đầu tư doanh nghiệp được xây dựng theo kiến trúc Clean Architecture, CQRS, và Domain-Driven Design (DDD). Hỗ trợ xác thực Google OAuth, tính toán P&L theo phương pháp chi phí trung bình, và xử lý nền tảng thời gian thực.

## ✨ Tính năng Chính

### 🔐 Xác thực & Bảo mật
- **Google OAuth 2.0**: Đăng nhập an toàn với tài khoản Google
- **JWT Tokens**: Quản lý phiên làm việc bảo mật
- **Role-based Access**: Phân quyền chi tiết theo vai trò
- **Audit Logging**: Ghi log toàn bộ hoạt động hệ thống

### 📊 Quản lý Danh mục Đầu tư
- **Portfolio Management**: Tạo và quản lý nhiều danh mục đầu tư
- **Trade Tracking**: Theo dõi giao dịch mua/bán chứng khoán
- **Real-time P&L**: Tính toán lãi/lỗ thời gian thực
- **Position Monitoring**: Giám sát vị thế đầu tư chi tiết

### 💰 Tính toán P&L Nâng cao
- **Average Cost Method**: Phương pháp chi phí trung bình chuẩn xác
- **Realized vs Unrealized P&L**: Phân biệt lãi/lỗ đã thực hiện và chưa thực hiện
- **Position-based Tracking**: Theo dõi từng vị thế riêng biệt
- **Historical Performance**: Báo cáo hiệu suất lịch sử

### 🏗️ Kiến trúc Kỹ thuật
- **Clean Architecture**: Tách biệt rõ ràng các layer
- **CQRS Pattern**: Command Query Responsibility Segregation
- **Domain-Driven Design**: Thiết kế theo domain business
- **Event Sourcing**: Xử lý sự kiện domain
- **Background Processing**: Worker service xử lý nền tảng

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
├── docs/                            # Documentation
└── .github/                         # GitHub Actions, Copilot instructions
```

## 🚀 Bắt đầu Nhanh

### Yêu cầu Hệ thống
- .NET 8.0 SDK
- Node.js 18+ & npm
- MongoDB 7.0+
- Docker & Docker Compose (khuyến nghị)

### Cài đặt & Chạy

```bash
# Clone repository
git clone https://github.com/your-org/investment-mate-v2.git
cd investment-mate-v2

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

- [🚀 Bắt đầu](docs/getting-started.md) - Hướng dẫn cài đặt chi tiết
- [🏗️ Kiến trúc](docs/architecture.md) - Thiết kế hệ thống
- [🔧 API Documentation](docs/api.md) - REST API endpoints
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

- **Your Name** - *Initial work* - [your-github](https://github.com/your-github)

## 🙏 Lời cảm ơn

- Microsoft cho .NET ecosystem
- MongoDB team
- Angular team
- Open source community

---

**Lưu ý**: Đây là dự án doanh nghiệp với yêu cầu bảo mật và hiệu suất cao. Đảm bảo tuân thủ các best practices trong production deployment.</content>
<parameter name="filePath">d:\invest-mate-v2\project\README.md