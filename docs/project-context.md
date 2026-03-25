# Project Context — Investment Mate v2

## Project Owner

- **Trường Phạm Văn** — Vietnamese investor, full-stack developer
- Focus: disciplined investing workflow (Strategy → Plan → Trade → Risk → Compound Growth)
- Communication: Vietnamese language, technical terms in English
- Values: UX workflow continuity, Vietnamese language quality (diacritics critical)

## Project Vision

Transform from "trade recorder" to "opportunity finder":
- Phase 1-6: Foundation (CRUD, analytics, market data, trade wizard)
- Phase 7+: Intelligence (AI integration, smart signals, portfolio optimization)

## Key UX Decisions (từ UX evaluation 2026-03-12)

1. **Consolidated workflow** — Merged fragmented 16 pages into natural flows (Position Sizing → Trade Plan, Advanced Analytics → Analytics)
2. **Trade Wizard** — 5-step disciplined flow: Strategy → Plan → Checklist → Confirm → Journal
3. **Dashboard Cockpit** — Single-page overview: portfolio summary, equity curve, risk alerts, market indices, watchlist
4. **AI integration** — 11 use cases, supports "copy prompt" (no API key required) + streaming (with key)
5. **Vietnamese-first** — All UI text in Vietnamese with proper diacritics (dấu), commit messages in English

## Current Improvement Plan (Round 9, score 9.4/10 → target 10/10)

### Tier 1 — Done

1. **Watchlist** — ✅ CRUD, batch live prices, VN30 import, target prices, deep link to Trade Plan
2. **Smart Trade Signals** — ✅ EMA/RSI/MACD/Volume analysis, support/resistance, signal summary
3. **Portfolio Optimizer** — ✅ Concentration alerts, sector diversification (via IFundamentalDataProvider), correlation warnings, diversification score, recommendations

### Tier 2 — Done

4. AI Prompt Enhancement — ✅ Richer context for all 12 AI use cases
5. Risk Dashboard improvements — ✅ Position-level risk (beta, sector, positionVaR), trailing stop monitoring with real-time alerts

## Common Pitfalls (từ past bugs)

- **`[contextData]="{}"` in Angular templates** — Creates new object reference every change detection cycle, causes infinite loop. Use `readonly emptyContext = {}` as stable reference.
- **`CancelAfter()` in .NET** — Only cancels the token, doesn't stop tasks that don't check it. Use `.WaitAsync(TimeSpan)` which throws `TimeoutException` independently.
- **24hmoney prices** — API returns prices in units of 1,000 VND. Must multiply by 1,000.
- **MongoDB Atlas** — Seed/connection takes ~16s on cold start. Backend launch uses `--launch-profile https` for port 5000.
- **`appsettings.json` placeholders** — .NET doesn't interpolate `{PlaceholderName}` in JSON. Must use real URLs as defaults or environment variables.
- **Money/StockSymbol equality** — `other != null` in `Equals()` triggers custom `!=` operator → `StackOverflowException`. Use `other is not null`.
