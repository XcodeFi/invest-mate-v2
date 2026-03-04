# Environment Configuration

Hệ thống Investment Mate v2 hỗ trợ nhiều môi trường khác nhau thông qua environment configuration.

## Cấu trúc Environment

```
src/environments/
├── environment.ts          # Development/Local
├── environment.prod.ts     # Production
└── environment.staging.ts  # Staging (tùy chọn)
```

## Cấu hình cho từng môi trường

### Development (Local)
```typescript
export const environment = {
  production: false,
  apiUrl: 'http://localhost:5000/api/v1',
  appName: 'Investment Mate v2 - Development'
};
```

### Production
```typescript
export const environment = {
  production: true,
  apiUrl: 'https://api.investmentmate.com/api/v1',
  appName: 'Investment Mate v2'
};
```

### Staging
```typescript
export const environment = {
  production: false,
  apiUrl: 'https://staging-api.investmentmate.com/api/v1',
  appName: 'Investment Mate v2 - Staging'
};
```

## Cách sử dụng trong Service

Tất cả services đã được cập nhật để sử dụng environment configuration:

```typescript
import { environment } from '../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class ExampleService {
  private readonly API_URL = `${environment.apiUrl}/endpoint`;
}
```

## Build Commands

### Development
```bash
npm run build          # Sử dụng environment.ts
npm start             # Development server
```

### Production
```bash
npm run build --prod   # Sử dụng environment.prod.ts
```

### Staging (nếu có)
```bash
npm run build --configuration=staging
```

## Deployment

### Local Development
- API chạy trên: `http://localhost:5000`
- Frontend sử dụng: `environment.ts`

### Production
- API chạy trên: `https://api.investmentmate.com`
- Frontend sử dụng: `environment.prod.ts`

## Thêm Environment mới

1. Tạo file `environment.{name}.ts` trong `src/environments/`
2. Cập nhật `angular.json` để thêm configuration mới
3. Sử dụng: `npm run build --configuration={name}`

Ví dụ trong `angular.json`:
```json
"configurations": {
  "staging": {
    "fileReplacements": [
      {
        "replace": "src/environments/environment.ts",
        "with": "src/environments/environment.staging.ts"
      }
    ]
  }
}
```