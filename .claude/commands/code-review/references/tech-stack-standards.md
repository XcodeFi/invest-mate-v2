# Tech Stack Review Standards

Project stack: **Angular 19** (frontend) + **.NET 9** (backend) + **MongoDB** (database).
These standards are embedded in agent prompts. Each section is organized by review concern.

---

## Angular 19 — Frontend Standards

### Bug Patterns
- **Control flow**: Must use `@if`/`@for`/`@switch` (new control flow), NOT `*ngIf`/`*ngFor`/`*ngSwitch` (deprecated structural directives)
- **`@for` requires `track`**: Every `@for` loop must have a `track` expression (e.g., `track item.id`). Missing `track` is a compile error in Angular 19
- **Signals**: If using signals, mutations must go through `.set()` / `.update()` / `.mutate()` — never assign directly
- **Standalone components**: All new components must be `standalone: true` (default in Angular 19). No `NgModule` declarations for new code
- **Subscription leaks**: Subscriptions must be cleaned up via `takeUntilDestroyed()` (from `@angular/core/rxjs-interop`), `async` pipe, or explicit `unsubscribe` in `ngOnDestroy`. Using `DestroyRef` + `takeUntilDestroyed()` is preferred
- **Template-driven forms**: Project uses `ngModel` (not reactive forms). New forms must follow this pattern
- **Inline templates**: Components must use `template: \`...\`` in `@Component`, NOT separate `.html` files
- **Vietnamese text**: All UI text (labels, buttons, placeholders, messages, errors, tooltips) must be Vietnamese with full diacritics. No unaccented text (e.g., "Sap xep" is wrong, "Sap xep" must be "Sap xep")
- **Symbol inputs**: Must use `appUppercase` directive, NOT CSS `uppercase` or inline `toUpperCase()`
- **Tailwind CSS**: Use Tailwind utility classes. No custom CSS files unless absolutely necessary

### Security Patterns
- **`bypassSecurityTrustHtml`**: Flag any use — must justify why Angular's built-in sanitization is insufficient
- **`innerHTML` binding**: Must use `[innerHTML]` with Angular's DomSanitizer, never raw `innerHTML` assignment via DOM API
- **Sensitive data in templates**: No API keys, tokens, or secrets in template bindings or component properties that render to DOM
- **HttpClient interceptors**: Auth tokens must be added via interceptor, not manually per request
- **Route guards**: Protected routes must have `canActivate` / `canMatch` guards

### Performance Patterns
- **Change detection**: New components should use `changeDetection: ChangeDetectionStrategy.OnPush` unless there is a specific reason not to
- **Template computation**: No heavy computation or function calls in templates — use pipes, signals, or computed properties
- **Bundle size**: Avoid importing entire libraries (e.g., `import * as lodash`). Use tree-shakable imports
- **Lazy loading**: Feature routes should use lazy loading (`loadComponent` / `loadChildren`)
- **Observable subscriptions in loops**: Never subscribe inside `@for` / loops — restructure with `async` pipe or pre-compute
- **`trackBy` equivalent**: In `@for`, the `track` expression must use a stable identifier (`.id`), not index or object reference

---

## .NET 9 — Backend Standards

### Bug Patterns
- **Async/await correctness**: Every `async` method must `await` its async calls. Flag missing `await` on Task-returning methods
- **CancellationToken**: All handler methods (`Handle` in commands/queries) must accept and propagate `CancellationToken`. Flag handlers missing this parameter
- **Null safety**: Repository methods returning single entities (`FindOneAsync`, `GetByIdAsync`) may return `null` — callers must handle this (throw or return appropriate error)
- **CQRS violations**: Commands must not return domain data (only success/failure/id). Queries must not modify state
- **Clean Architecture violations**: Domain must not reference Application/Infrastructure/Api. Application must not reference Infrastructure/Api. Infrastructure must not reference Api. Only Api references all layers via DI
- **Entity validation**: Domain entities should validate their invariants in constructors/factory methods. Flag entities that accept invalid state
- **Value object equality**: Value objects must override `Equals`/`GetHashCode`. Flag value objects without these
- **Missing `.Trim()` / `.ToUpper()`**: Symbol-related code must normalize with `.ToUpper().Trim()`

### Security Patterns
- **`[Authorize]` attribute**: All new controller endpoints must have `[Authorize]` unless explicitly public. Flag missing auth
- **Ownership validation**: Endpoints accessing user data must filter by userId/ownership. Flag IDOR vulnerabilities (direct object access without ownership check)
- **Input validation**: Controller action parameters must be validated (FluentValidation or DataAnnotations). Flag endpoints accepting unvalidated user input
- **Raw string in queries**: NEVER concatenate user input into MongoDB filter strings. Always use `FilterDefinitionBuilder<T>`. Flag any string interpolation/concatenation in filter construction
- **Logging sensitive data**: Never log passwords, tokens, connection strings, or PII. Flag `_logger.Log*()` calls that include sensitive parameters
- **Error exposure**: API responses must not leak stack traces or internal details. Use problem details / structured error responses

### Performance Patterns
- **Sync-over-async**: Flag `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` — these cause thread pool starvation
- **CancellationToken propagation**: Every async method in the chain must pass `CancellationToken` to the next async call and to MongoDB driver methods
- **Missing `using`/`IDisposable`**: Disposable resources (HTTP clients, streams) must be disposed via `using` statement
- **StringBuilder**: String concatenation in loops must use `StringBuilder`
- **Large allocations in loops**: Flag `new List<T>()`, `new Dictionary<>()`, or `.ToList()` inside loops when the collection is recreated each iteration

---

## MongoDB — Database Standards

### Bug Patterns
- **ObjectId comparison**: Never compare `ObjectId` as strings. Use `ObjectId.Parse()` or typed comparison
- **Missing null check after find**: `FindOneAsync` / `Find().FirstOrDefaultAsync()` can return null — caller must handle
- **Replace vs Update**: Use `UpdateOneAsync` with `UpdateDefinition<T>` for partial updates. `ReplaceOneAsync` replaces the entire document — only use when intentional
- **Incorrect filter**: Verify filter fields match actual document schema (field names are case-sensitive in MongoDB)
- **Missing `Builders<T>.Filter`**: Always use typed `FilterDefinitionBuilder<T>` — not raw BsonDocument filters for type safety
- **Upsert without unique index**: `ReplaceOneAsync` with `IsUpsert = true` without a unique index can create duplicates under concurrent writes

### Security Patterns
- **NoSQL injection**: NEVER build filters via string concatenation or interpolation with user input. Always use `Builders<T>.Filter.Eq()` and similar typed builders
- **Field-level access**: Sensitive fields (passwords, tokens) must not be returned in query projections unless explicitly needed
- **Audit fields**: Entities with `CreatedAt`/`UpdatedAt` must set these server-side, never from client input

### Performance Patterns
- **N+1 queries**: Flag loops that call `FindOneAsync` / `Find` individually per item. Use `Builders<T>.Filter.In()` for batch lookups
- **Missing projection**: Queries fetching documents for display should use `.Project()` to select only needed fields, not load entire documents
- **Unbounded queries**: All list/search queries must have `.Limit()` or pagination. Flag queries returning all documents
- **Missing indexes**: New query patterns (new filter fields) should have corresponding indexes. Flag new `Filter.Eq("newField", ...)` without mentioning index
- **Large document updates**: Use `$set` / `UpdateDefinition<T>` for partial updates instead of replacing entire large documents
- **Aggregation vs LINQ**: For complex queries (group, join, unwind), prefer MongoDB aggregation pipeline over client-side LINQ processing
- **Sort without index**: Sorting on fields without an index causes in-memory sort — flag on large collections
