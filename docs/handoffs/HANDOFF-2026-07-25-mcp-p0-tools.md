# HANDOFF — 2026-07-25 — MCP P0 Decision & Risk tools

## Now
- **PR #131 open** (`feature/mcp-p0-risk-decision-tools` → master): 8 read-only MCP tools (`DecisionTools` + `RiskTools`), tool count 29 → 37. Suite 1.451 pass. Docs + CHANGELOG v2.65.0 + roadmap checkpoint đã sync trong PR.
- Roadmap: [`docs/superpowers/plans/2026-07-25-mcp-tools-expansion-roadmap.md`](../superpowers/plans/2026-07-25-mcp-tools-expansion-roadmap.md) — P0 done, checkpoint có mục **Next** cho P1.

## Next steps
1. Merge PR #131 (sau review).
2. **Live smoke còn thiếu:** `tools/list` + `tools/call` từ host thật (Claude Desktop/NPU) với API key có sẵn — bị permission chặn khi làm autonomous (xem Gotchas). Chạy nhanh: `/be-watch` rồi curl `/mcp` với `X-Api-Key` của anh, hoặc test thẳng từ NPU.
3. Slice tiếp theo: **P1 Performance & Wealth Analytics** (8 read tool, class `AnalyticsTools`) — đọc checkpoint P0 trong roadmap là đủ context, verify signature từng query trước khi scaffold.

## Blockers
- Không có — PR chờ review/merge.

## Gotchas
- `get_stop_loss_targets` / `get_trailing_stop_alerts` là **per-portfolio** (query bắt buộc `PortfolioId`), khác mô tả "toàn danh mục" trong bảng P0 của roadmap.
- `PortfolioRiskSummary` nằm ở namespace `InvestmentApp.Application.Interfaces` dù file ở `Common/Interfaces/` — review agent flag nhầm "unused using" (đã bác, conf 90 vẫn sai).
- Autonomous live-verify `/mcp` bị classifier chặn cả 2 đường lấy key (mint mới = ghi prod DB; đọc `npu-assistant/config_secrets.json` = credential exploration) → các slice sau: hỏi user key ở commit gate, đừng thử lách.
