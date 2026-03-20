# AI Claude Integration — Tài liệu Kỹ thuật

> **Branch:** `feature/ai-integration`
> **Cập nhật lần cuối:** 2026-03-20
> **Phiên bản:** v2.15.0

---

## 1. Tổng quan

Tích hợp Claude AI (Anthropic) làm trợ lý thông minh trong Investment Mate — 5 use case streaming SSE, mỗi user tự quản API key (mã hóa server-side).

### Kiến trúc tổng thể

```
┌─────────────────────────────────────────────────────────────────┐
│  FRONTEND (Angular)                                             │
│                                                                 │
│  AiChatPanelComponent ──► AiService                            │
│  (sliding panel, markdown,  (fetch + ReadableStream → Observable)│
│   follow-up questions)       │                                  │
│                              │ POST /api/v1/ai/{use-case}      │
│                              │ Content-Type: text/event-stream  │
└──────────────────────────────┼──────────────────────────────────┘
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│  BACKEND (.NET 9)                                               │
│                                                                 │
│  AiController ──► AiAssistantService ──► ClaudeApiService      │
│  (SSE writer)    (context builder,       (Anthropic Messages API│
│                   system prompts,         stream: true,         │
│                   token tracking)         SSE event parser)     │
│                       │                                         │
│                       ▼                                         │
│                  AiSettings (MongoDB)                           │
│                  (encrypted key, model, usage stats)            │
└─────────────────────────────────────────────────────────────────┘
                               │
                               ▼
                  ┌──────────────────────┐
                  │  Anthropic Claude API │
                  │  POST /v1/messages    │
                  │  stream: true         │
                  └──────────────────────┘
```

### 5 Use Cases

| # | Use Case | Trigger (UI) | Context Data |
|---|----------|-------------|--------------|
| 1 | Journal Review | Nút "🤖 AI Phân tích" — `/journals` | 20 nhật ký + trades liên quan |
| 2 | Portfolio Review | Nút "🤖 AI Đánh giá" — portfolio detail | Vị thế, P&L, risk metrics |
| 3 | Trade Plan Advisor | Nút "🤖 AI Tư vấn" — `/trade-plan` | Full plan + portfolio balance |
| 4 | Chat Assistant | Nút "AI" — header (global) | Portfolio summary + conversation history |
| 5 | Monthly Summary | Nút "🤖 AI Tổng kết" — `/monthly-review` | Trades, P&L, win rate trong tháng |

---

## 2. Domain Entity

### AiSettings

**File:** `src/InvestmentApp.Domain/Entities/AiSettings.cs`
**Collection:** `ai_settings` (MongoDB, unique index on `UserId`)

```
AiSettings : AggregateRoot
├── UserId (string)
├── EncryptedApiKey (string)          ← ASP.NET Data Protection
├── Model (string)                    ← default "claude-sonnet-4-6-20250514"
├── TotalInputTokens (long)
├── TotalOutputTokens (long)
├── EstimatedCostUsd (decimal)
├── IsDeleted (bool)
├── CreatedAt (DateTime)
└── UpdatedAt (DateTime)
```

**Methods:**
- `Create(userId, encryptedApiKey, model)` — static factory
- `UpdateApiKey(encryptedApiKey)` — thay đổi API key
- `UpdateModel(model)` — đổi model (Sonnet ↔ Opus)
- `AddTokenUsage(inputTokens, outputTokens, costUsd)` — cộng dồn usage
- `SoftDelete()` — xóa mềm

**Quan hệ:** `User (1) ──── AiSettings (1)` — mỗi user 1 bản ghi cấu hình AI.

---

## 3. Backend — Layers

### 3.1. Application Layer (Interfaces)

#### IAiChatService — Low-level Claude API

**File:** `src/InvestmentApp.Application/Common/Interfaces/IAiChatService.cs`

```csharp
public interface IAiChatService
{
    IAsyncEnumerable<AiStreamChunk> StreamChatAsync(
        string apiKey, string model, string systemPrompt,
        List<AiChatMessage> messages, CancellationToken ct = default);
}

public class AiChatMessage { public string Role; public string Content; }

public class AiStreamChunk
{
    public string Type { get; set; }       // "text" | "usage" | "error"
    public string? Text { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public string? ErrorMessage { get; set; }
}
```

#### IAiAssistantService — High-level Use Cases

**File:** `src/InvestmentApp.Application/Common/Interfaces/IAiAssistantService.cs`

```csharp
public interface IAiAssistantService
{
    IAsyncEnumerable<AiStreamChunk> ReviewJournalAsync(string userId, string? portfolioId, string? question, CancellationToken ct);
    IAsyncEnumerable<AiStreamChunk> ReviewPortfolioAsync(string userId, string portfolioId, string? question, CancellationToken ct);
    IAsyncEnumerable<AiStreamChunk> AdviseTradePlanAsync(string userId, string tradePlanId, string? question, CancellationToken ct);
    IAsyncEnumerable<AiStreamChunk> ChatAsync(string userId, string message, List<AiChatMessage>? history, CancellationToken ct);
    IAsyncEnumerable<AiStreamChunk> MonthlySummaryAsync(string userId, string portfolioId, int year, int month, CancellationToken ct);
}
```

#### IAiKeyEncryptionService

**File:** `src/InvestmentApp.Application/Common/Interfaces/IAiKeyEncryptionService.cs`

```csharp
public interface IAiKeyEncryptionService
{
    string Encrypt(string plainTextApiKey);
    string Decrypt(string encryptedApiKey);
}
```

#### MediatR Commands/Queries

```
src/InvestmentApp.Application/AiSettings/
├── Commands/
│   └── SaveAiSettings/
│       └── SaveAiSettingsCommand.cs     ← { ApiKey?, Model? } → AiSettingsDto
├── Queries/
│   └── GetAiSettings/
│       └── GetAiSettingsQuery.cs        ← {} → AiSettingsDto (masked key)
└── Dtos/
    └── AiSettingsDto.cs                 ← HasApiKey, MaskedApiKey, Model, usage stats
```

---

### 3.2. Infrastructure Layer (Implementations)

#### AiKeyEncryptionService

**File:** `src/InvestmentApp.Infrastructure/Services/AiKeyEncryptionService.cs`

- Dùng ASP.NET Data Protection API
- Protector: `"AiSettings.ApiKey.v1"`
- Encrypt trước khi lưu MongoDB, decrypt khi gọi Anthropic API

#### ClaudeApiService — Anthropic SSE Client

**File:** `src/InvestmentApp.Infrastructure/Services/ClaudeApiService.cs`

**HTTP Configuration:**
- Base URL: `https://api.anthropic.com/`
- Endpoint: `POST /v1/messages`
- Headers: `x-api-key: {apiKey}`, `anthropic-version: 2023-06-01`
- Body: `{ model, system, messages, stream: true, max_tokens: 4096 }`
- `HttpCompletionOption.ResponseHeadersRead` — đọc stream ngay khi có header

**SSE Event Parsing:**

| Anthropic Event | Yield |
|----------------|-------|
| `message_start` | `{ type: "usage", inputTokens }` |
| `content_block_delta` (text_delta) | `{ type: "text", text }` |
| `message_delta` | `{ type: "usage", outputTokens }` |
| `message_stop` | End stream |
| HTTP 401 | `{ type: "error", errorMessage: "API key không hợp lệ..." }` |
| HTTP 429 | `{ type: "error", errorMessage: "Rate limit..." }` |
| HTTP error khác | `{ type: "error", errorMessage }` |

#### AiAssistantService — Use Case Orchestrator

**File:** `src/InvestmentApp.Infrastructure/Services/AiAssistantService.cs`

**Dependencies (8 injected):**
- `IAiSettingsRepository`, `IAiKeyEncryptionService`, `IAiChatService`
- `ITradeJournalRepository`, `ITradeRepository`, `IPortfolioRepository`
- `IPnLService`, `ITradePlanRepository`

**Flow mỗi use case:**
1. Load `AiSettings` → decrypt API key (error nếu chưa có key)
2. Gather context data từ các repository liên quan
3. Build system prompt (Vietnamese có dấu) + user message kèm context
4. Gọi `IAiChatService.StreamChatAsync()` → forward chunks
5. Sau stream kết thúc: cập nhật token usage vào `AiSettings`

**Chi phí token (CalculateCost):**

| Model | Input | Output |
|-------|-------|--------|
| Sonnet 4.6 (`claude-sonnet-4-6-*`) | $3 / 1M tokens | $15 / 1M tokens |
| Opus 4.6 (`claude-opus-4-6-*`) | $15 / 1M tokens | $75 / 1M tokens |

#### AiSettingsRepository

**File:** `src/InvestmentApp.Infrastructure/Repositories/AiSettingsRepository.cs`

- Collection: `ai_settings`
- Unique index: `UserId`
- Filter mặc định: `!IsDeleted`

---

### 3.3. API Layer (Controllers)

#### AiSettingsController — CRUD Settings

**File:** `src/InvestmentApp.Api/Controllers/AiSettingsController.cs`
**Route:** `api/v1/ai-settings` — `[Authorize]`

| Method | Path | Body | Mô tả |
|--------|------|------|-------|
| `GET` | `/` | — | Lấy settings (masked key `sk-ant-...xxxx` + usage) |
| `PUT` | `/` | `{ apiKey?, model? }` | Lưu/cập nhật API key + model |
| `DELETE` | `/` | — | Soft delete settings |
| `POST` | `/test` | — | Test API key (gọi Claude với "Hello") |

#### AiController — SSE Streaming Endpoints

**File:** `src/InvestmentApp.Api/Controllers/AiController.cs`
**Route:** `api/v1/ai` — `[Authorize]`

| Method | Path | Request Body | Use Case |
|--------|------|-------------|----------|
| `POST` | `/journal-review` | `{ portfolioId?, question? }` | Phân tích nhật ký |
| `POST` | `/portfolio-review` | `{ portfolioId!, question? }` | Đánh giá danh mục |
| `POST` | `/trade-plan-advisor` | `{ tradePlanId!, question? }` | Tư vấn kế hoạch GD |
| `POST` | `/chat` | `{ message!, history?[] }` | Chat tổng hợp |
| `POST` | `/monthly-summary` | `{ portfolioId!, year, month }` | Tổng kết tháng |

**SSE Response Format:**
```
Content-Type: text/event-stream
Cache-Control: no-cache
X-Accel-Buffering: no

data: {"type":"usage","inputTokens":150}

data: {"type":"text","text":"Phân tích "}

data: {"type":"text","text":"nhật ký..."}

data: {"type":"usage","outputTokens":85}

data: [DONE]
```

---

### 3.4. DI Registration

**File:** `src/InvestmentApp.Api/Program.cs`

```csharp
builder.Services.AddScoped<IAiSettingsRepository, AiSettingsRepository>();
builder.Services.AddScoped<IAiKeyEncryptionService, AiKeyEncryptionService>();
builder.Services.AddHttpClient<IAiChatService, ClaudeApiService>(client =>
{
    client.BaseAddress = new Uri("https://api.anthropic.com/");
    client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    client.Timeout = TimeSpan.FromMinutes(5);
});
builder.Services.AddScoped<IAiAssistantService, AiAssistantService>();
```

---

## 4. System Prompts (Vietnamese)

### Base Prompt (dùng chung)

```
Bạn là trợ lý AI tích hợp trong Investment Mate — ứng dụng quản lý danh mục
đầu tư chứng khoán Việt Nam.
Quy tắc:
- Trả lời bằng tiếng Việt có dấu đầy đủ
- Sử dụng markdown formatting
- Ngắn gọn, đi thẳng vào vấn đề
- Đưa ra nhận xét khách quan, dựa trên dữ liệu
- Khi gợi ý, luôn kèm lý do cụ thể
```

### 1. Journal Review

```
{base}
Nhiệm vụ: Phân tích nhật ký giao dịch.
1. **Tâm lý**: Nhận diện FOMO, tham lam, sợ hãi, revenge trading, tự tin thái quá
2. **Kỷ luật**: Đánh giá mức độ tuân thủ kế hoạch
3. **Điểm mạnh & yếu**: Quyết định tốt vs sai lầm lặp lại
4. **Gợi ý cải thiện**: Hành động cụ thể
```

**Context data:** 20 nhật ký gần nhất + trades liên quan (symbol, P&L, emotion, rating)

### 2. Portfolio Review

```
{base}
Nhiệm vụ: Đánh giá danh mục đầu tư.
1. **Đa dạng hóa**: Cảnh báo nếu 1 mã > 30% danh mục
2. **Hiệu suất**: P&L, win rate, vị thế đang lỗ
3. **Rủi ro**: Drawdown, vị thế gần stop-loss
4. **Gợi ý**: Cân bằng lại, chốt lời/cắt lỗ
```

**Context data:** Vị thế hiện tại, P&L per symbol, risk metrics, total value

### 3. Trade Plan Advisor

```
{base}
Nhiệm vụ: Tư vấn kế hoạch giao dịch.
1. **Entry**: Điểm vào có hợp lý? So với hỗ trợ/kháng cự
2. **Stop-loss**: SL quá gần/xa? Risk per trade
3. **Take-profit**: TP realistic? Risk:Reward ratio
4. **Position sizing**: Phù hợp với danh mục?
5. **Chấm điểm** (1-10) và gợi ý điều chỉnh
```

**Context data:** Full plan (entry/SL/TP/lots/exits), portfolio balance, recent trades cùng symbol

### 4. Chat Assistant

```
{base}
Bạn có thể trả lời về: chiến lược đầu tư, phân tích kỹ thuật,
quản lý rủi ro, cách sử dụng app, thị trường chứng khoán Việt Nam.
```

**Context data:** Danh sách portfolio + vốn ban đầu; conversation history từ frontend

### 5. Monthly Summary

```
{base}
Nhiệm vụ: Tổng kết hiệu suất tháng {month}/{year}.
1. **Tổng quan**: Lãi/lỗ, return %, win rate
2. **Giao dịch nổi bật**: Top winning/losing
3. **Pattern**: Xu hướng hành vi, sai lầm lặp lại
4. **Gợi ý tháng tới**: Mục tiêu cụ thể
```

**Context data:** Trades trong tháng, P&L, win rate, performance metrics

---

## 5. Frontend

### 5.1. AiService

**File:** `frontend/src/app/core/services/ai.service.ts`

#### Interfaces

```typescript
interface AiSettingsDto {
  hasApiKey: boolean;
  maskedApiKey?: string;        // "sk-ant-...xxxx"
  model: string;
  totalInputTokens: number;
  totalOutputTokens: number;
  estimatedCostUsd: number;
}

interface AiStreamChunk {
  type: 'text' | 'usage' | 'error';
  text?: string;
  inputTokens?: number;
  outputTokens?: number;
  errorMessage?: string;
}

interface AiChatMessage {
  role: 'user' | 'assistant';
  content: string;
}
```

#### Methods

**CRUD (HttpClient):**
- `getSettings(): Observable<AiSettingsDto>`
- `saveSettings(req: SaveAiSettingsRequest): Observable<AiSettingsDto>`
- `deleteSettings(): Observable<void>`
- `testConnection(): Observable<any>`

**Streaming (fetch + ReadableStream → Observable):**
- `streamJournalReview(portfolioId?, question?): Observable<AiStreamChunk>`
- `streamPortfolioReview(portfolioId, question?): Observable<AiStreamChunk>`
- `streamTradePlanAdvisor(tradePlanId, question?): Observable<AiStreamChunk>`
- `streamChat(message, history?): Observable<AiStreamChunk>`
- `streamMonthlySummary(portfolioId, year, month): Observable<AiStreamChunk>`

#### Streaming Implementation

```typescript
// Private method — wraps fetch in Observable
private streamRequest(endpoint: string, body: any): Observable<AiStreamChunk> {
  return new Observable(subscriber => {
    const abortController = new AbortController();

    fetch(`${environment.apiUrl}/${endpoint}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${token}` },
      body: JSON.stringify(body),
      signal: abortController.signal
    })
    .then(response => {
      const reader = response.body!.getReader();
      const decoder = new TextDecoder();
      // Read stream, split by \n\n, parse "data: {json}", emit chunks
      // Stop on "data: [DONE]"
    });

    return () => abortController.abort();  // cleanup on unsubscribe
  });
}
```

---

### 5.2. AiChatPanelComponent (Reusable)

**File:** `frontend/src/app/shared/components/ai-chat-panel/ai-chat-panel.component.ts`

#### Inputs/Outputs

```typescript
@Input() isOpen: boolean;
@Input() title: string;           // "AI Phân tích Nhật ký", "Trợ lý AI", etc.
@Input() useCase: string;         // 'journal-review' | 'portfolio-review' | 'trade-plan-advisor' | 'chat' | 'monthly-summary'
@Input() contextData: any;        // { portfolioId?, tradePlanId?, year?, month? }
@Output() isOpenChange = new EventEmitter<boolean>();
```

#### Internal State

```typescript
messages: { role: 'user' | 'assistant', content: string }[] = [];
isStreaming = false;
currentStreamText = '';
tokenUsage = { input: 0, output: 0 };
errorMessage: string | null = null;
userInput = '';
```

#### Behavior

1. **On open:** Auto-start streaming cho non-chat use cases (journal-review, portfolio-review, etc.)
2. **Chat use case:** Hiện empty state, chờ user nhập message
3. **Streaming:** Mỗi `text` chunk → append vào `currentStreamText` → render markdown qua `marked.parse()`
4. **Usage chunk:** Cập nhật `tokenUsage` display (hiện token count)
5. **Error chunk:** Hiện error card với link đến `/ai-settings`
6. **Complete:** Push `currentStreamText` vào `messages`, clear streaming state
7. **Follow-up:** User nhập câu hỏi → append vào `messages` → start new stream kèm question + history
8. **Close:** `AbortController.abort()` hủy stream đang chạy

#### UI Layout

```
┌─── Overlay (bg-black/30, click to close) ─────────────────────┐
│                                                                 │
│   ┌─── Panel (fixed right-0, max-w-lg, h-full, bg-gray-900) ─┐│
│   │ Header: title, token count, ✕ button                      ││
│   │─────────────────────────────────────────────────────────── ││
│   │ Messages area (flex-1, overflow-y-auto):                   ││
│   │   User bubble (right, bg-blue-600)                         ││
│   │   Assistant markdown (left, prose-invert)                  ││
│   │   Streaming indicator: blinking cursor ▋                   ││
│   │─────────────────────────────────────────────────────────── ││
│   │ Error banner (if error) → link to /ai-settings             ││
│   │─────────────────────────────────────────────────────────── ││
│   │ Input bar: text input + "Gửi" button                      ││
│   └────────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────┘
```

**Markdown rendering:** `marked` library + `DomSanitizer.bypassSecurityTrustHtml()`

---

### 5.3. AiSettingsComponent

**File:** `frontend/src/app/features/ai-settings/ai-settings.component.ts`
**Route:** `/ai-settings`

#### Sections

| Section | Mô tả |
|---------|-------|
| Khóa API Anthropic | Input (password type) + show/hide, save, change |
| Kiểm tra kết nối | Test API key với Claude |
| Mô hình AI | Dropdown: Sonnet 4.6 (mặc định) / Opus 4.6 |
| Thống kê sử dụng | Input tokens, Output tokens, Chi phí ước tính (USD) |
| Vùng nguy hiểm | Xóa API key với confirm dialog |

---

### 5.4. Integration Points

| Page | Component | Button | useCase | contextData |
|------|-----------|--------|---------|-------------|
| `/journals` | `JournalsComponent` | "🤖 AI Phân tích" | `journal-review` | `{ portfolioId }` |
| Portfolio Detail | `PortfolioDetailComponent` | "🤖 AI Đánh giá" | `portfolio-review` | `{ portfolioId }` |
| `/trade-plan` | `TradePlanComponent` | "🤖 AI Tư vấn" (per plan) | `trade-plan-advisor` | `{ tradePlanId }` |
| `/monthly-review` | `MonthlyReviewComponent` | "🤖 AI Tổng kết" | `monthly-summary` | `{ portfolioId, year, month }` |
| Header (global) | `HeaderComponent` | "AI" button | `chat` | `{}` |

---

## 6. Error Handling

| Error | Backend Response | Frontend Behavior |
|-------|-----------------|-------------------|
| Chưa có API key | `{ type: "error", errorMessage: "Chưa cấu hình API key..." }` | Error card + link `/ai-settings` |
| API key sai (401) | `{ type: "error", errorMessage: "API key không hợp lệ..." }` | Gợi ý kiểm tra lại key |
| Rate limit (429) | `{ type: "error", errorMessage: "Rate limit..." }` | Thông báo chờ |
| Lỗi mạng | Catch exception → yield error chunk | Hiện lỗi kết nối |
| Không có dữ liệu | Yield error giải thích | Thông báo chưa có dữ liệu |
| User đóng panel | `CancellationToken` cancel | `AbortController.abort()` |

---

## 7. Model Support

| Model ID | Tên hiển thị | Giá Input | Giá Output | Ghi chú |
|----------|-------------|-----------|------------|---------|
| `claude-sonnet-4-6-20250514` | Claude Sonnet 4.6 | $3/M tokens | $15/M tokens | Mặc định, nhanh, tiết kiệm |
| `claude-opus-4-6-20250514` | Claude Opus 4.6 | $15/M tokens | $75/M tokens | Sâu hơn, chính xác hơn |

**Ước tính chi phí:**
- Context ~2000 tokens/request, response ~500 tokens
- 100 câu hỏi/ngày với Sonnet ≈ **$0.10/ngày ≈ $3/tháng**
- Token usage được tích lũy và hiển thị trên `/ai-settings`

---

## 8. File Map

### Backend

| File | Layer | Mô tả |
|------|-------|-------|
| `Domain/Entities/AiSettings.cs` | Domain | Entity lưu cấu hình AI per user |
| `Application/Common/Interfaces/IAiChatService.cs` | Application | Interface + types cho Claude API |
| `Application/Common/Interfaces/IAiAssistantService.cs` | Application | Interface 5 use cases |
| `Application/Common/Interfaces/IAiKeyEncryptionService.cs` | Application | Interface mã hóa API key |
| `Application/AiSettings/Commands/SaveAiSettings/` | Application | MediatR command upsert settings |
| `Application/AiSettings/Queries/GetAiSettings/` | Application | MediatR query lấy settings |
| `Application/AiSettings/Dtos/AiSettingsDto.cs` | Application | DTO response |
| `Infrastructure/Repositories/AiSettingsRepository.cs` | Infrastructure | MongoDB repository |
| `Infrastructure/Services/AiKeyEncryptionService.cs` | Infrastructure | Data Protection encryption |
| `Infrastructure/Services/ClaudeApiService.cs` | Infrastructure | Anthropic SSE streaming client |
| `Infrastructure/Services/AiAssistantService.cs` | Infrastructure | 5 use cases + prompts + tracking |
| `Api/Controllers/AiSettingsController.cs` | API | CRUD settings endpoints |
| `Api/Controllers/AiController.cs` | API | 5 SSE streaming endpoints |

### Frontend

| File | Mô tả |
|------|-------|
| `core/services/ai.service.ts` | Service: CRUD + streaming (fetch → Observable) |
| `shared/components/ai-chat-panel/ai-chat-panel.component.ts` | Reusable sliding chat panel |
| `features/ai-settings/ai-settings.component.ts` | Settings page `/ai-settings` |

### Files đã sửa (integration)

| File | Thay đổi |
|------|----------|
| `Application/RepositoryInterfaces.cs` | Thêm `IAiSettingsRepository` |
| `Api/Program.cs` | DI registration (4 services) |
| `Infrastructure/InvestmentApp.Infrastructure.csproj` | Thêm DataProtection package |
| `app.routes.ts` | Route `/ai-settings` |
| `header.component.ts` | AI chat button + panel + nav item |
| `bottom-nav.component.ts` | AI settings trong moreItems |
| `journals.component.ts` | AI review button + panel |
| `portfolio-detail.component.ts` | AI review button + panel |
| `trade-plan.component.ts` | AI advisor button + panel |
| `monthly-review.component.ts` | AI summary button + panel |
