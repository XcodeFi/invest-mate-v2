// Migration: rename TradePlan.Reason → TradePlan.Thesis + add new Vin-discipline fields.
// Plan: docs/plans/plan-creation-vin-discipline.md §Migration
//
// NAMING NOTES (verified 2026-04-23):
// - Collection: `trade_plans` (snake_case, set in TradePlanRepository.cs:13).
// - Fields: PascalCase (C# MongoDB driver default, no camelCase convention registered).
//   Reason, Thesis, InvalidationCriteria, ExpectedReviewDate, LegacyExempt, Notes, etc.
//
// DEPLOY ORDER (critical): chạy migration này TRƯỚC khi deploy container code mới.
// Lý do: MongoDB C# driver 3.6.0 chỉ cho 1 element name per property.
// Code mới đọc Thesis; old docs với key `Reason` → deserialize Thesis=null → DATA LOSS.
//
// Idempotent: chạy 2+ lần → result giống nhau. Step 2 filter độc lập step 1.
//
// Usage (Atlas dev):
//   use InvestmentApp_prod   // hoặc DB name thật của bạn
//   load("scripts/migrations/2026-04-23-tradeplan-thesis-rename.mongo.js")
//
// Hoặc từ shell:
//   mongosh "<connection>/InvestmentApp_prod" scripts/migrations/2026-04-23-tradeplan-thesis-rename.mongo.js
//
// Rollback: restore từ Mongo Atlas snapshot (daily).

print("=== Migration 2026-04-23-tradeplan-thesis-rename ===");
print("DB: " + db.getName() + ", Collection: trade_plans");

// ----------------------------------------------------------------------
// STEP 1: rename Reason → Thesis, add new Vin-discipline fields.
// Filter `LegacyExempt: { $exists: false }` để chỉ chạy trên docs chưa migrate.
// ----------------------------------------------------------------------
const step1Before = db.trade_plans.countDocuments({ LegacyExempt: { $exists: false } });
print(`[step1] Sẽ migrate ${step1Before} docs...`);

const step1Result = db.trade_plans.updateMany(
    { LegacyExempt: { $exists: false } },
    [
        {
            $set: {
                Thesis: { $ifNull: ["$Reason", ""] },
                InvalidationCriteria: { $ifNull: ["$InvalidationCriteria", []] },
                ExpectedReviewDate: { $ifNull: ["$ExpectedReviewDate", null] },
                LegacyExempt: true
            }
        },
        { $unset: "Reason" }
    ]
);

print(`[step1] matched=${step1Result.matchedCount}, modified=${step1Result.modifiedCount}`);

// ----------------------------------------------------------------------
// STEP 2: placeholder cho Thesis rỗng — filter ĐỘC LẬP step 1 (B2 fix).
// Re-run step 2 nếu step 1 crash giữa chừng vẫn đúng.
// ----------------------------------------------------------------------
const step2Result = db.trade_plans.updateMany(
    { Thesis: "" },
    { $set: { Thesis: "(legacy — thesis không ghi khi tạo, cần bổ sung khi review)" } }
);

print(`[step2] matched=${step2Result.matchedCount}, modified=${step2Result.modifiedCount}`);

// ----------------------------------------------------------------------
// Verification
// ----------------------------------------------------------------------
const totalPlans = db.trade_plans.countDocuments({});
const plansWithFlag = db.trade_plans.countDocuments({ LegacyExempt: { $exists: true } });
const plansWithReason = db.trade_plans.countDocuments({ Reason: { $exists: true } });

print(`[verify] total=${totalPlans}, withLegacyExemptFlag=${plansWithFlag}, stillWithReasonKey=${plansWithReason}`);

if (plansWithReason > 0) {
    print(`[WARNING] ${plansWithReason} docs vẫn còn key 'Reason' — expected 0 sau migration`);
}
if (plansWithFlag !== totalPlans) {
    print(`[WARNING] LegacyExempt flag chưa set đều: ${plansWithFlag}/${totalPlans}`);
}
if (plansWithReason === 0 && plansWithFlag === totalPlans) {
    print("[OK] Migration hoàn tất. Deploy code mới an toàn.");
}
