# Risk Management & Strategy Features

## 1. Backtesting UI (`/backtesting`)

**Muc dich:** Test thu chien luoc tren du lieu lich su truoc khi ap dung tien that.

### Chuc nang
- Chon chien luoc da tao, nhap khung thoi gian va von ban dau
- Backend xu ly async (Worker service), frontend auto-poll ket qua moi 5s
- 3 tab ket qua:
  - **Ket qua:** CAGR, Sharpe Ratio, Max Drawdown, Win Rate, Profit Factor, Tong GD (Thang/Thua)
  - **Equity Curve:** Bieu do gia tri danh muc theo thoi gian + bang chi tiet (gia tri, loi nhuan ngay, loi nhuan tich luy)
  - **Giao dich:** Bang giao dich mo phong (ma CK, loai, gia vao/ra, KL, P&L, %)

### API Endpoints (da co san)
| Method | Endpoint | Mo ta |
|--------|----------|-------|
| POST | `/api/v1/backtests` | Gui backtest moi (async) |
| GET | `/api/v1/backtests` | Danh sach backtest cua user |
| GET | `/api/v1/backtests/{id}` | Chi tiet + ket qua day du |
| GET | `/api/v1/backtests/{id}/equity-curve` | Du lieu equity curve |
| GET | `/api/v1/backtests/{id}/trades` | Giao dich mo phong |

### Request body (POST)
```json
{
  "strategyId": "string",
  "name": "string",
  "startDate": "2024-01-01",
  "endDate": "2024-12-31",
  "initialCapital": 100000000
}
```

### Backtest Status Flow
```
Pending → Running → Completed / Failed
```

---

## 2. Position Sizing Calculator (`/position-sizing`)

**Muc dich:** Tinh so luong co phieu toi uu cho moi giao dich dua tren muc rui ro chap nhan.

### Chuc nang
- Nhap: Tong gia tri danh muc, % rui ro/GD, gia vao lenh, stop-loss, take-profit, % vi the toi da
- Tu dong lay Risk Profile tu danh muc (neu chon portfolio)
- Ket qua:
  - So co phieu toi uu (lam tron xuong lo 100 CP)
  - Gia tri vi the va % danh muc
  - Tien rui ro toi da
  - R:R Ratio
  - Loi nhuan / Lo tiem nang
  - Canh bao khi vuot gioi han
- Bang tham chieu nhanh: so CP theo cac muc rui ro 0.5%, 1%, 1.5%, 2%, 3%, 5%

### Cong thuc
```
Max Risk Amount = Account Balance × Risk%
Risk Per Share = |Entry Price - Stop Loss Price|
Optimal Shares = floor(Max Risk Amount / Risk Per Share / 100) × 100
Position Value = Optimal Shares × Entry Price
R:R Ratio = |Target - Entry| / |Entry - Stop Loss|
```

### Khong can backend API - tinh toan hoan toan tren frontend.

---

## 3. Trade Plan / Pre-trade Checklist (`/trade-plan`)

**Muc dich:** Dam bao moi giao dich deu duoc kiem tra ky truoc khi vao lenh, tranh FOMO va sai lam cam xuc.

### Chuc nang
- Nhap thong tin giao dich: Ma CK, huong (Mua/Ban), gia vao/SL/TP, so luong, chien luoc
- Hien thi quy tac chien luoc (entry/exit/risk rules) de tham chieu
- Tinh toan real-time: R:R ratio, rui ro/CP, gia tri vi the, P&L tiem nang
- Muc do tu tin (1-10 slider)

### Pre-trade Checklist (13 items, 4 nhom)

#### Phan tich (4 items)
| # | Item | Bat buoc |
|---|------|----------|
| 1 | Da xac dinh xu huong chinh (Daily/Weekly) | Co |
| 2 | Setup khop voi chien luoc da chon | Co |
| 3 | Khoi luong giao dich xac nhan | Khong |
| 4 | Khong co tin xau (earnings, su kien) | Khong |

#### Quan ly rui ro (4 items)
| # | Item | Bat buoc |
|---|------|----------|
| 5 | Stop-loss da duoc dat | Co |
| 6 | R:R ratio >= 2:1 | Co |
| 7 | Vi the trong gioi han position sizing | Co |
| 8 | Tong rui ro danh muc chua vuot gioi han | Khong |

#### Tam ly (3 items)
| # | Item | Bat buoc |
|---|------|----------|
| 9 | Khong dang FOMO hoac so hai | Khong |
| 10 | Chap nhan mat so tien rui ro nay | Co |
| 11 | Khong revenge trading | Khong |

#### Xac nhan (2 items)
| # | Item | Bat buoc |
|---|------|----------|
| 12 | Da ghi nhat ky giao dich | Khong |
| 13 | Da xac nhan lai gia vao/SL/TP | Co |

### GO / NO-GO Logic
- **SAN SANG GIAO DICH**: Tat ca 7 items "bat buoc" deu checked
- **CHUA DU DIEU KIEN**: Con items bat buoc chua check
- Checklist Score: % items da check / tong items (hien thi badge mau)
- Auto-check: R:R va Stop-loss tu dong check khi nhap gia hop le

### Khong can backend API - xu ly tren frontend.

---

## 4. Risk Dashboard (`/risk-dashboard`)

**Muc dich:** Tong quan suc khoe rui ro cua danh muc trong 1 trang duy nhat.

### Chuc nang

#### Risk Overview Cards (4 cards)
- Tong gia tri danh muc + so vi the
- Value at Risk 95% (muc lo toi da 1 ngay)
- Max Drawdown + Current Drawdown
- Vi the lon nhat (% danh muc) - mau do/vang/xanh

#### Risk Health Score (0-100)
Tinh diem dua tren:
| Yeu to | Diem tru | Dieu kien |
|--------|----------|-----------|
| Drawdown | -30 | Current DD > 15% |
| Drawdown | -15 | Current DD > 8% |
| Tap trung | -20 | Largest position > 30% |
| Tap trung | -10 | Largest position > 20% |
| Tuong quan | -15 | > 3 cap tuong quan cao |
| Tuong quan | -5 | 1-3 cap tuong quan cao |
| Stop-loss | -20 | Khong co SL nao active |

Hien thi: Thanh progress + 4 chi so mau (xanh/vang/do)

#### Risk Profile Compliance
- So sanh vi the lon nhat vs gioi han
- So sanh drawdown vs gioi han alert
- Progress bars voi mau xanh (OK) / do (vuot)

#### Stop-Loss Status
- So SL dang hoat dong / da kich hoat
- % gan SL nhat
- Top 5 vi the gan SL nhat

#### Canh bao tuong quan
- Danh sach cap co phieu co tuong quan > 0.7
- Canh bao rui ro tap trung

#### Strategy Scorecard
Cham diem tu dong (A-F) cho moi chien luoc active:

| Tieu chi | Diem toi da | Cong thuc |
|----------|-------------|-----------|
| Win Rate | 30 | winRate × 0.6 |
| Profit Factor | 30 | profitFactor × 15 |
| So giao dich | 20 | totalTrades × 2 |
| Avg Win/Loss | 20 | ratio × 10 |

| Grade | Diem |
|-------|------|
| A | >= 85 |
| B | >= 70 |
| C | >= 55 |
| D | >= 40 |
| F | < 40 |

### API su dung (da co san)
| API | Muc dich |
|-----|----------|
| `GET /risk/portfolio/{id}/summary` | Tong quan rui ro |
| `GET /risk/portfolio/{id}/drawdown` | Drawdown |
| `GET /risk/portfolio/{id}/correlation` | Ma tran tuong quan |
| `GET /risk/portfolio/{id}/stop-loss` | Stop-loss targets |
| `GET /risk/portfolio/{id}/profile` | Risk profile |
| `GET /strategies` | Danh sach chien luoc |
| `GET /strategies/{id}/performance` | Hieu suat chien luoc |

---

## Navigation

Cac trang moi duoc them vao header navigation:

### Nhom "Phan tich"
- Backtest (`/backtesting`)

### Nhom "Quan ly"
- Risk Dashboard (`/risk-dashboard`) - dau nhom
- Ke hoach GD (`/trade-plan`)
- Tinh vi the (`/position-sizing`)

---

## Files moi tao

### Frontend
- `frontend/src/app/features/backtesting/backtesting.component.ts`
- `frontend/src/app/features/position-sizing/position-sizing.component.ts`
- `frontend/src/app/features/trade-plan/trade-plan.component.ts`
- `frontend/src/app/features/risk-dashboard/risk-dashboard.component.ts`
- `frontend/src/app/core/services/backtest.service.ts`

### Files sua doi
- `frontend/src/app/app.routes.ts` - Them 4 routes moi
- `frontend/src/app/shared/components/header/header.component.ts` - Them nav items
- `src/InvestmentApp.Api/Controllers/BacktestsController.cs` - Fix auth scheme (JWT Bearer + "sub" claim)
