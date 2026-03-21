# Tích hợp AI Assistant vào Investment Mate v2

## Trả lời ngắn: CÓ — và rất khả thi

Investment Mate đã có backend (NestJS + MongoDB) và dữ liệu user đầy đủ. Chỉ cần thêm 1 endpoint gọi AI API (Claude/GPT), truyền context từ DB, và hiển thị chat UI trên frontend.

---

## 1. KIẾN TRÚC TỔNG THỂ

```
┌──────────────────────────────────────────────────────────┐
│  FRONTEND (Angular)                                      │
│                                                          │
│  ┌─────────────────────────────┐                         │
│  │  💬 Chat Widget (góc phải)  │                         │
│  │  ┌───────────────────────┐  │                         │
│  │  │ 🤖 Danh mục bạn đang  │  │                         │
│  │  │ lỗ -38% ở FPT. Nên   │  │                         │
│  │  │ xem xét cắt lỗ hoặc  │  │                         │
│  │  │ DCA thêm nếu tin vào  │  │                         │
│  │  │ triển vọng dài hạn.   │  │                         │
│  │  └───────────────────────┘  │                         │
│  │  ┌───────────────────────┐  │                         │
│  │  │ 💭 Nhập câu hỏi...   │  │                         │
│  │  └───────────────────────┘  │                         │
│  └─────────────────────────────┘                         │
│                                                          │
│  ┌────── Suggested Questions ──────┐                     │
│  │ "Danh mục tôi đang thế nào?"   │                     │
│  │ "FPT nên giữ hay bán?"         │                     │
│  │ "Gợi ý mã mua tuần này?"       │                     │
│  └─────────────────────────────────┘                     │
└──────────────────┬───────────────────────────────────────┘
                   │ POST /api/v1/ai/chat
                   ▼
┌──────────────────────────────────────────────────────────┐
│  BACKEND (NestJS)                                        │
│                                                          │
│  ┌─── AiChatController ──────────────────────────────┐   │
│  │                                                    │   │
│  │  1. Nhận message từ user                          │   │
│  │  2. Thu thập CONTEXT từ DB ◄──── MongoDB          │   │
│  │  3. Xây dựng PROMPT (system + context + question) │   │
│  │  4. Gọi AI API ─────────────────► Claude API      │   │
│  │  5. Trả response về frontend                      │   │
│  │                                                    │   │
│  └────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────┘
```

---

## 2. CÁCH TRUYỀN CONTEXT — PHẦN QUAN TRỌNG NHẤT

Khi user hỏi, backend tự động collect TẤT CẢ dữ liệu liên quan và đưa vào prompt.

### Bước 1: Thu thập context từ MongoDB

```typescript
// backend: ai-chat.service.ts

async buildContext(userId: string): Promise<string> {
  // Song song fetch tất cả data
  const [portfolios, positions, trades, plans, strategies,
         riskProfile, journals, alerts] = await Promise.all([
    this.portfolioService.findByUser(userId),
    this.positionService.findOpenByUser(userId),
    this.tradeService.findRecentByUser(userId, 20), // 20 GD gần nhất
    this.tradePlanService.findByUser(userId),
    this.strategyService.findByUser(userId),
    this.riskService.findProfileByUser(userId),
    this.journalService.findRecentByUser(userId, 5),
    this.alertService.findActiveByUser(userId),
  ]);

  // Lấy giá thị trường hiện tại
  const symbols = positions.map(p => p.symbol);
  const marketPrices = await this.marketService.getPrices(symbols);

  // Tính toán metrics
  const totalValue = positions.reduce((sum, p) => {
    const price = marketPrices[p.symbol] || p.avgPrice;
    return sum + (p.quantity * price);
  }, 0);

  const totalInvested = positions.reduce((sum, p) =>
    sum + (p.quantity * p.avgPrice), 0);

  const totalPnL = totalValue - totalInvested;
  const pnlPercent = totalInvested > 0
    ? ((totalPnL / totalInvested) * 100).toFixed(2)
    : 0;

  // Format thành text có cấu trúc
  return `
=== DANH MỤC ĐẦU TƯ ===
${portfolios.map(p => `- ${p.name}: Vốn ${formatVND(p.capital)}`).join('\n')}

=== VỊ THẾ ĐANG MỞ ===
Tổng giá trị: ${formatVND(totalValue)} | P&L: ${formatVND(totalPnL)} (${pnlPercent}%)
${positions.map(p => {
  const price = marketPrices[p.symbol] || p.avgPrice;
  const pnl = (price - p.avgPrice) * p.quantity;
  const pct = ((price - p.avgPrice) / p.avgPrice * 100).toFixed(1);
  return `- ${p.symbol}: ${p.quantity} CP @ ${formatVND(p.avgPrice)} → Hiện ${formatVND(price)} (${pct}%) | P&L: ${formatVND(pnl)}`;
}).join('\n')}

=== KẾ HOẠCH GIAO DỊCH ===
${plans.map(p => `- ${p.symbol} ${p.direction}: Giá ${formatVND(p.entryPrice)} SL ${formatVND(p.stopLoss)} TP ${formatVND(p.takeProfit)} | Trạng thái: ${p.status}`).join('\n')}

=== CHIẾN LƯỢC ===
${strategies.map(s => `- ${s.name}: ${s.description?.substring(0, 100)}`).join('\n')}

=== HỒ SƠ RỦI RO ===
${riskProfile ? `Vị thế tối đa: ${riskProfile.maxPositionSize}%, R:R tối thiểu: ${riskProfile.minRR}, Rủi ro DM: ${riskProfile.maxPortfolioRisk}%` : 'Chưa thiết lập'}

=== GIAO DỊCH GẦN ĐÂY ===
${trades.slice(0, 10).map(t => `- ${t.date}: ${t.direction} ${t.symbol} ${t.quantity} CP @ ${formatVND(t.price)} | ${t.pnl ? formatVND(t.pnl) : ''}`).join('\n')}

=== NHẬT KÝ GẦN ĐÂY ===
${journals.map(j => `- ${j.date} (${j.symbol}): ${j.content?.substring(0, 150)}`).join('\n')}

=== CẢNH BÁO ĐANG HOẠT ĐỘNG ===
${alerts.map(a => `- ${a.symbol}: ${a.condition} ${formatVND(a.targetPrice)}`).join('\n')}

=== GIÁ THỊ TRƯỜNG HIỆN TẠI ===
${Object.entries(marketPrices).map(([sym, price]) => `${sym}: ${formatVND(price)}`).join(' | ')}
Ngày: ${new Date().toLocaleDateString('vi-VN')}
  `.trim();
}
```

### Bước 2: Xây dựng System Prompt

```typescript
const SYSTEM_PROMPT = `
Bạn là Investment Mate AI — trợ lý đầu tư cá nhân thông minh cho thị trường chứng khoán Việt Nam.

NGUYÊN TẮC:
1. Trả lời bằng tiếng Việt, ngắn gọn, dễ hiểu
2. Dựa trên DỮ LIỆU THỰC TẾ của user (được cung cấp bên dưới), KHÔNG bịa số liệu
3. Luôn đưa ra phân tích DỰA TRÊN CONTEXT, không chung chung
4. Khi gợi ý mua/bán, luôn kèm lý do và cảnh báo rủi ro
5. Nhắc user rằng đây là gợi ý tham khảo, quyết định cuối cùng thuộc về họ
6. Nếu user hỏi về mã KHÔNG có trong danh mục, vẫn có thể tư vấn dựa trên kiến thức chung
7. Format trả lời: dùng bullet points, in đậm số quan trọng, emoji vừa phải

KHẢ NĂNG:
- Phân tích danh mục: lãi/lỗ, tỷ trọng, rủi ro tập trung
- Gợi ý hành động: nên mua thêm, giữ, hay cắt lỗ
- Đánh giá chiến lược: hiệu quả, win rate, cải thiện
- So sánh: vị thế nào tốt nhất/kém nhất
- Tính toán: position sizing, R:R ratio, break-even price
- Tâm lý giao dịch: nhận biết FOMO, revenge trading từ nhật ký
`;
```

### Bước 3: Gọi AI API

```typescript
// Dùng Claude API (Anthropic)
async chat(userId: string, message: string, history: Message[]): Promise<string> {
  const context = await this.buildContext(userId);

  const response = await this.anthropic.messages.create({
    model: 'claude-sonnet-4-20250514',
    max_tokens: 1024,
    system: SYSTEM_PROMPT + '\n\n' + context,
    messages: [
      ...history.map(m => ({
        role: m.role,
        content: m.content
      })),
      { role: 'user', content: message }
    ]
  });

  return response.content[0].text;
}
```

---

## 3. CÁC CÂU HỎI MÀ AI CÓ THỂ TRẢ LỜI

### Nhóm 1: Phân tích danh mục

| Câu hỏi | AI trả lời dựa trên |
|---|---|
| "Danh mục tôi đang thế nào?" | Positions + market prices → tổng P&L, top winner/loser |
| "Tôi đang lỗ bao nhiêu ở FPT?" | Position FPT + giá hiện tại → P&L chi tiết |
| "Danh mục tôi có đa dạng không?" | Positions → tỷ trọng ngành, tương quan, cảnh báo tập trung |
| "Tổng phí và thuế tôi đã trả?" | Trades → tổng hợp phí mua 0.15% + thuế bán 0.1% |

**Ví dụ trả lời:**
```
🤖 Danh mục bạn hiện tại:

📊 Tổng giá trị: **15.540.000đ** (đầu tư: 25.334.800đ)
📉 Lỗ chưa hiện thực: **-9.794.800đ (-38.7%)**

Vị thế:
• **FPT**: 200 CP @ 126.674đ → hiện 77.800đ (⚠️ **-38.6%**)
  Đang cách stop-loss -50.2% — đáng lo ngại

💡 Nhận xét:
- Danh mục tập trung 100% vào 1 mã (FPT) — rủi ro rất cao
- FPT đang trong vùng lỗ sâu, cần đánh giá lại thesis đầu tư
- Gợi ý: Review lại lý do mua ban đầu, nếu thesis không thay đổi
  thì có thể DCA, nếu đã thay đổi thì nên cắt lỗ

⚠️ Đây là gợi ý tham khảo, quyết định cuối cùng thuộc về bạn.
```

### Nhóm 2: Gợi ý hành động

| Câu hỏi | AI trả lời dựa trên |
|---|---|
| "FPT nên giữ hay bán?" | Position + Trade Plan + Strategy + Journal → phân tích |
| "Nên mua thêm mã gì?" | Positions + Risk Profile → gợi ý đa dạng hóa |
| "Risk tôi có ổn không?" | Risk Profile + Positions → kiểm tra tuân thủ |
| "Tôi nên làm gì hôm nay?" | Daily Routine + Positions + Alerts → checklist cá nhân |

### Nhóm 3: Tính toán

| Câu hỏi | AI trả lời dựa trên |
|---|---|
| "Mua HPG thì nên mua bao nhiêu CP?" | Risk Profile + Portfolio capital → position sizing |
| "Break-even FPT là bao nhiêu?" | Position data → tính giá hòa vốn |
| "R:R ratio kế hoạch FPT?" | Trade Plan → tính R:R |
| "Nếu FPT về 90k thì lãi bao nhiêu?" | Position → tính P&L scenario |

### Nhóm 4: Tâm lý & Review

| Câu hỏi | AI trả lời dựa trên |
|---|---|
| "Tôi có đang FOMO không?" | Journals + recent trades → phân tích pattern |
| "Chiến lược nào hiệu quả nhất?" | Strategies + trades → so sánh win rate, PF |
| "Tháng này giao dịch thế nào?" | Trades trong tháng → tổng kết |
| "Sai lầm lớn nhất của tôi?" | Trades + Journals → phân tích GD thua lỗ nhất |

---

## 4. UI DESIGN — CHAT WIDGET

### Phương án A: Floating Chat (Gợi ý ✅)

```
┌─── Mọi trang ──────────────────────────────────────────┐
│                                                          │
│  [Nội dung trang hiện tại]                              │
│                                                          │
│                                                          │
│                                        ┌────────────┐   │
│                                        │ 💬 Hỏi AI  │   │
│                                        └────────────┘   │
│                                          ↑ Floating     │
│                                            button       │
└──────────────────────────────────────────────────────────┘

Click vào → Mở panel chat:

┌─────────────────────────────────────────────── ✕ ──────┐
│  🤖 Investment Mate AI                                  │
│─────────────────────────────────────────────────────────│
│                                                          │
│  🤖 Chào bạn! Tôi có thể giúp gì?                      │
│                                                          │
│  Gợi ý:                                                  │
│  ┌─────────────────┐ ┌──────────────────┐               │
│  │ Danh mục thế nào│ │ FPT nên giữ/bán? │               │
│  └─────────────────┘ └──────────────────┘               │
│  ┌─────────────────┐ ┌──────────────────┐               │
│  │ Hôm nay làm gì? │ │ Gợi ý mã mua    │               │
│  └─────────────────┘ └──────────────────┘               │
│                                                          │
│  👤 FPT đang lỗ nặng, tôi nên làm gì?                  │
│                                                          │
│  🤖 FPT đang lỗ **-38.6%** (mua 126.674đ, hiện         │
│  77.800đ). Đây là phân tích:                             │
│                                                          │
│  📊 **Chiến lược hiện tại:** Deep Value / Contrarian     │
│  — mua khi thị trường ghét bỏ, cơ bản vẫn vững.        │
│                                                          │
│  Có 3 lựa chọn:                                         │
│  1. **Cắt lỗ** nếu thesis đã thay đổi                   │
│  2. **Giữ** nếu tin triển vọng dài hạn                   │
│  3. **DCA** thêm ở giá hiện tại để hạ giá TB            │
│     → Mua thêm 100 CP @ 77.800 = TB mới ~110.000       │
│                                                          │
│  ⚠️ Lưu ý: Vị thế chiếm 100% DM, rủi ro tập trung     │
│  rất cao. Cân nhắc giảm tỷ trọng.                       │
│                                                          │
│  [📋 Tạo Trade Plan DCA FPT]                            │
│                                                          │
│─────────────────────────────────────────────────────────│
│  💭 Nhập câu hỏi...                           [Gửi ➤] │
└──────────────────────────────────────────────────────────┘
```

### Phương án B: Trang riêng `/ai-chat`
- Full-page chat experience
- Lịch sử hội thoại
- Có thể save/bookmark câu trả lời hay

### Gợi ý: Làm CẢ HAI — Widget mặc định, link "Mở rộng" → trang riêng

---

## 5. CONTEXT-AWARE: AI BIẾT USER ĐANG Ở TRANG NÀO

```typescript
// Frontend gửi thêm currentPage
POST /api/v1/ai/chat
{
  "message": "Tôi nên làm gì?",
  "currentPage": "/positions",     // ← trang hiện tại
  "currentSymbol": "FPT",          // ← mã đang xem (nếu có)
  "conversationId": "abc123"       // ← lịch sử hội thoại
}
```

Backend thêm context tùy trang:

| Trang | Context thêm |
|---|---|
| `/positions` | Chi tiết tất cả vị thế + P&L realtime |
| `/trade-plan` | Kế hoạch đang soạn hoặc đang xem |
| `/analytics` | Win rate, Sharpe, Sortino, top metrics |
| `/risk-dashboard` | Risk score, drawdown, violations |
| `/market-data` | Giá hiện tại + top biến động |
| `/journals` | 5 journal entries gần nhất |

**Ví dụ:** User đang ở `/trade-plan` đang soạn HPG → hỏi "Plan này ổn không?" → AI biết đang nói về HPG plan và đánh giá cụ thể.

---

## 6. ACTIONABLE RESPONSES — AI CÓ THỂ "LÀM" CHỨ KHÔNG CHỈ "NÓI"

AI trả lời kèm **Action Buttons** mà user click được:

```
🤖 Gợi ý DCA FPT thêm 100 CP @ 77.800đ:
- Giá TB mới: ~110.000đ (từ 126.674đ)
- Break-even mới: 110.000đ (giảm 13%)

[📋 Tạo Trade Plan DCA FPT] ← Click → navigate /trade-plan?symbol=FPT&...
[🧮 Xem Position Sizing]    ← Click → navigate /risk?tab=position-sizing
[📊 Xem chart FPT]          ← Click → navigate /market-data?symbol=FPT
```

Backend trả về structured response:

```typescript
interface AiResponse {
  text: string;           // Markdown text
  actions?: {
    label: string;        // "Tạo Trade Plan DCA FPT"
    icon: string;         // "📋"
    route: string;        // "/trade-plan"
    queryParams?: Record<string, string>;
  }[];
  suggestedQuestions?: string[];  // Follow-up questions
}
```

---

## 7. TRIỂN KHAI TỪNG BƯỚC

### Phase 1 — MVP (1 tuần)

```
Effort: ████░░░░░░ 40%
```

- [ ] Backend: `AiChatModule` với 1 endpoint `/api/v1/ai/chat`
- [ ] Backend: `buildContext()` collect data từ MongoDB
- [ ] Backend: Gọi Claude API (Anthropic SDK)
- [ ] Frontend: Floating chat button + panel (Angular component)
- [ ] Frontend: Send/receive messages, render markdown
- [ ] 5 suggested questions mặc định

**Chi phí API:** Claude Sonnet ~$3/1M input tokens, ~$15/1M output tokens
- Context ~2000 tokens/request, response ~500 tokens
- 100 câu hỏi/ngày ≈ $0.10/ngày ≈ **$3/tháng**

### Phase 2 — Smart Features (1-2 tuần)

- [ ] Context-aware (biết trang hiện tại)
- [ ] Action buttons trong response
- [ ] Conversation history (MongoDB collection)
- [ ] Suggested questions dynamic (dựa trên context)
- [ ] Streaming response (SSE) cho UX mượt

### Phase 3 — Advanced (2-3 tuần)

- [ ] Function calling: AI tự gọi internal APIs
  ```
  User: "Tạo plan mua VNM 100 CP @ 61k, SL 57k"
  AI: Gọi POST /api/trade-plans → tạo xong → "Đã tạo Trade Plan VNM ✅"
  ```
- [ ] Trang `/ai-chat` full-page với lịch sử
- [ ] Voice input (Web Speech API)
- [ ] Daily AI digest: sáng auto-generate "Tổng quan hôm nay"
- [ ] Rate limiting + usage tracking

---

## 8. LỰA CHỌN AI PROVIDER

| Provider | Model | Giá (1M tokens) | Ưu điểm | Nhược điểm |
|---|---|---|---|---|
| **Anthropic** | Claude Sonnet | $3 in / $15 out | Phân tích tốt, an toàn, tiếng Việt tốt | Cần API key |
| **OpenAI** | GPT-4o-mini | $0.15 in / $0.60 out | Rẻ nhất, nhanh | Tiếng Việt kém hơn |
| **OpenAI** | GPT-4o | $2.50 in / $10 out | Đa năng | Đắt hơn |
| **Google** | Gemini 1.5 Flash | $0.075 in / $0.30 out | Rẻ nhất, context lớn | Ít phổ biến |
| **Local** | Ollama + Qwen2.5 | Miễn phí | Không phí API | Cần server mạnh |

**Gợi ý:** Bắt đầu với **Claude Sonnet** (phân tích tài chính tốt, tiếng Việt chuẩn) hoặc **GPT-4o-mini** (rẻ hơn 20x, vẫn đủ tốt cho Q&A).

---

## 9. BẢO MẬT & GIỚI HẠN

```typescript
// Rate limiting
@UseGuards(ThrottlerGuard)    // 20 requests/phút/user
@UseGuards(AuthGuard)         // Chỉ user đã login

// Không gửi dữ liệu nhạy cảm
// KHÔNG gửi: password, token, email cá nhân
// CHỈ gửi: portfolio data, trades, positions (đã anonymize)

// Token limit
const MAX_CONTEXT_TOKENS = 4000;  // Giới hạn context gửi đi
const MAX_RESPONSE_TOKENS = 1024; // Giới hạn response
```

---

## 10. TÓM TẮT

| Câu hỏi | Trả lời |
|---|---|
| Có tích hợp được không? | **CÓ** — rất khả thi |
| Context lấy từ đâu? | MongoDB — tất cả data của user |
| Tích hợp thế nào? | Backend gọi AI API, truyền context, trả response |
| Mất bao lâu? | MVP 1 tuần, full 3-4 tuần |
| Chi phí? | ~$3-10/tháng (100-300 câu hỏi/ngày) |
| User hỏi gì được? | Danh mục, gợi ý mua/bán, tính toán, review chiến lược |
| UI như thế nào? | Floating chat widget + trang riêng |

**Investment Mate + AI = Từ "ghi chép giao dịch" → "Trợ lý đầu tư cá nhân 24/7"**