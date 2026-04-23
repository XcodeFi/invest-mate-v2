// Migration: rename TradePlan.reason → TradePlan.thesis + add new Vin-discipline fields.
// Plan: docs/plans/plan-creation-vin-discipline.md §Migration
//
// DEPLOY ORDER (critical): chạy migration này TRƯỚC khi deploy container code mới.
// Lý do: MongoDB C# driver 3.6.0 chỉ cho 1 BsonElement per property. Code mới đọc key `thesis`;
// nếu chạy sau deploy, old docs với key `reason` sẽ deserialize thành Thesis=null → DATA LOSS.
//
// Idempotent: chạy 2+ lần → result giống nhau. Step 2 filter độc lập step 1 (B2 fix từ review).
//
// Usage:
//   mongosh "<connection-string>/<db>" scripts/migrations/2026-04-23-tradeplan-thesis-rename.mongo.js
//
// Rollback: restore từ snapshot daily Mongo Atlas.

// ----------------------------------------------------------------------
// STEP 1: rename reason → thesis, add new Vin-discipline fields.
// Filter `legacyExempt: { $exists: false }` để chỉ chạy trên docs chưa migrate.
// ----------------------------------------------------------------------
const step1Before = db.tradePlans.countDocuments({ legacyExempt: { $exists: false } });
print(`[step1] Sẽ migrate ${step1Before} docs...`);

const step1Result = db.tradePlans.updateMany(
    { legacyExempt: { $exists: false } },
    [
        {
            $set: {
                thesis: { $ifNull: ["$reason", ""] },
                invalidationCriteria: { $ifNull: ["$invalidationCriteria", []] },
                expectedReviewDate: { $ifNull: ["$expectedReviewDate", null] },
                legacyExempt: true
            }
        },
        { $unset: "reason" }
    ]
);

print(`[step1] matched=${step1Result.matchedCount}, modified=${step1Result.modifiedCount}`);

// ----------------------------------------------------------------------
// STEP 2: placeholder cho thesis rỗng — filter ĐỘC LẬP step 1 (B2 fix).
// Chạy lại step 2 nếu step 1 crash giữa chừng vẫn đúng.
// ----------------------------------------------------------------------
const step2Result = db.tradePlans.updateMany(
    { thesis: "" },
    { $set: { thesis: "(legacy — thesis không ghi khi tạo, cần bổ sung khi review)" } }
);

print(`[step2] matched=${step2Result.matchedCount}, modified=${step2Result.modifiedCount}`);

// ----------------------------------------------------------------------
// Verification: sanity check tổng số docs có legacyExempt flag.
// ----------------------------------------------------------------------
const totalPlans = db.tradePlans.countDocuments({});
const plansWithFlag = db.tradePlans.countDocuments({ legacyExempt: { $exists: true } });
const plansWithReason = db.tradePlans.countDocuments({ reason: { $exists: true } });

print(`[verify] total=${totalPlans}, withLegacyExemptFlag=${plansWithFlag}, stillWithReasonKey=${plansWithReason}`);

if (plansWithReason > 0) {
    print(`[WARNING] ${plansWithReason} docs vẫn còn key 'reason' — expected 0 sau migration`);
}
if (plansWithFlag !== totalPlans) {
    print(`[WARNING] legacyExempt flag chưa set đều: ${plansWithFlag}/${totalPlans}`);
}
