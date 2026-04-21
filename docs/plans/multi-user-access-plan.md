# Kế hoạch Multi-user Access & Debug Impersonation — Investment Mate v2

> Tài liệu kế hoạch — thiết kế chức năng chia sẻ tài khoản đầu tư (vợ-chồng, co-investor) + công cụ admin impersonate phục vụ debug.
> Cập nhật lần cuối: 2026-04-20 (rev 2 — vá theo review: controller naming, JournalEntry/AlertRule quyền, InviteEmail, CreatedByUserId, cache invalidation, impersonation revocation, phân pha nhỏ hơn)
> Trạng thái: **Đang thảo luận, chưa triển khai**

---

## Bối cảnh

### Pain point 1 — Chia sẻ tài khoản đầu tư

Hiện tại mỗi entity (Portfolio, TradePlan, CapitalFlow, Strategy, Watchlist, Journal…) đều gắn cứng `UserId` và lọc bằng `User.FindFirst("sub")` trong JWT. Không có cách cho:

- Vợ-chồng cùng quản lý 1 tài khoản chứng khoán (cùng nhập trade, cùng xem P&L)
- Một người đầu tư cùng (góp vốn) chỉ xem báo cáo, không thao tác
- Chia sẻ read-only cho cố vấn/kế toán

### Pain point 2 — Debug dữ liệu người dùng

Login hiện tại chỉ có Google OAuth → developer không thể login như user khác để tái hiện bug. Công cụ `/db-query` (read-only Mongo) giúp xem raw data nhưng không giúp thấy UI như user đó thấy.

---

## Các phương án đã cân nhắc

### Nhóm A — Chia sẻ tài khoản

#### Phương án A1: Portfolio-level sharing

Thêm entity `PortfolioMember { portfolioId, userId, role }` với role `Owner | Editor | Viewer`.

- **Scope:** chỉ 1 portfolio được share. Watchlist/Strategy/Routine vẫn riêng từng user.
- **Ưu:** đơn giản, khớp mô hình "1 tài khoản chứng khoán = 1 portfolio". Refactor vừa phải (~20 điểm filter `UserId` liên quan portfolio).
- **Nhược:** không share dữ liệu ngoài portfolio. Nếu sau này cần workspace đầy đủ phải mở rộng tiếp.

#### Phương án A2: Workspace-level sharing

Thêm entity `Workspace` + `WorkspaceMember`. Mọi entity gắn `WorkspaceId` thay vì `UserId`.

- **Scope:** toàn bộ dữ liệu đầu tư (portfolio, plan, watchlist, strategy, journal, routine) chung.
- **Ưu:** mô hình co-manage đầy đủ nhất. RBAC tập trung 1 chỗ.
- **Nhược:** refactor khổng lồ — mọi entity/query/index/controller đổi. Migration phức tạp (mỗi user hiện có → 1 workspace mặc định). Rủi ro cao cho production.

##### Biến thể Postman-style (deferred note — 2026-04-21)

Lấy ý tưởng workspace switcher của Postman làm mô hình tham chiếu nếu sau này nâng cấp A1 → A2:

- **Multi-workspace per user:** mỗi user có 1 Personal workspace default + tạo thêm Team workspace (gia đình, co-invest) không giới hạn. Switch qua dropdown ở top nav (giống Postman top-left).
- **Scope data:** Portfolio/Trade/CapitalFlow/Snapshot/Position/RiskProfile + TradePlan/JournalEntry/AlertRule chuyển từ `UserId` → `WorkspaceId`. Watchlist/Strategy/Routine/AiSettings **vẫn per-user** (preferences cá nhân).
- **Role per-workspace:** Owner/Editor/Viewer giống A1, nhưng apply ở scope workspace (không per-portfolio) → invite vợ vào family workspace 1 lần là thấy hết portfolio bên trong.
- **Request context:** FE cache `activeWorkspaceId` ở localStorage, HTTP interceptor tự gắn header `X-Workspace-Id` → BE filter mọi query theo đó.
- **Migration:** mỗi User → seed 1 Personal workspace + backfill `WorkspaceId` trên mọi entity. Index mới `{ userId: 1, status: 1 }` trên `WorkspaceMember`, `{ workspaceId: 1 }` trên entity tài chính.
- **Authorization:** `IWorkspaceAccessService` + attribute `[RequireWorkspaceAccess(role)]` thay cho `PortfolioAccessService`.

**Vì sao defer:** hiện app dùng cá nhân (solo user), lợi ích workspace chưa tương xứng với chi phí refactor. Khi pain point đa user xuất hiện thực sự, A1 có path nâng cấp dễ dàng: `WorkspaceId = PortfolioId` gốc, `WorkspaceMember` sinh từ `PortfolioMember` cũ. Giữ ý tưởng này như future consideration.

#### Phương án A3: Read-only share link (token-based)

Owner tạo share link chứa token → người xem mở link thấy dashboard read-only.

- **Ưu:** nhanh nhất (2-3 ngày), không đụng domain.
- **Nhược:** không phải co-manage. Không hỗ trợ vợ-chồng cùng nhập trade. Chỉ giải được bài toán "viewer".

### Nhóm B — Debug impersonation

#### Phương án B1: Admin impersonate

Thêm claim `role=admin` cho một số user. Endpoint `POST /auth/impersonate/:userId` cấp JWT mới với `sub=targetUserId` + `actor=adminId`. Audit log mọi lần impersonate.

- **Ưu:** 1-click debug, không đụng schema, dùng được cả prod (có audit).
- **Nhược:** backdoor → phải bảo vệ nghiêm (whitelist admin user IDs, MFA optional, audit bắt buộc).

#### Phương án B2: Dev-only password provider

Thêm `Provider="dev"` với email/password, chỉ bật khi `ASPNETCORE_ENVIRONMENT != Production`.

- **Ưu:** tách khỏi prod hoàn toàn, test account seed được.
- **Nhược:** không giúp khi bug xảy ra trên user prod thật (không có password của họ).

#### Phương án B3: Dùng `/db-query` skill có sẵn

Skill read-only MongoDB đã có, đủ để inspect data.

- **Ưu:** zero code.
- **Nhược:** không thấy UI như user đó, không reproduce được bug liên quan rendering/flow.

---

## Quyết định triển khai

**Chọn: Phương án A1 (Portfolio sharing) + Phương án B1 (Admin impersonate)** — hai tính năng trực giao, giải hai pain point khác nhau.

Lý do:

- **A1 đủ cho use case vợ-chồng** vì ở VN "tài khoản đầu tư" ≈ 1 tài khoản ở CTCK ≈ 1 Portfolio. Dữ liệu cá nhân hoá (Watchlist/Strategy) vẫn nên riêng.
- **B1 giải quyết triệt để debug** với chi phí thấp, có audit trail nên an toàn cho prod.
- **A2 để sau** — nếu sau 6 tháng thấy cần workspace đầy đủ, có thể nâng cấp: `WorkspaceId` = `PortfolioId` gốc, chạy migration thêm WorkspaceMember từ PortfolioMember.

---

# PHẦN 1 — Phương án A1: Portfolio Sharing

## 1.1. Mô hình dữ liệu

### Entity mới: `PortfolioMember`

```csharp
public class PortfolioMember : AggregateRoot
{
    public string PortfolioId { get; private set; }
    public string? UserId { get; private set; }       // null khi invite email chưa resolve sang user
    public string InviteEmail { get; private set; }   // email dùng để invite, luôn lowercase-trim
    public PortfolioRole Role { get; private set; }   // Owner | Editor | Viewer
    public string InvitedBy { get; private set; }     // userId của người mời
    public DateTime InvitedAt { get; private set; }
    public DateTime? AcceptedAt { get; private set; }
    public MemberStatus Status { get; private set; }  // Pending | Active | Revoked
    public bool IsDeleted { get; private set; }
}

public enum PortfolioRole { Owner, Editor, Viewer }
public enum MemberStatus { Pending, Active, Revoked }
```

- Mỗi Portfolio luôn có đúng **1 Owner** (= `Portfolio.UserId` hiện tại, seed migration).
- Một user có thể vừa là Owner portfolio này, vừa là Viewer portfolio khác.
- **Invite flow:**
  - Khi invite, luôn tạo record với `InviteEmail` (lowercase + trim) và `Status=Pending`.
  - Nếu email đã khớp user tồn tại → gán luôn `UserId` + giữ `Status=Pending` chờ accept.
  - Nếu email chưa có user → `UserId=null`. Khi user đăng ký Google lần đầu, sau khi tạo `User`, resolver chạy: tìm mọi `PortfolioMember { InviteEmail == user.Email, UserId == null }` và set `UserId`.
  - **Lưu ý email mismatch:** Google OAuth trả `email` có thể khác email invite (VD: invite `vo@gmail.com` nhưng đăng nhập Google workspace `vo@cty.com`). Trong trường hợp này, UI "Pending invites của tôi" cho phép user thấy invite gửi tới **bất kỳ email** mà user confirm sở hữu (phase 2 — cần verify email thứ 2). Phase 1: chỉ match đúng email chính của User entity.

### Index MongoDB

- `{ portfolioId: 1, userId: 1 }` unique **partial** (filter `userId: { $ne: null }`) — cho phép nhiều record Pending cùng portfolio với `userId=null`
- `{ portfolioId: 1, inviteEmail: 1 }` unique **partial** (filter `status: { $in: ["Pending","Active"] }`) — tránh invite trùng email
- `{ userId: 1, status: 1 }` — query "tất cả portfolio tôi có quyền truy cập"
- `{ inviteEmail: 1, status: 1 }` — resolver auto-link khi user đăng ký
- `{ portfolioId: 1, isDeleted: 1 }`

### Migration

- Với mỗi Portfolio hiện có → tạo `PortfolioMember { portfolioId, userId=Portfolio.UserId, inviteEmail=User.Email, role=Owner, status=Active, acceptedAt=now }`.
- Giữ nguyên `Portfolio.UserId` như **creator/owner cache** (không xoá, tránh break code cũ, rename về `CreatedByUserId` ở phase sau).
- Migration script theo pattern hiện có tại [scripts/migrations/](scripts/migrations/) — thêm file `2026-XX-XX-seed-portfolio-members.mongo.js`.

## 1.2. Quyền theo role

| Hành động                                 | Owner | Editor | Viewer |
| -------------------------------------------- | :---: | :----: | :----: |
| Xem portfolio/trades/P&L/analytics/snapshots |  ✅  |   ✅   |   ✅   |
| Thêm/sửa/xoá Trade                        |  ✅  |   ✅   |   ❌   |
| Thêm/sửa/xoá CapitalFlow                  |  ✅  |   ✅   |   ❌   |
| Thêm/sửa TradePlan (gắn portfolio này)   |  ✅  |   ✅   |   ❌   |
| Sửa `InitialCapital`, `Name` portfolio  |  ✅  |   ❌   |   ❌   |
| Mời/remove member, đổi role               |  ✅  |   ❌   |   ❌   |
| Xoá portfolio                               |  ✅  |   ❌   |   ❌   |
| Xem RiskProfile, AlertRule gắn portfolio    |  ✅  |   ✅   |   ✅   |
| Sửa RiskProfile                             |  ✅  |   ✅   |   ❌   |

**Lưu ý đặc biệt (entity có portfolioId optional):**

Các entity sau có cả `UserId` và `PortfolioId` (nullable) trong code hiện tại — quyền xử lý theo cờ `PortfolioId`:

| Entity                 | Khi `PortfolioId != null`                                                | Khi `PortfolioId == null`                        |
| ---------------------- | -------------------------------------------------------------------------- | -------------------------------------------------- |
| **TradePlan**    | Inherit quyền portfolio (Editor mới được thêm/sửa/xoá)             | Private của `UserId` — chỉ creator thấy/sửa |
| **JournalEntry** | Inherit quyền portfolio (Editor được thêm/sửa/xoá; Viewer chỉ xem) | Private của `UserId` — chỉ creator thấy/sửa |
| **AlertRule**    | Inherit quyền portfolio (Editor được CRUD; Viewer chỉ xem)            | Private của `UserId` — chỉ creator            |

**Entity KHÔNG share (luôn private per user):**

- `Strategy`, `Watchlist`, `DailyRoutine`, `AiSettings` — không có `PortfolioId`, không cần refactor filter.

**Entity gắn qua Trade:**

- **TradeJournal (nếu có) gắn Trade**: quyền kế thừa theo Portfolio của Trade đó. Implement: resolve `trade.PortfolioId` → check `accessService`.

## 1.3. Authorization layer

### Service mới: `IPortfolioAccessService`

```csharp
public interface IPortfolioAccessService
{
    Task<PortfolioRole?> GetUserRoleAsync(string portfolioId, string userId);
    Task<bool> CanViewAsync(string portfolioId, string userId);
    Task<bool> CanEditAsync(string portfolioId, string userId);
    Task<bool> CanManageAsync(string portfolioId, string userId); // Owner-only
    Task<List<string>> GetAccessiblePortfolioIdsAsync(string userId, PortfolioRole minRole = PortfolioRole.Viewer);
}
```

### Refactor pattern

Ở controllers hiện tại filter thẳng `UserId == currentUserId`. Đổi sang:

1. **Query list:** `portfolioIds = accessService.GetAccessiblePortfolioIds(userId)` rồi filter `Portfolio.Id IN portfolioIds`.
2. **Query detail/mutation:** gọi `accessService.CanView/Edit/Manage(portfolioId, userId)` trước khi handle.
3. **Entity gắn gián tiếp** (Trade/CapitalFlow/Snapshot có `portfolioId`): resolve portfolioId từ entity → check quyền.

### Authorize attribute tuỳ biến

`[RequirePortfolioAccess(role: PortfolioRole.Editor)]` đọc `portfolioId` từ route/body, gọi `accessService` trong filter pipeline → trả 403 nếu thiếu quyền. Áp ở controllers:

- `PortfoliosController`
- `TradesController`
- `CapitalFlowsController`
- `PositionsController`
- `PnLController`
- `SnapshotsController`
- `AdvancedAnalyticsController` *(tên thật trong code, không phải `AnalyticsController`)*
- `RiskController`
- `AlertsController` — chỉ khi `AlertRule.PortfolioId != null`; nếu null, fallback filter `UserId`
- `TradePlansController` — chỉ khi `TradePlan.PortfolioId != null`; nếu null, fallback filter `UserId`
- `JournalEntriesController` — chỉ khi `JournalEntry.PortfolioId != null`; nếu null, fallback filter `UserId`

## 1.4. API Endpoints mới

Controller mới: `PortfolioMembersController` → route prefix `/api/v1/portfolios/{portfolioId}/members`

| Method | Route                        | Mô tả                              | Auth   |
| ------ | ---------------------------- | ------------------------------------ | ------ |
| GET    | `/members`                 | List members của portfolio          | View   |
| POST   | `/members/invite`          | Mời user (email + role)             | Owner  |
| PUT    | `/members/{memberId}/role` | Đổi role                           | Owner  |
| DELETE | `/members/{memberId}`      | Remove member (không xoá Owner)    | Owner  |
| POST   | `/members/leave`           | User tự rời (không áp cho Owner) | Member |

Endpoint "invites của tôi":

| Method | Route                               | Mô tả                          |
| ------ | ----------------------------------- | -------------------------------- |
| GET    | `/api/v1/me/pending-invites`      | Danh sách lời mời chờ accept |
| POST   | `/api/v1/me/invites/{id}/accept`  | Accept invite                    |
| POST   | `/api/v1/me/invites/{id}/decline` | Từ chối                        |

Endpoint portfolio list mở rộng:

```
GET /api/v1/portfolios  
→ trả về { owned: [...], shared: [{ ...portfolio, myRole: "Viewer", ownerName: "..." }] }
```

## 1.5. UI (Frontend)

### Trang mới

- `/portfolios/:id/members` — quản lý thành viên: list members, form invite (email + role dropdown), action đổi role / remove.
- `/pending-invites` — danh sách lời mời, nút Accept/Decline (badge notification trên header).

### Thay đổi trang hiện tại

- **`/portfolios`** — 2 section "Của tôi" + "Được chia sẻ" (hiển thị badge role + tên owner).
- **Trên mọi trang portfolio-context** (dashboard, trades, analytics, risk…): banner "Bạn đang xem với tư cách **Viewer**" khi role != Owner. Disable/ẩn nút mutation theo role (vd: Viewer không thấy nút "Thêm giao dịch").
- **Header:** badge số invite pending.

### Service Angular

- `PortfolioMemberService` (CRUD members, invite).
- `InviteService` (list pending, accept, decline).
- Mở rộng `AuthService` lưu `myRoleByPortfolioId` cache, component check role để ẩn UI.

## 1.6. Tiêu chí hoàn thành (Definition of Done)

- [ ] Unit test `PortfolioAccessService` với 6 case: Owner, Editor, Viewer, non-member, revoked, pending
- [ ] Unit test invite flow: invite → pending (userId=null) → user login → resolver auto-link → accept → active
- [ ] Unit test role enforcement: Viewer thử POST /trades → 403
- [ ] Unit test entity với `portfolioId` optional: `TradePlan`/`JournalEntry`/`AlertRule` fallback filter `UserId` khi `PortfolioId == null`
- [ ] Unit test concurrency: tạo Portfolio mới → PortfolioMember Owner được tạo atomic
- [ ] Migration script seed `PortfolioMember` cho mọi Portfolio hiện có (bao gồm `InviteEmail = User.Email`)
- [ ] Endpoint `/portfolios/{id}/members` CRUD đầy đủ (xUnit integration test)
- [ ] Trang `/portfolios/:id/members` + `/pending-invites` hoạt động
- [ ] Banner "Viewer mode" hiển thị đúng context
- [ ] Viewer thử thao tác → bị chặn ở cả UI (ẩn nút) và API (403)
- [ ] **Phase 2 only:** `CreatedByUserId` được lưu đúng trên Trade/CapitalFlow/JournalEntry khi Editor tạo record; backfill migration chạy đúng cho data cũ
- [ ] Update `docs/business-domain.md` (thêm entity PortfolioMember, quyền theo role, `CreatedByUserId`)
- [ ] Update `docs/architecture.md` (controller + service mới, attribute `[RequirePortfolioAccess]`)
- [ ] Update `docs/features.md`
- [ ] Update `frontend/src/assets/CHANGELOG.md`

## 1.7. Phân pha triển khai

> Plan ban đầu gộp "1 tuần" cho toàn bộ refactor 9+ controllers là quá optimistic. Tách nhỏ để rollout an toàn hơn, mỗi sub-phase đều ship được độc lập.

**Phase 1A (2-3 ngày) — Foundation:**

- Entity `PortfolioMember` + enums + repository + MongoDB index.
- Migration script seed Owner cho mọi Portfolio hiện có.
- `IPortfolioAccessService` + unit tests đầy đủ.
- Chưa đụng controller nào — code mới chưa active, rollback dễ.

**Phase 1B (2-3 ngày) — Viewer-only MVP (3 controllers):**

- Refactor `PortfoliosController` (list trả `owned` + `shared`).
- Refactor `TradesController` + `PositionsController` (đủ cho vợ-chồng xem dashboard + trades của nhau).
- API members: `GET/POST invite/DELETE` — chỉ role `Owner` và `Viewer`.
- Invite bằng email text trong UI (không gửi mail thật).
- UI tối thiểu: trang `/portfolios/:id/members` + banner "Viewer mode".

**Phase 1C (2-3 ngày) — Rollout Viewer tới controllers còn lại:**

- `CapitalFlowsController`, `PnLController`, `SnapshotsController`, `AdvancedAnalyticsController`, `RiskController`.
- `AlertsController`, `TradePlansController`, `JournalEntriesController` với logic fallback khi `portfolioId == null`.
- Trang `/pending-invites` + badge notification header.

**Phase 2 (1 tuần) — Editor role:**

- **Bắt buộc trước khi bật Editor:** thêm `CreatedByUserId` vào `Trade`, `CapitalFlow`, `JournalEntry` (audit "ai đã tạo/sửa"). Không có field này, co-management mất trace — không thể phân biệt vợ hay chồng tạo trade.
  - Migration: backfill `CreatedByUserId` = `Portfolio.UserId` (Owner) cho record cũ.
  - Controller: khi create, set `CreatedByUserId = currentUserId`.
- Thêm role `Editor` vào enum + access service.
- Attribute `[RequirePortfolioAccess(role)]` áp lên mutation endpoints.
- Role switcher UI, form mời với role dropdown.
- Resolver auto-link email invite khi user đăng ký Google lần đầu.

**Phase 3 (optional):**

- Gửi email invite qua SMTP/SendGrid.
- Notification in-app khi invite/accept/role-change (realtime qua SignalR nếu có, hoặc polling mỗi 30s).
- Audit log mọi mutation của Editor (tái sử dụng audit infrastructure của impersonation nếu được).

---

# PHẦN 2 — Phương án B1: Admin Impersonate

## 2.1. Mô hình dữ liệu

### Mở rộng `User` entity

```csharp
public class User : AggregateRoot
{
    // ...existing fields
    public UserRole Role { get; private set; } = UserRole.User;
}

public enum UserRole { User, Admin }
```

- Mặc định tất cả user hiện có = `User`.
- **Bootstrap admin đầu tiên:** seed qua env var `ADMIN_EMAILS` (comma-separated) khi app khởi động → auto set `Role=Admin` cho các email này. Chỉ chạy idempotent ở startup (không override nếu user đã có role khác).
- **Sau bootstrap:** dùng `POST /admin/users/{id}/promote` để grant thêm admin, không cần sửa env và restart.

### Entity mới: `ImpersonationAudit`

```csharp
// Append-only audit log — KHÔNG phải AggregateRoot (không có business rule, không raise event).
// Dùng base class nhẹ (Entity) hoặc pattern AuditEntry nếu codebase đã có.
public class ImpersonationAudit : Entity
{
    public string AdminUserId { get; private set; }
    public string TargetUserId { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public string Reason { get; private set; }   // bắt buộc, lý do debug
    public string IpAddress { get; private set; }
    public string UserAgent { get; private set; }
    public bool IsRevoked { get; private set; }  // true khi admin logout / manually stop → từ chối token này
}
```

- Index: `{ adminUserId: 1, startedAt: -1 }`, `{ targetUserId: 1, startedAt: -1 }`, `{ _id: 1, isRevoked: 1 }` (lookup nhanh khi validate token).

## 2.2. JWT design

Token impersonate chứa:

```json
{
  "sub": "<targetUserId>",        // backend xử lý như user đó
  "actor": "<adminUserId>",       // admin thực sự đang login
  "impersonation_id": "<auditId>",
  "exp": "<1 hour>",              // TTL ngắn, không refresh
  "amr": ["impersonate"]          // claim đánh dấu
}
```

- Middleware đọc `actor` → nếu có:
  1. **Validate `impersonation_id`** bằng lookup `ImpersonationAudit` → nếu `IsRevoked=true` hoặc `EndedAt != null`, trả 401 kèm `X-Impersonation-Revoked: true` để frontend tự động thoát impersonate.
  2. Log mọi request (method + path + body hash) vào audit log gắn `ImpersonationId`.
- Header response `X-Impersonating: true` + `X-Impersonation-Target: <email>` để frontend biết hiển thị banner.

**Revocation strategy (quan trọng vì JWT stateless):**

- Không cần blacklist store riêng — dùng chính `ImpersonationAudit.IsRevoked` làm "session store" (1 bản ghi Mongo = 1 session).
- Trade-off: mỗi request khi impersonate phải query Mongo 1 lần → thêm ~2-5ms. Chấp nhận được vì scope chỉ admin dùng, số lượng request thấp.
- Alternative nếu sau này scale to many admins: cache `impersonation_id → IsRevoked` trong `IMemoryCache` TTL 30s. Revoke qua pub-sub để invalidate cache các instance khác.
- Khi admin gọi `/auth/log`
- 
- `out` (nếu có): tìm mọi `ImpersonationAudit { AdminUserId == currentAdminId, EndedAt == null }` → set `IsRevoked=true`.

## 2.3. API Endpoints

| Method | Route                                 | Mô tả                                                                            | Auth                    |
| ------ | ------------------------------------- | ---------------------------------------------------------------------------------- | ----------------------- |
| GET    | `/api/v1/admin/users`               | Search user theo email/name (paginated)                                            | Admin                   |
| POST   | `/api/v1/admin/users/{id}/promote`  | Grant `Role=Admin` cho user khác (self-bootstrapping khỏi phụ thuộc env var) | Admin                   |
| POST   | `/api/v1/admin/users/{id}/demote`   | Revoke admin role (không cho demote chính mình nếu là admin duy nhất)        | Admin                   |
| POST   | `/api/v1/admin/impersonate`         | Body `{ targetUserId, reason }` → trả JWT impersonate                          | Admin                   |
| POST   | `/api/v1/admin/impersonate/stop`    | Kết thúc impersonate session (set `EndedAt` + `IsRevoked=true`)              | Admin đang impersonate |
| GET    | `/api/v1/admin/impersonate/history` | Lịch sử impersonate (filter theo admin/target/khoảng thời gian)                | Admin                   |

**Bảo mật nghiêm ngặt:**

- Middleware `[RequireRole(Admin)]` check `role` claim từ JWT gốc (không phải từ token impersonate → tránh impersonate lồng nhau).
- Token impersonate KHÔNG được dùng để gọi `/admin/impersonate` lần nữa (check `amr` claim).
- Mutation endpoints (POST/PUT/DELETE) khi đang impersonate: **log warning** + header `X-Impersonation-Mutation: true`. Optional config `ALLOW_IMPERSONATE_MUTATIONS=false` (default) → trả 403 khi mutation + impersonate (chỉ cho xem read-only khi debug).

## 2.4. UI (Frontend)

### Trang mới `/admin/users` (chỉ admin thấy link)

- Search user theo email.
- Mỗi row có nút "Xem như user này" → modal nhập `reason` → gọi API → lưu JWT mới vào localStorage (giữ JWT admin gốc ở key riêng `admin_token`) → reload page.

### Banner toàn cục khi đang impersonate

- Thanh cảnh báo màu đỏ trên cùng: "⚠️ Bạn đang xem với tư cách **{targetEmail}**. Mọi thao tác sẽ được ghi log. [Thoát impersonate]"
- Nút "Thoát" khôi phục JWT admin gốc từ localStorage.

### Route guard

- `AdminGuard` check claim `role=admin` → bảo vệ `/admin/*`.

## 2.5. Tiêu chí hoàn thành (Definition of Done)

- [ ] Unit test `ImpersonationService.CreateToken` (sinh JWT đúng format, TTL đúng)
- [ ] Integration test: admin gọi `/admin/impersonate` → nhận token → gọi `/portfolios` thấy data của target
- [ ] Integration test: non-admin gọi `/admin/impersonate` → 403
- [ ] Integration test: token impersonate gọi `/admin/impersonate` lần 2 → 403
- [ ] **Integration test revocation:** admin stop impersonate → token cũ gọi API → 401 + header `X-Impersonation-Revoked: true`
- [ ] **Integration test self-bootstrap:** admin promote user khác → user đó có `Role=Admin` persisted
- [ ] **Integration test demote:** không cho demote admin duy nhất (tránh lock-out)
- [ ] Middleware log đúng `ImpersonationId` vào `AuditEntry` cho mọi request
- [ ] Bootstrap admin từ env `ADMIN_EMAILS` hoạt động idempotent (restart app không override)
- [ ] Banner đỏ hiển thị đúng, nút "Thoát" khôi phục session admin gốc
- [ ] `/admin/impersonate/history` trả lịch sử đầy đủ
- [ ] Mutation guard: `ALLOW_IMPERSONATE_MUTATIONS=false` chặn POST/PUT/DELETE
- [ ] Update `docs/architecture.md` (thêm admin controller, impersonation flow, revocation pattern)
- [ ] Update `CLAUDE.md` mục bảo mật (cảnh báo không share JWT admin)

## 2.6. Phân pha triển khai

**Phase 1 (MVP — 3 ngày):** Role enum + env seed + endpoint impersonate/stop + banner đỏ. Mutation **bị chặn cứng** khi impersonate. Audit log cơ bản. *Chi tiết implementation: xem §2.7.*

**Phase 2 (2 ngày):** Trang `/admin/users` search + history. Config cho phép/cấm mutation. Self-bootstrap endpoints (`/admin/users/{id}/promote|demote`).

**Phase 3 (optional):** MFA cho admin login. Slack notification khi có impersonate session mới. Expire token ngay khi admin logout. Dry-run mutation mode.

## 2.7. Phase 1 Implementation Plan (Ship-ready, snapshot sau analyze)

> Ghi chú từ Phase 1 ship workflow (2026-04-20). Context cụ thể từ codebase hiện tại.
> Trạng thái: **✅ Implemented 2026-04-21** — xem `frontend/src/assets/CHANGELOG.md` mục v2.45.0. 926 tests green. Manual verification scenarios §2.7.5 còn đang chờ smoke-test trên môi trường dev.

### 2.7.1. Scope giới hạn (đúng MVP)

**IN scope:**

- `UserRole { User, Admin }` enum + `User.Role` field + `PromoteToAdmin()` / `DemoteToUser()`
- `ImpersonationAudit` entity (simple DTO, không `AggregateRoot`)
- Bootstrap admin từ config section `Admin:AllowEmails` (idempotent, không override existing role)
- `POST /api/v1/admin/impersonate` + `POST /api/v1/admin/impersonate/stop`
- JWT impersonate: claims `sub, actor, impersonation_id, amr=["impersonate"]`, TTL 1h
- Middleware: validate `IsRevoked` → 401 + `X-Impersonation-Revoked: true`
- Block mutation (POST/PUT/DELETE/PATCH) khi `ALLOW_IMPERSONATE_MUTATIONS=false` → 403
- Response header `X-Impersonating: true`
- Frontend: red banner + nút "Thoát" + HTTP interceptor auto-restore admin token khi 401 + revoked

**OUT of scope (để Phase 2):**

- `GET /admin/users` search
- `POST /admin/users/{id}/promote` / `demote`
- `GET /admin/impersonate/history`
- MFA, Slack notify
- Admin user search UI → Phase 1 admin phải lấy `targetUserId` qua `/db-query` skill hoặc Mongo trực tiếp

### 2.7.2. File changes (checklist ship)

**Domain layer:**

- `src/InvestmentApp.Domain/Entities/UserRole.cs` — **NEW** enum `{ User, Admin }`
- `src/InvestmentApp.Domain/UserEntity.cs` — **MOD** thêm `Role` (default `User`) + method `PromoteToAdmin()` / `DemoteToUser()`
- `src/InvestmentApp.Domain/Entities/ImpersonationAudit.cs` — **NEW** simple DTO theo pattern `AuditEntry`. Fields: `Id, AdminUserId, TargetUserId, StartedAt, EndedAt?, Reason, IpAddress, UserAgent, IsRevoked`. Method `Revoke()` set `IsRevoked=true, EndedAt=UtcNow`.

**Application layer:**

- `src/InvestmentApp.Application/RepositoryInterfaces.cs` — **MOD** thêm `IImpersonationAuditRepository : IRepository<ImpersonationAudit>` với method `GetActiveByAdminAsync(adminId)`, `GetByIdAsync(id)`
- `src/InvestmentApp.Application/Admin/Commands/StartImpersonationCommand.cs` + Handler — **NEW**. Input: `adminUserId, targetUserId, reason, ipAddress, userAgent`. Output: `{ token, impersonationId, targetEmail, expiresAt }`. Logic: verify admin role, verify target exists, verify caller không đang impersonate (qua `amr` claim check ở controller trước khi gọi handler), tạo audit, gọi `JwtService.CreateImpersonationToken`.
- `src/InvestmentApp.Application/Admin/Commands/StopImpersonationCommand.cs` + Handler — **NEW**. Input: `impersonationId, adminUserId`. Logic: verify audit thuộc về admin gốc, gọi `audit.Revoke()`, save.
- `src/InvestmentApp.Application/Common/Interfaces/IJwtService.cs` *(nếu chưa có)* — thêm method `CreateImpersonationToken(string adminId, User target, string impersonationId)`

**Infrastructure layer:**

- `src/InvestmentApp.Infrastructure/Repositories/ImpersonationAuditRepository.cs` — **NEW** MongoDB collection `impersonationAudits`, indexes: `{ adminUserId: 1, startedAt: -1 }`, `{ _id: 1, isRevoked: 1 }`
- `src/InvestmentApp.Infrastructure/Services/JwtService.cs` — **MOD**:
  1. Login token gốc: thêm claim `role` từ `user.Role.ToString()`
  2. Method mới `CreateImpersonationToken()` — claims `sub=target.Id, actor=adminId, impersonation_id, email=target.Email, name=target.Name, amr=impersonate`, TTL 1h cố định (không đọc từ config)
- `src/InvestmentApp.Infrastructure/Services/AdminBootstrapHostedService.cs` — **NEW** `IHostedService`: đọc `Admin:AllowEmails`, với mỗi email tìm user, nếu `Role != Admin` → promote + save. Try/catch log warning (tránh fail startup khi Mongo cold).

**Api layer:**

- `src/InvestmentApp.Api/Authorization/RequireAdminAttribute.cs` — **NEW** `AuthorizeAttribute` wrapper check claim `role=Admin` AND NOT có claim `amr=impersonate` (tránh impersonate lồng).
- `src/InvestmentApp.Api/Controllers/AdminController.cs` — **NEW**. Route `/api/v1/admin`. `[Authorize, RequireAdmin]`. Endpoints:
  - `POST impersonate` — body `ImpersonateRequest { targetUserId, reason }`
  - `POST impersonate/stop` — lấy `impersonation_id` từ claim (chỉ đang impersonate mới dùng được — cần relax `RequireAdmin` ở endpoint này hoặc đổi sang `[Authorize]` + check actor claim)
- `src/InvestmentApp.Api/Middleware/ImpersonationValidationMiddleware.cs` — **NEW**. Chạy sau `UseAuthentication`:
  1. Check claim `impersonation_id` → nếu có: repo `GetByIdAsync`, `IsRevoked || EndedAt != null` → return 401 + header `X-Impersonation-Revoked: true`
  2. Set response header `X-Impersonating: true`
  3. Nếu method ∈ {POST, PUT, DELETE, PATCH} AND `!config["Admin:AllowImpersonateMutations"]` → return 403 JSON `{ error: "MUTATION_BLOCKED_DURING_IMPERSONATION" }`
- `src/InvestmentApp.Api/Program.cs` — **MOD**:
  - DI: `AddScoped<IImpersonationAuditRepository, ImpersonationAuditRepository>()`, MediatR auto-pick handlers (nếu đã config), `AddHostedService<AdminBootstrapHostedService>()`
  - Bind config section `Admin` (optional — đọc trực tiếp `IConfiguration["Admin:AllowEmails"]`)
  - Middleware pipeline: `app.UseAuthentication(); app.UseMiddleware<ImpersonationValidationMiddleware>(); app.UseAuthorization();`

**Config (`src/InvestmentApp.Api/appsettings.json`):**

```json
"Admin": {
  "AllowEmails": [],
  "AllowImpersonateMutations": false
}
```

Env var override: `Admin__AllowEmails__0=admin@example.com`, `Admin__AllowImpersonateMutations=true`.

**Frontend (`frontend/src/app/`):**

- `core/services/impersonation.service.ts` — **NEW**
  - `startImpersonate(targetUserId, reason)` → POST API, lưu current `token` vào `localStorage['admin_token']`, set mới, navigate `/dashboard` + reload
  - `stopImpersonate()` → POST API (best-effort, không block), restore `admin_token` → `token`, xoá `admin_token`, navigate `/dashboard` + reload
  - `isImpersonating()` bool — check có `admin_token` không
  - `getTargetInfo()` → decode JWT payload `{ email, name }`
- `core/interceptors/impersonation-revoked.interceptor.ts` — **NEW** catch 401 + header `X-Impersonation-Revoked` → gọi `impersonationService.stopImpersonate(skipApiCall=true)` + toast "Phiên impersonate đã hết hạn"
- `shared/components/impersonation-banner/impersonation-banner.component.ts` — **NEW** standalone, inline template. Red bar full-width sticky top. Text: `⚠️ Bạn đang xem với tư cách {targetEmail}. Mọi thao tác POST/PUT/DELETE sẽ bị chặn. [Thoát impersonate]`
- `app.component.ts` — **MOD** mount banner ở đầu layout, *ngIf `impersonationService.isImpersonating()`
- `app.config.ts` — **MOD** register interceptor

### 2.7.3. Tests (TDD order)

| #  | Layer          | Test                                                                                     | File                                                                         |
| -- | -------------- | ---------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------- |
| 1  | Domain         | `User.Role` default = `User` khi tạo mới                                           | `tests/InvestmentApp.Domain.Tests/UserRoleTests.cs`                        |
| 2  | Domain         | `User.PromoteToAdmin()` đổi Role sang Admin                                          | cùng file                                                                   |
| 3  | Domain         | `User.DemoteToUser()` đổi Role về User                                              | cùng file                                                                   |
| 4  | Domain         | `ImpersonationAudit` ctor set `StartedAt=UtcNow`, `IsRevoked=false`                | `tests/InvestmentApp.Domain.Tests/ImpersonationAuditTests.cs`              |
| 5  | Domain         | `ImpersonationAudit.Revoke()` set `IsRevoked=true, EndedAt`                          | cùng file                                                                   |
| 6  | Application    | `StartImpersonationCommand` — non-admin → `UnauthorizedAccessException`            | `tests/InvestmentApp.Application.Tests/Admin/StartImpersonationTests.cs`   |
| 7  | Application    | `StartImpersonationCommand` — target not found → throw                               | cùng file                                                                   |
| 8  | Application    | `StartImpersonationCommand` — admin OK → tạo audit + trả token                     | cùng file                                                                   |
| 9  | Application    | `StopImpersonationCommand` — set IsRevoked                                            | `tests/InvestmentApp.Application.Tests/Admin/StopImpersonationTests.cs`    |
| 10 | Application    | `StopImpersonationCommand` — admin khác stop → throw (chỉ admin gốc stop được) | cùng file                                                                   |
| 11 | Infrastructure | `JwtService.CreateImpersonationToken` — chứa đủ 5 claims, TTL ~1h                  | `tests/InvestmentApp.Infrastructure.Tests/JwtServiceImpersonationTests.cs` |
| 12 | Infrastructure | `JwtService.CreateToken` (login gốc) — include `role` claim                        | cùng file                                                                   |

Frontend tests: skip (project hiện `.spec.ts` pending — theo architecture.md).

### 2.7.4. Risks (Phase 1 cụ thể)

1. **`amr` claim chống lồng impersonate** — xử lý ở 2 tầng: `RequireAdminAttribute` reject token có `amr=impersonate`, middleware cũng reject call vào `/admin/impersonate` (endpoint-level check trong handler).
2. **Bootstrap race với Mongo cold start** (~16s theo [project-context.md:85](docs/project-context.md#L85)) — `IHostedService` try/catch, log warning, không throw. App vẫn start được, admin tự promote qua DB hoặc restart sau.
3. **Middleware order Program.cs** — phải đặt `ImpersonationValidationMiddleware` SAU `UseAuthentication` (cần claims) nhưng TRƯỚC `UseAuthorization` (để skip 403 check khi đã block 401).
4. **Mutation block quá rộng** — Phase 1 không whitelist endpoint nào. Admin lỡ bấm POST → 403 rõ ràng, xem banner. Chấp nhận trade-off để MVP đơn giản.
5. **Localstorage key conflict** — dùng tên `admin_token` cố định, không dùng JWT claim để đặt tên (tránh xung đột nếu tương lai có multi-admin). Nếu đã có `admin_token` thì không ghi đè (nghĩa là đang impersonate + cố gắng impersonate lồng — chặn ở service level).

### 2.7.5. Manual verification scenarios (Phase 4 ship)

| #  | Scenario                                                 | Expected                                                                |
| -- | -------------------------------------------------------- | ----------------------------------------------------------------------- |
| 1  | Khởi động app với `Admin__AllowEmails__0=<email>`  | User đó có `Role=Admin` sau startup                                |
| 2  | Non-admin gọi `POST /admin/impersonate`               | 403                                                                     |
| 3  | Admin gọi `POST /admin/impersonate` body valid        | 200 + token JWT                                                         |
| 4  | Dùng token impersonate gọi `GET /portfolios`         | Thấy data của target user                                             |
| 5  | Dùng token impersonate gọi `POST /trades`            | 403 + body `MUTATION_BLOCKED_DURING_IMPERSONATION`                    |
| 6  | Dùng token impersonate gọi `POST /admin/impersonate` | 403 (chống lồng)                                                      |
| 7  | Admin gọi `POST /admin/impersonate/stop`              | 200, audit record `IsRevoked=true, EndedAt` set                       |
| 8  | Dùng token sau khi stop → gọi API bất kỳ            | 401 + header `X-Impersonation-Revoked: true`                          |
| 9  | Frontend: banner đỏ hiển thị sau khi impersonate     | Banner + target email đúng                                            |
| 10 | Frontend: bấm "Thoát impersonate"                      | Restore admin token, banner biến mất                                  |
| 11 | Frontend: khi backend trả 401 + revoked header          | Interceptor auto-restore, toast                                         |
| 12 | Restart app 2 lần với cùng `ADMIN_EMAILS`           | Role không bị duplicate/flip, user existing Admin không bị override |

---

## Thứ tự triển khai đề xuất

1. **B1 Phase 1 (3 ngày)** — làm trước vì đơn giản, không đụng nhiều code, giải quyết ngay pain point debug.
2. **A1 Phase 1A (2-3 ngày)** — Foundation: entity + access service + migration. Chưa đụng controller.
3. **A1 Phase 1B (2-3 ngày)** — Viewer-only MVP cho 3 controller chính (Portfolios/Trades/Positions). Ship được cho vợ-chồng dùng cơ bản.
4. **A1 Phase 1C (2-3 ngày)** — Rollout Viewer tới các controller còn lại.
5. **A1 Phase 2 (1 tuần)** — `CreatedByUserId` audit + Editor role + UI đầy đủ.
6. **B1 Phase 2 + A1 Phase 3** — các phần polish, làm song song hoặc theo priority thực tế.

**Tổng timeline ước tính:** ~3 tuần end-to-end, nhưng mỗi sub-phase đều ship được độc lập → có thể pause giữa chừng.

## Rủi ro & Lưu ý

- **Index performance:** sau khi thêm `PortfolioMember` lookup, nhiều query list phải JOIN.

  - **KHÔNG cache `accessiblePortfolioIds` với TTL cố định** — với dữ liệu tài chính, nếu Owner revoke member mà cache chưa expire, member vẫn thấy data → không acceptable.
  - Thay vào đó: dựa hoàn toàn vào index `{ userId: 1, status: 1 }` trên `PortfolioMember` — query rất rẻ (index-covered). Benchmark thực tế trước khi tối ưu sớm.
  - Nếu sau khi đo đạc thấy bottleneck → invalidate cache theo **domain event** (`PortfolioMemberChanged` raise khi invite/accept/role-change/revoke) thay vì TTL. Pattern: cache store + invalidation event.
- **Soft delete inheritance:** khi Portfolio bị `MarkAsDeleted` → mọi `PortfolioMember` của nó cũng ẩn khỏi query list của member (không xoá bản ghi). Restore portfolio → members tự active lại.
- **Concurrency khi tạo Portfolio mới:** `CreatePortfolioCommand` phải atomic tạo cả `Portfolio` và `PortfolioMember { role=Owner }` trong cùng transaction (MongoDB multi-doc transaction) hoặc qua domain event handler đồng bộ. Tránh trạng thái "Portfolio tồn tại nhưng không có Owner member" → user mất quyền truy cập portfolio của chính mình.
- **Impersonation leak:** nếu admin mở browser tab khác khi đang impersonate → tab kia vẫn dùng JWT impersonate. Mitigation: banner đỏ bắt buộc, token TTL 1 giờ, `ImpersonationAudit.IsRevoked` check trên mỗi request (xem §2.2).
- **Impersonation + mutation:** mặc định `ALLOW_IMPERSONATE_MUTATIONS=false` an toàn nhưng hạn chế bug tái hiện được. Phase 3 cân nhắc "dry-run mode" — wrap mutation trong transaction rồi rollback cuối request, admin thấy được effect nhưng không commit.
- **Email mismatch khi invite:** Google OAuth email ≠ invite email → Phase 1 chấp nhận giới hạn (user phải đăng nhập đúng email được invite). Phase 2+ hỗ trợ user verify thêm email thứ 2.
- **Backward compat:** giữ `Portfolio.UserId` tới khi toàn bộ codebase dùng `PortfolioAccessService` → mới rename thành `CreatedByUserId`. KHÔNG rename ở Phase 1 — rủi ro break code cũ.
