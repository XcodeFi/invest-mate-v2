# Investment Mate v2 - Local Development Setup

Scripts để chạy Investment Mate v2 trên môi trường local development.

## 🚀 Quick Start

### Cách 1: PowerShell Script (Khuyến nghị)
```powershell
.\run-local.ps1
```

### Cách 2: Batch Script
```cmd
run-local.bat
```

## 📋 Yêu cầu hệ thống

- **.NET 9 SDK** - [Download](https://dotnet.microsoft.com/download)
- **Node.js 18+** - [Download](https://nodejs.org/)
- **Angular CLI** (sẽ được cài tự động nếu chưa có)

## 🔧 Scripts Available

### `run-local.ps1` (PowerShell)
Script đầy đủ tính năng với:
- ✅ Kiểm tra prerequisites tự động
- ✅ Build cả backend và frontend
- ✅ Chạy song song backend và frontend
- ✅ Kiểm tra services ready trước khi báo thành công
- ✅ Dừng tất cả services khi thoát
- ✅ Options để skip build hoặc services cụ thể

**Usage:**
```powershell
# Chạy đầy đủ (build + start services)
.\run-local.ps1

# Skip build (chỉ start services)
.\run-local.ps1 -SkipBuild

# Chỉ chạy backend
.\run-local.ps1 -SkipFrontend

# Chỉ chạy frontend
.\run-local.ps1 -SkipBackend
```

### `run-local.bat` (Batch)
Script đơn giản:
- ✅ Kiểm tra cơ bản prerequisites
- ✅ Build và start services
- ✅ Mở từng service trong command window riêng
- ✅ Dễ debug và xem logs

## 🌐 Services URLs

Sau khi chạy thành công:

- **Backend API**: http://localhost:5000
- **API Documentation**: http://localhost:5000/swagger
- **Frontend**: http://localhost:4200

## 🛠️ Troubleshooting

### Backend không start
```bash
# Kiểm tra port 5000 có bị chiếm không
netstat -ano | findstr :5000

# Kill process nếu cần
taskkill /PID <PID> /F
```

### Frontend không start
```bash
# Kiểm tra port 4200 có bị chiếm không
netstat -ano | findstr :4200

# Clear Angular cache
cd frontend
rm -rf node_modules/.cache
npm start
```

### Node.js version cũ
```bash
# Upgrade Node.js lên 18+
# Download từ https://nodejs.org/
node --version  # Should be 18+
```

### .NET SDK không tìm thấy
```bash
# Install .NET 9 SDK
# Download từ https://dotnet.microsoft.com/download
dotnet --version  # Should show 9.x.x
```

## 🔍 Manual Start (Nếu scripts không hoạt động)

### Backend
```bash
cd src/InvestmentApp.Api
dotnet clean
dotnet build
dotnet run --urls=http://localhost:5000
```

### Frontend
```bash
cd frontend
npm install
npx ng serve --port 4200
```

## 📝 Development Notes

- Backend sử dụng **Clean Architecture** với .NET 9
- Frontend sử dụng **Angular 17+** với standalone components
- Database: **MongoDB** (cần setup riêng nếu dùng local DB)
- Authentication: **JWT tokens**
- API Documentation: **Swagger/OpenAPI**

## 🎯 Features

- ✅ VCBS-compliant fee calculation
- ✅ Real-time fee calculation in trade forms
- ✅ Portfolio management
- ✅ Trade execution
- ✅ P&L calculation
- ✅ Risk management

---

**Happy coding! 🚀**