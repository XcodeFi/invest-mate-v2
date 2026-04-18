# P2 — Trade Plan Form Editability Matrix (Phương án A — Strict)

## Goal

Lấp UX gap cho Trade Plan form: hiện tại frontend cho phép edit mọi field ở mọi state, nhưng backend đã chặn update khi plan ở `Executed`/`Reviewed`. Áp dụng **Phương án A — Strict**: khi Ready = "kế hoạch đã ký", hầu hết field bị khoá trở xuống.

## Editability Matrix

| Field Group | Draft | Ready | InProgress | Executed | Reviewed | Cancelled |
|---|---|---|---|---|---|---|
| Entry Info (symbol/direction/entry/qty/strategy/portfolio/entryMode) | ✏️ | ✏️ | 🔒 | 🔒 | 🔒 | 🔒 |
| Stop-Loss | ✏️ | ✏️ | ⚠️ tighten-only | 🔒 | 🔒 | 🔒 |
| Take-Profit | ✏️ | ✏️ | 🔒 | 🔒 | 🔒 | 🔒 |
| Risk Context (market/horizon/confidence) | ✏️ | ✏️ | ✏️ | 🔒 | 🔒 | 🔒 |
| Lots (ScalingIn/DCA) | ✏️ | ✏️ | ⚠️ pending-only | 🔒 | 🔒 | 🔒 |
| Exit Targets / Scenario | ✏️ | ✏️ | 🔒 | 🔒 | 🔒 | 🔒 |
| Checklist | ✏️ | ✏️ | ✏️ | 🔒 | 🔒 | 🔒 |
| Reason, Notes, Tags | ✏️ | ✏️ | ✏️ | ✏️ | ✏️ | 🔒 |
| Campaign Review (lessons) | — | — | — | — | ✏️ only | — |

Legend: ✏️ edit · 🔒 readonly · ⚠️ conditional · — n/a

## Tighten-SL Gate (InProgress)

Khi plan ở `InProgress` và user chỉnh SL:
- **Long (Buy)**: newSl phải `>=` currentSl (chỉ được nới gần giá hơn).
- **Short (Sell)**: newSl phải `<=` currentSl.
- Vi phạm → chặn save với notification: *"SL mới lỏng hơn SL hiện tại — không được phép trong trạng thái Đang chạy."*

Lý do: tâm lý "không dời SL để tránh loss" — nguyên tắc kỷ luật cốt lõi.

## Implementation

### Component getters

```ts
get canEditEntryInfo(): boolean
get canEditStopLoss(): boolean       // full vs tighten-only marker
get canEditTakeProfit(): boolean
get canEditRiskContext(): boolean
get canEditExitTargets(): boolean
get canEditLots(): boolean            // full vs pending-only marker
get canEditChecklist(): boolean
get canEditNotes(): boolean
get stateBanner(): { tone, message } | null
```

### Helper methods

```ts
canEditLot(lot: PlanLotForm): boolean   // per-lot check (pending-only in InProgress)
validateTightenSl(newSl: number): { ok, reason? }
```

### UI

1. **Banner** ở đầu form (thay editing indicator hiện tại)
2. `[readonly]="!canEditX"` + readonly styling helper `readonlyClass()`
3. Action buttons visibility theo state
4. Save flow: khi InProgress, chỉ cho phép save nếu tighten-SL hợp lệ

## Tests

`trade-plan.component.spec.ts`:
- `describe('Editability matrix')` — 6 states × 8 groups, spot-check critical combos
- `describe('Tighten-SL gate')` — Long/Short, tighter/looser/equal
- `describe('State banner')` — each state returns correct tone + message
- `describe('Action button visibility')` — Draft/Ready/InProgress/Executed/Reviewed/Cancelled

## Non-goals

- Không sửa backend (đã có `TradePlan.Update()` chặn)
- Không refactor component (3311 lines — chỉ thêm getters + wire disabled)
- Không touch Campaign Review flow (đã hoạt động đúng)
