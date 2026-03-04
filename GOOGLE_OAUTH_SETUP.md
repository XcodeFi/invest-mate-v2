# Google OAuth Setup Guide

## 1. Tạo Google OAuth Credentials

### Truy cập Google Cloud Console
1. Đi đến [Google Cloud Console](https://console.cloud.google.com/)
   - **Link trực tiếp đến OAuth Client hiện tại**: https://console.cloud.google.com/auth/clients/110814165751-4g48k5km1ddv4m4pe1jsdqfl7eq1k5k5.apps.googleusercontent.com?project=fir-test-82cdf
2. Tạo project mới hoặc chọn project existing
3. Enable các APIs cần thiết:
   - Google+ API
   - Google OAuth2 API

### Tạo OAuth 2.0 Credentials
1. Truy cập "APIs & Services" > "Credentials"
2. Click "Create Credentials" > "OAuth 2.0 Client IDs"
3. Chọn "Web application"
4. Điền thông tin:
   - Name: "Investment Mate v2"
   - Authorized redirect URIs: 
     ```
     http://localhost:5000/api/auth/google/callback
     ```
5. Lưu Client ID và Client Secret

## 2. Cập nhật Application Settings

Update file `src/InvestmentApp.Api/appsettings.Development.json`:

```json
{
  "GoogleOAuth": {
    "ClientId": "your-actual-client-id-here",
    "ClientSecret": "your-actual-client-secret-here"
  }
}
```

## 3. Setup Database

### Sử dụng Docker (Khuyến nghị)
```bash
docker run -d --name mongodb -p 27017:27017 mongo:5.0
```

### Hoặc cài đặt MongoDB local
1. Download từ [mongodb.com](https://www.mongodb.com/try/download/community)
2. Install và start MongoDB service

## 4. Test Authentication Flow

### Start Backend
```bash
cd src/InvestmentApp.Api
dotnet run --launch-profile https
```

### Start Frontend
```bash
cd frontend
npm start
```

### Test Steps
1. Truy cập `http://localhost:4200`
2. Click "Đăng nhập với Google"
3. Đăng nhập với tài khoản Google
4. Sẽ được redirect về dashboard nếu thành công

## Troubleshooting

### ✅ FIXED: Lỗi "Correlation failed"
- **Nguyên nhân:** Đã disable state validation và correlation cookie không đúng cách
- **Giải pháp:** Sử dụng authentication scheme config chuẩn:
  ```csharp
  options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
  options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
  ```
- **Lưu ý:** Không custom StateDataFormat hoặc CorrelationCookie trong production

### Lỗi Database Connection
- Đảm bảo MongoDB đang chạy trên port 27017
- Kiểm tra connection string trong appsettings.json

### Lỗi SSL/Certificate
- Development environment sử dụng self-signed certificate
- Browser có thể show warning - click "Proceed anyway"

## Security Notes

- **Never commit real credentials** to version control
- Sử dụng environment variables cho production
- Rotate credentials định kỳ
- Monitor authentication logs