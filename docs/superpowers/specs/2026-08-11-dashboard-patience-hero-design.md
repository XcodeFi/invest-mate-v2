# Màn tĩnh tâm trên trang chủ — thiết kế

Ngày: 2026-08-11 · Trạng thái: đã duyệt, chờ viết plan

## Thuật ngữ

| Viết tắt | Tên đầy đủ | Nghĩa ở tài liệu này |
|---|---|---|
| FOMO | Fear Of Missing Out | Sợ bỏ lỡ — thấy mã khác tăng nên sốt ruột muốn mua theo |
| API | Application Programming Interface | Đường gọi dữ liệu giữa frontend và backend |
| SVG | Scalable Vector Graphics | Định dạng ảnh vector vẽ thẳng trong HTML, phóng to không vỡ |
| CSS | Cascading Style Sheets | Ngôn ngữ định kiểu, ở đây dùng để chạy hoạt hoạ mà không cần JavaScript |
| UTC | Coordinated Universal Time | Giờ chuẩn quốc tế, giờ Việt Nam = UTC + 7 |
| VN | Việt Nam | Dùng cho "giờ VN", "ngày VN" |
| TDD | Test-Driven Development | Viết test trước, code sau |

## Vì sao làm

Ba câu của chủ sở hữu ứng dụng, ghi nguyên văn vì chúng là toàn bộ yêu cầu:

> Đầu tư chứng khoán chứ không phải là chơi chứng khoán.
> Tiền là của mình, nó là con số, nhưng mất tiền là thật.
> Khi cảm xúc vào, thì không hành động.

Câu thứ ba là một **luật dừng**, không phải một lời nhắc. Trang `/dashboard` hiện tại xếp chồng 11 khối số liệu và mở đầu bằng danh sách việc cần làm — không có một khoảng lặng nào giữa lúc muốn hành động và lúc hành động.

Mục tiêu: chèn khoảng lặng đó, và cho nó quyền chạm vào tay người dùng chứ không chỉ nói suông.

## Không làm

- Không đổi bố cục hay logic của 11 khối hiện có (trừ việc xoá Giao dịch nhanh, xem §4).
- Không thêm âm thanh.
- Không có "kỷ lục chờ dài nhất" — muốn đúng thì phải lưu lịch sử; một kỷ lục biết tự quên là một lời nói dối nhỏ.
- Không có cá cắn câu trong hoạt hoạ — phần thưởng dành cho việc *chờ*, không dành cho việc *có biến*.
- Không chặn đường vào `/trade-plan` và `/trade-wizard` (đã cân nhắc, để lần sau; lần này giữ trong phạm vi dashboard).

## 1. Hero tĩnh tâm

Một khối full-width đặt trên cùng `/dashboard`, ngay trước Hàng đợi quyết định. Mọi widget khác giữ nguyên thứ tự, chỉ bị đẩy xuống.

```
┌────────────────────────────────────────────────────────────┐
│  ░░░░░  trời chuyển màu theo mức tĩnh  ░░░░░               │
│         🧍 ────────────╮                                   │
│   ～～～～～～～～～～～～●～～～～～～～～～～～～～～～～    │
│                                                            │
│      "Thị trường chuyển tiền từ người sốt ruột             │
│       sang người kiên nhẫn."        — Warren Buffett       │
│                                                            │
│  ● Đã 12 ngày chưa đặt lệnh                                │
│    Tiền là con số trên màn hình. Mất tiền là thật.         │
├────────────────────────────────────────────────────────────┤
│  Giờ anh đang thế nào?   [Bình tĩnh] [FOMO] [Sợ] [Cay cú]  │
└────────────────────────────────────────────────────────────┘
```

Dòng "Tiền là con số trên màn hình. Mất tiền là thật." là hằng số, luôn hiện, không xoay vòng.

### Hoạt hoạ

SVG vẽ tay, hoạt hoạ thuần CSS, **không có vòng lặp JavaScript** — trang này đã có Chart.js và 11 widget, không thêm việc cho luồng chính.

Cảnh nhận đúng hai đầu vào:

```
calm  = min(daysSince, 14) / 14   // 0 = vừa động tay, 1 = phẳng như gương
dim   = boolean                   // true khi tâm trạng ≠ Calm (§2)
```

`calm` điều khiển biên độ sóng, tốc độ sóng và độ ấm của trời. `dim` chỉ làm tối lớp trời đi một bậc — nó **không** đụng vào sóng, vì sóng đang kể một sự thật khác (số ngày chưa động tay) và không được để tâm trạng viết đè lên sự thật đó.

| daysSince | Mặt nước | Trời | Phao |
|---|---|---|---|
| 0–1 | sóng gấp, biên độ lớn, đục | xám lạnh | nhấp nhô liên tục |
| 2–6 | sóng dịu dần | hửng | lắc nhẹ |
| 7–13 | gợn lăn tăn | ngả vàng ấm | gần như đứng |
| ≥ 14 | phẳng, có bóng phản chiếu | hoàng hôn ấm | bất động |

Trần 14 ngày **chỉ dùng cho hình ảnh**. Số ngày hiển thị luôn là số thật — chờ 200 ngày thì ghi 200.

`daysSince = null` (chưa có lệnh nào bao giờ): hiển thị `calm = 1` kèm chữ "Chưa có lệnh nào", **không** ghi một con số ngày bịa ra.

Tôn trọng `prefers-reduced-motion`: tắt hết chuyển động, giữ ảnh tĩnh ở đúng mức `calm` đó.

## 2. Luật dừng

Hỏi **một lần mỗi ngày** (ngày VN). Chọn xong, thanh hỏi thu lại thành `Đang: FOMO · đổi`.

**Bình tĩnh** → không có gì thay đổi; châm ngôn lấy từ nhóm bình tĩnh.

**FOMO / Sợ / Cay cú** → ba việc cùng lúc:

1. Châm ngôn đổi sang nhóm tương ứng
2. Trời trong cảnh câu tối đi một bậc
3. `<app-decision-queue>` bị phủ một lớp mờ:

```
┌──────────────────────────────┐
│ □ Hàng đợi quyết định (3)    │
│ ░░░░░░░░░░░░░░░░░░░░░░░░░░░░ │
│  Anh đang chấm là FOMO.      │
│  Danh sách này tối nay       │
│  vẫn ở đây.                  │
│  [ Vẫn xem bây giờ ]         │
└──────────────────────────────┘
```

Bấm `Vẫn xem bây giờ` là mở, giữ mở tới hết ngày. **Không cấm** — chỉ bắt trả giá bằng một cú bấm có ý thức. Cú bấm đó được ghi lại (`OverrodeAt`, §3.2).

## 3. Dữ liệu

### 3.1 Số ngày chưa đặt lệnh

```
GET /api/trades/last-activity
→ { "lastTradeDate": "2026-07-30", "daysSince": 12 }
→ { "lastTradeDate": null, "daysSince": null }
```

Query lọc theo `UserId` của người gọi, sắp `TradeDate` giảm dần, lấy 1.

**Vì sao cần endpoint mới thay vì dùng dữ liệu sẵn có.** Frontend đã có `ActivePosition.recentTrades[].tradeDate` qua `PositionsService`, nhưng nó chỉ thấy lệnh của **vị thế đang mở**. Bán sạch một mã thì vị thế biến mất và lệnh đó tàng hình — đồng hồ kiên nhẫn sẽ nhảy vọt lên đúng vào lúc người dùng vừa làm việc cảm tính nhất. Một đồng hồ nói dối theo chiều đó thì thà không có.

Route chữ `last-activity` được ASP.NET Core ưu tiên hơn `{id}` nên không đụng `GET /api/trades/{id}` đang có.

### 3.2 Tâm trạng

Lưu ở server theo tài khoản (không phải localStorage) để mở máy khác vẫn thấy, và để sau này đối chiếu được "những hôm chấm FOMO tôi đã làm gì".

Collection `mood_check_ins`, trường PascalCase (dự án không đăng ký convention camelCase), unique index `(UserId, DateKey)`:

| Trường | Kiểu | Ghi chú |
|---|---|---|
| `UserId` | string | Mọi query lọc theo đây, không ngoại lệ |
| `DateKey` | string | `"2026-08-11"` — **chuỗi, không phải DateTime** |
| `Mood` | enum | `Calm` \| `Fomo` \| `Fear` \| `Revenge` |
| `CheckedAt` | DateTime | UTC |
| `OverrodeAt` | DateTime? | Dấu vết đã bấm "Vẫn xem bây giờ" |

**`DateKey` là chuỗi — cố ý.** Lưu nửa đêm giờ VN xuống Mongo thì đọc lên thành 17:00 hôm trước, và mọi so sánh mốc ngày lệch một ngày trong khi unit test vẫn xanh vì test không đi vòng qua database. Chuỗi `"YYYY-MM-DD"` thì không có gì để lệch.

**Server tự tính "hôm nay"**, frontend không gửi ngày lên: `UtcNow + 07:00` (VN không có giờ mùa hè nên cộng cứng là an toàn). Để frontend gửi thì máy đặt sai giờ là hỏng.

```
GET  /api/mood/today   → { "mood": "Fomo", "overrode": false }
                       → { "mood": null,   "overrode": false }
POST /api/mood         ← { "Mood": "Fomo" }     ghi đè bản hôm nay (upsert)
POST /api/mood/override                          đóng dấu OverrodeAt
```

**Đổi tâm trạng trong ngày thì `OverrodeAt` bị xoá về `null`** — lớp phủ quay lại. Nếu giữ lại, người dùng chỉ cần chấm "Bình tĩnh" rồi chấm lại "FOMO" là mở khoá vĩnh viễn mà không phải bấm qua lớp phủ lần nào.

Enum truyền dạng chuỗi an toàn: `JsonStringEnumConverter` đã đăng ký toàn cục ở `ApiJsonConfig`.

**Vì sao giữ `OverrodeAt`.** Nó là thứ duy nhất sau này trả lời được câu quan trọng nhất — luật dừng có tác dụng thật không, hay tháng nào cũng bấm bỏ qua 9/10 lần. Không có nó thì tính năng này không bao giờ tự chứng minh được.

## 4. Xoá widget Giao dịch nhanh

Chủ sở hữu ít dùng, và nó là lối đặt lệnh nhanh nhất trên trang chủ — bỏ đi vừa dọn trang vừa đúng hướng của tính năng này.

Xoá template dòng 610–704 và khối method dưới comment `// ─── Quick Trade Widget ───` ở cuối `dashboard.component.ts`.

Chỉ dọn đúng những gì việc xoá làm mồ côi:

| Xoá | Giữ — còn dùng chỗ khác |
|---|---|
| `UppercaseDirective` (import + mảng `imports`) | `FormsModule` — ô nhập CAGR mục tiêu, số năm |
| `isBuyTrade` (import + field) | `MarketDataService` — `getBatchPrices` của bảng vị thế |
| `RiskService`, `RiskProfile` (import + inject) | `catchError` / `of` — dùng khắp file |
| 7 field `qt*` | |
| `onQtSymbolBlur`, `calcQtStats`, `openInTradePlan` | |

Không spec nào tham chiếu tới widget này. Đường tới `/trade-plan` vẫn còn nguyên ở menu — chỉ mất lối tắt trên trang chủ.

## 5. Cấu trúc file

Bốn mảnh nhỏ, mỗi mảnh một việc, test riêng được:

| Tệp | Việc | Phụ thuộc |
|---|---|---|
| `dashboard/widgets/fishing-scene.component.ts` | Chỉ vẽ. Nhận `calm: 0..1` và `dim: boolean`. Không biết gì về giao dịch | không |
| `dashboard/widgets/patience-quotes.ts` | Dữ liệu thuần + hàm `pickQuote(mood, seed)` | không |
| `core/services/mood.service.ts` | Gọi 3 endpoint tâm trạng | HttpClient |
| `dashboard/widgets/patience-hero.component.ts` | Ghép ba thứ trên, phát `moodChange` | ba cái trên |

`dashboard.component.ts` chỉ thêm khoảng 6 dòng: đặt `<app-patience-hero>` lên đầu, hứng `moodChange`, bọc `<app-decision-queue>` trong lớp phủ. Không đụng logic số liệu nào đang có.

`trade.service.ts` thêm một method `getLastActivity()`.

Backend: `GetLastTradeActivityQuery` + handler, `MoodCheckIn` entity + repository + `MoodController`, và một method trên `ITradeRepository`.

**Lưu ý:** dự án không có global auth interceptor — service mới phải tự gắn header qua `getHeaders()` như các service khác, nếu không sẽ build xanh rồi 401 lúc chạy.

## 6. Thư viện châm ngôn

Khoảng 22 câu viết sẵn trong `patience-quotes.ts`, tiếng Việt có dấu, chia theo nhóm tâm trạng. Câu của người thật thì ghi tên; câu ẩn dụ người câu thì để trống tác giả — **không bịa tên cho câu tự viết**.

**Bình tĩnh**
- "Thị trường chuyển tiền từ người sốt ruột sang người kiên nhẫn." — Warren Buffett
- "Tiền lớn không nằm ở chỗ mua bán, mà ở chỗ ngồi yên." — Charlie Munger
- "Trong ngắn hạn thị trường là cái máy bỏ phiếu; về dài hạn nó là cái cân." — Benjamin Graham
- "Phẩm chất quan trọng nhất của nhà đầu tư là tính khí, không phải trí tuệ." — Warren Buffett
- "Mặt hồ phẳng không phải vì không có gió. Vì đã đủ lâu không ai quăng đá."
- "Người câu giỏi không phải người quăng nhiều nhất."

**FOMO**
- "Cá không chạy đi đâu. Người sốt ruột mới chạy."
- "Hãy sợ khi người khác tham, và tham khi người khác sợ." — Warren Buffett
- "Không phải suy nghĩ làm tôi kiếm được tiền lớn. Luôn luôn là việc ngồi yên." — Jesse Livermore
- "Chuyến tàu này anh lỡ. Ngày mai có chuyến khác. Tiền mất thì không có chuyến khác."
- "Quăng câu vì thấy người bên cạnh giật được cá — đó không phải là câu, đó là đuổi."

**Sợ**
- "Chìa khoá thật sự để kiếm tiền từ cổ phiếu là đừng để bị doạ ra khỏi chúng." — Peter Lynch
- "Kẻ thù lớn nhất của nhà đầu tư nhiều khả năng là chính anh ta." — Benjamin Graham
- "Giá là thứ bạn trả. Giá trị là thứ bạn nhận." — Warren Buffett
- "Nước động không có nghĩa là phải kéo cần lên."
- "Sợ thì ngồi im. Ngồi im không mất gì cả."

**Cay cú**
- "Tiền mất rồi không biết anh là ai. Nó không quay lại vì anh tức."
- "Quy tắc số 1: đừng để mất tiền. Quy tắc số 2: đừng quên quy tắc số 1." — Warren Buffett
- "Thị trường không nợ anh một lần gỡ."
- "Mất cá thì về. Mất cần câu thì hết câu."
- "Lệnh gỡ gạc là lệnh đắt nhất anh từng đặt."
- "Hôm nay không đặt lệnh nào cũng là một quyết định. Thường là quyết định đúng."

`pickQuote(mood, seed)` chọn theo `seed` là `DateKey` — cùng một ngày thì thấy cùng một câu, không nhấp nháy mỗi lần render.

Chưa chấm tâm trạng hôm nay (`mood = null`) thì dùng nhóm **Bình tĩnh**. Đó là trạng thái mặc định trước khi người dùng nói khác đi, và cũng là nhóm duy nhất không kèm lớp phủ.

## 7. Test (viết trước, Red → Green)

**Backend**
- `GetLastTradeActivity`: chưa có lệnh → `null`; trả đúng `TradeDate` lớn nhất; **lệnh của user khác không lọt vào**
- `MoodCheckIn`: chấm hai lần trong ngày → vẫn 1 bản ghi; qua ngày mới → `mood: null`; **bản ghi của user khác không lọt vào**; lúc 00:30 giờ VN vẫn ra `DateKey` hôm nay theo VN chứ không phải hôm qua theo UTC
- `override` khi chưa chấm tâm trạng → không tạo bản ghi ma

**Frontend**
- `pickQuote`: đúng nhóm tâm trạng; cùng seed ra cùng câu; không bao giờ rỗng
- `fishing-scene`: `calm` ngoài khoảng bị kẹp về 0..1; `prefers-reduced-motion` gắn class tắt hoạt hoạ
- `patience-hero`: hiện đúng số ngày; `daysSince = null` → "Chưa có lệnh nào"; bấm tâm trạng thì gọi API và phát sự kiện
- `dashboard`: tâm trạng ≠ `Calm` → lớp phủ hiện trên Hàng đợi quyết định; bấm "Vẫn xem bây giờ" → phủ biến mất và gọi `POST /api/mood/override`

Hai ca ownership ở trên là ca quan trọng nhất, không phải ca vui.

## 8. Tài liệu phải đồng bộ trước khi commit

- `docs/architecture.md` — component mới, service mới, controller mới
- `docs/business-domain.md` — entity `MoodCheckIn`, 4 endpoint mới
- `docs/features.md` — tính năng màn tĩnh tâm; đánh dấu Giao dịch nhanh đã gỡ
- `docs/project-context.md` — quyết định UX: vì sao trang chủ có luật dừng
- `frontend/src/assets/CHANGELOG.md`
- `frontend/src/assets/docs/*.md` + đăng ký mục Help

## 9. Giới hạn đã biết

- Luật dừng chỉ phủ Hàng đợi quyết định. Vào thẳng `/trade-plan` từ menu thì không bị chặn — có ý thức chấp nhận ở bản này.
- Tâm trạng là tự chấm. Người dùng tự lừa mình thì hệ thống không biết. `OverrodeAt` là cách duy nhất phát hiện gián tiếp.
- Chưa có màn đối chiếu "hôm FOMO tôi đã làm gì". Dữ liệu đủ để làm sau, nhưng bản này không làm.
