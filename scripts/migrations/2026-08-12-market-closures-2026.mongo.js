// Migration: seed 12 ngày nghỉ giao dịch 2026 vào collection market_closures.
// Plan: docs/superpowers/plans/2026-08-12-t2-settlement-pending-cash.md Task 5
//
// NAMING NOTES (đã đối chiếu 2026-08-12):
// - Collection: `market_closures` (snake_case, đặt trong MarketClosureRepository.cs:16).
// - Fields: PascalCase (mặc định MongoDB C# driver, không có convention camelCase nào đăng ký).
// - Date lưu nửa đêm UTC — khớp [BsonDateTimeOptions(Kind = DateTimeKind.Utc)] trên entity.
// - AggregateRoot có Id (string, driver map thành _id) và Version (int). Không field nào khác.
//
// Nguồn: thông báo lịch nghỉ giao dịch năm 2026 của HOSE — 12 phiên.
// T7 22/08/2026 là ngày làm việc bù nhưng HOSE không giao dịch — cuối tuần đã bị loại
// theo DayOfWeek trong code nên KHÔNG ghi vào đây.
//
// Idempotent: _id sinh theo (user, ngày) nên chạy lại cho ra đúng bản ghi cũ, không đẻ bản thứ hai.
//
// Usage:
//   mongosh "<connection>/<database>" --eval 'var USER_ID="<userId>"' scripts/migrations/2026-08-12-market-closures-2026.mongo.js
//
// Rollback: db.market_closures.deleteMany({ UserId: USER_ID, Date: { $gte: ISODate("2026-01-01"), $lte: ISODate("2026-12-31") } })

print("=== Migration 2026-08-12-market-closures-2026 ===");
print("DB: " + db.getName() + ", Collection: market_closures");

if (typeof USER_ID === "undefined" || !USER_ID) {
    throw new Error("Phải truyền USER_ID: mongosh ... --eval 'var USER_ID=\"...\"' <script>");
}

const CLOSURES = [
    { date: "2026-01-01", note: "Tết Dương lịch" },
    { date: "2026-02-16", note: "Tết Nguyên đán Bính Ngọ" },
    { date: "2026-02-17", note: "Tết Nguyên đán Bính Ngọ" },
    { date: "2026-02-18", note: "Tết Nguyên đán Bính Ngọ" },
    { date: "2026-02-19", note: "Tết Nguyên đán Bính Ngọ" },
    { date: "2026-02-20", note: "Tết Nguyên đán Bính Ngọ" },
    { date: "2026-04-27", note: "Giỗ Tổ Hùng Vương" },
    { date: "2026-04-30", note: "Ngày Chiến thắng" },
    { date: "2026-05-01", note: "Quốc tế Lao động" },
    { date: "2026-08-31", note: "Quốc khánh" },
    { date: "2026-09-01", note: "Quốc khánh" },
    { date: "2026-09-02", note: "Quốc khánh" }
];

let inserted = 0, existing = 0;

CLOSURES.forEach(function (item) {
    const utcMidnight = new Date(item.date + "T00:00:00.000Z");
    const result = db.market_closures.updateOne(
        { UserId: USER_ID, Date: utcMidnight },
        {
            $setOnInsert: {
                // _id tự sinh theo (user, ngày) thay vì UUID(): chạy lại cho ra đúng _id cũ,
                // nên idempotent kể cả khi unique index chưa kịp được tạo.
                _id: "mc-" + USER_ID + "-" + item.date,
                UserId: USER_ID,
                Date: utcMidnight,
                Note: item.note,
                CreatedAt: new Date(),
                Version: 0
            }
        },
        { upsert: true }
    );
    if (result.upsertedCount > 0) inserted++;
    else existing++;
});

print("[done] thêm mới " + inserted + ", đã có sẵn " + existing + ", tổng " + CLOSURES.length);

const total = db.market_closures.countDocuments({
    UserId: USER_ID,
    Date: { $gte: new Date("2026-01-01T00:00:00.000Z"), $lte: new Date("2026-12-31T00:00:00.000Z") }
});
print("[verify] ngày nghỉ 2026 của user: " + total + " (phải là 12)");
