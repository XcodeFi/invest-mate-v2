# 🚀 Bắt đầu với Investment Mate v2

Hướng dẫn chi tiết để cài đặt và chạy hệ thống Investment Mate v2 trên môi trường development và production.

## 📋 Yêu cầu Hệ thống

### Minimum Requirements
- **OS**: Windows 10/11, macOS 12+, Ubuntu 20.04+
- **CPU**: Dual-core 2.5 GHz
- **RAM**: 8 GB
- **Disk**: 10 GB free space

### Development Environment
- **.NET 8.0 SDK**: [Download here](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Node.js 18+**: [Download here](https://nodejs.org/)
- **MongoDB 7.0+**: [Download here](https://www.mongodb.com/try/download/community)
- **Git**: [Download here](https://git-scm.com/)
- **Visual Studio 2022** hoặc **VS Code** với C# extension

### Production Environment
- **Docker & Docker Compose**: [Install Docker](https://docs.docker.com/get-docker/)
- **Reverse Proxy**: Nginx hoặc Traefik
- **SSL Certificate**: Let's Encrypt hoặc commercial SSL

## 🛠️ Cài đặt Development Environment

### 1. Clone Repository

```bash
git clone https://github.com/your-org/investment-mate-v2.git
cd investment-mate-v2
```

### 2. Cài đặt .NET 8.0 SDK

```bash
# Windows (PowerShell)
winget install Microsoft.DotNet.SDK.8

# macOS (Homebrew)
brew install --cask dotnet-sdk

# Ubuntu
wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0
```

### 3. Cài đặt MongoDB

```bash
# Windows
# Download và cài đặt từ https://www.mongodb.com/try/download/community

# macOS (Homebrew)
brew tap mongodb/brew
brew install mongodb-community
brew services start mongodb-community

# Ubuntu
sudo apt-get install gnupg
wget -qO - https://www.mongodb.org/static/pgp/server-7.0.asc | sudo apt-key add -
echo "deb [ arch=amd64,arm64 ] https://repo.mongodb.org/apt/ubuntu jammy/mongodb-org/7.0 multiverse" | sudo tee /etc/apt/sources.list.d/mongodb-org-7.0.list
sudo apt-get update
sudo apt-get install -y mongodb-org
sudo systemctl start mongod
```

### 4. Cài đặt Node.js (cho Frontend)

```bash
# Windows/macOS
# Download từ https://nodejs.org/

# Ubuntu
curl -fsSL https://deb.nodesource.com/setup_18.x | sudo -E bash -
sudo apt-get install -y nodejs
```

### 5. Cấu hình MongoDB

```bash
# Tạo database và user
mongosh

use investmentapp
db.createUser({
  user: "investmentuser",
  pwd: "securepassword123",
  roles: ["readWrite"]
})
```

## ⚙️ Cấu hình Ứng dụng

### 1. Backend Configuration

```bash
cd src/InvestmentApp.Api
cp appsettings.json appsettings.Development.json
```

Chỉnh sửa `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "MongoDb": "mongodb://localhost:27017"
  },
  "MongoDb": {
    "DatabaseName": "investmentapp"
  },
  "Jwt": {
    "Key": "YourSuperSecretKeyHere_MakeItVeryLongAndSecure123456789",
    "Issuer": "InvestmentApp",
    "Audience": "InvestmentAppUsers",
    "ExpiryInMinutes": 60
  },
  "GoogleOAuth": {
    "ClientId": "your-google-client-id-here",
    "ClientSecret": "your-google-client-secret-here"
  }
}
```

### 2. Google OAuth Setup

1. Truy cập [Google Cloud Console](https://console.cloud.google.com/)
2. Tạo project mới hoặc chọn project existing
3. Enable Google+ API
4. Tạo OAuth 2.0 credentials:
   - Application type: Web application
   - Authorized redirect URIs: `https://localhost:5001/api/auth/google/callback`
5. Copy Client ID và Client Secret vào `appsettings.Development.json`

## 🚀 Chạy Ứng dụng

### Backend

```bash
# Từ thư mục root
cd src/InvestmentApp.Api
dotnet restore
dotnet build
dotnet run
```

API sẽ chạy tại: `https://localhost:5001`

### Worker Service

```bash
# Terminal mới
cd src/InvestmentApp.Worker
dotnet run
```

### Frontend (khi có)

```bash
cd frontend
npm install
ng serve
```

Frontend sẽ chạy tại: `http://localhost:4200`

## 🧪 Testing

### Unit Tests

```bash
# Chạy tất cả tests
dotnet test

# Chạy tests cho layer cụ thể
dotnet test tests/InvestmentApp.Domain.Tests/
dotnet test tests/InvestmentApp.Application.Tests/

# Tests với coverage
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage
```

### Integration Tests

```bash
# Chạy integration tests
dotnet test --filter Category=Integration
```

### API Testing

```bash
# Sử dụng Swagger UI
# Truy cập: https://localhost:5001/swagger

# Hoặc sử dụng HTTP files
# Trong VS Code, mở InvestmentApp.Api.http
```

## 🐳 Docker Deployment

### Development với Docker Compose

```yaml
# docker-compose.yml
version: '3.8'
services:
  mongodb:
    image: mongo:7.0
    ports:
      - "27017:27017"
    volumes:
      - mongodb_data:/data/db
    environment:
      MONGO_INITDB_DATABASE: investmentapp

  api:
    build:
      context: .
      dockerfile: src/InvestmentApp.Api/Dockerfile
    ports:
      - "5001:80"
    depends_on:
      - mongodb
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__MongoDb=mongodb://mongodb:27017
      - MongoDb__DatabaseName=investmentapp

volumes:
  mongodb_data:
```

```bash
docker-compose up --build
```

### Production Deployment

```bash
# Build production images
docker build -f src/InvestmentApp.Api/Dockerfile -t investment-api:latest .
docker build -f src/InvestmentApp.Worker/Dockerfile -t investment-worker:latest .

# Chạy với docker-compose.prod.yml
docker-compose -f docker-compose.prod.yml up -d
```

## 🔍 Troubleshooting

### Common Issues

#### MongoDB Connection Failed
```bash
# Kiểm tra MongoDB đang chạy
sudo systemctl status mongod

# Kiểm tra logs
sudo journalctl -u mongod -f

# Test connection
mongosh --eval "db.adminCommand('ismaster')"
```

#### Port Already in Use
```bash
# Tìm process sử dụng port
netstat -ano | findstr :5001

# Kill process
taskkill /PID <PID> /F
```

#### SSL Certificate Issues
```bash
# Development - trust dev certificate
dotnet dev-certs https --trust

# Production - sử dụng reverse proxy với SSL
```

#### Google OAuth Redirect Issues
- Đảm bảo redirect URI trong Google Console khớp với ứng dụng
- Kiểm tra HTTPS cho production
- Verify CORS settings

### Debug Mode

```bash
# Chạy với debug logging
export ASPNETCORE_ENVIRONMENT=Development
dotnet run --launch-profile "InvestmentApp.Api"

# Kiểm tra logs
tail -f logs/investment-app-*.log
```

## 📊 Monitoring & Logging

### Application Logs
- Logs được lưu trong thư mục `logs/`
- Structured logging với Serilog
- Log levels: Trace, Debug, Information, Warning, Error, Fatal

### Health Checks
```bash
# Health endpoint
GET https://localhost:5001/health

# Metrics endpoint (planned)
GET https://localhost:5001/metrics
```

### Database Monitoring
```bash
# MongoDB status
mongosh --eval "db.serverStatus()"

# Collection statistics
mongosh investmentapp --eval "db.stats()"
```

## 🔒 Security Checklist

- [ ] Thay đổi JWT secret key
- [ ] Cấu hình Google OAuth credentials
- [ ] Enable HTTPS trong production
- [ ] Cấu hình CORS policies
- [ ] Setup firewall rules
- [ ] Enable audit logging
- [ ] Regular security updates

## 📞 Support

- **Issues**: [GitHub Issues](https://github.com/your-org/investment-mate-v2/issues)
- **Discussions**: [GitHub Discussions](https://github.com/your-org/investment-mate-v2/discussions)
- **Documentation**: [Wiki](https://github.com/your-org/investment-mate-v2/wiki)

## 🚀 Next Steps

Sau khi setup xong:
1. [Tạo portfolio đầu tiên](api.md#create-portfolio)
2. [Thêm giao dịch](api.md#add-trade)
3. [Xem P&L report](api.md#get-pnl)
4. [Tích hợp frontend](frontend-setup.md)

---

**Happy coding! 🎉**</content>
<parameter name="filePath">d:\invest-mate-v2\project\docs\getting-started.md