# Research: .NET Backend Rewrite

**Branch**: `dotnet-backend` | **Date**: 2026-09-28

All NEEDS CLARIFICATION items from the plan are resolved here. Decisions are final for this feature; amendments require a plan update per constitution §Governance.

---

## R-01: Partial PUT — Distinguishing JSON `null` from absent field

**Problem**: The frontend sends three distinct PUT payloads:
- Complete: `{ "status": "complete", "completedAt": "<ISO>" }` — completedAt present with a value
- Reopen: `{ "status": "active", "completedAt": null }` — completedAt present and explicitly null
- Rename: `{ "title": "new" }` — completedAt absent entirely

With a plain `DateTime? CompletedAt` property, System.Text.Json maps both "absent" and "JSON null" to `null`. The server cannot distinguish them.

**Decision**: Type `CompletedAt` as `JsonElement?` in `UpdateTaskDto`.

| Wire state | C# result | Controller action |
|-----------|-----------|-------------------|
| Absent | `null` (no value) | Leave DB field unchanged |
| `"completedAt": null` | `JsonElement` with `ValueKind == JsonValueKind.Null` | Write `null` to DB |
| `"completedAt": "2026-..."` | `JsonElement` with `ValueKind == JsonValueKind.String` | Parse and write date to DB |

The `HasValue` property on `JsonElement?` distinguishes absent (false) from present (true). When present, `ValueKind` distinguishes null from a real value.

**Alternatives considered**:
- `JsonMergePatch` library — adds a dependency; unnecessary given the single field needing this treatment.
- Custom model binder — more complex; same outcome.
- `JsonDocument` in controller action — works but bypasses model binding entirely; less idiomatic.

**How it is tested**: Two integration tests on the same pre-created task:
1. `PUT { "status": "active", "completedAt": null }` → GET task → `completedAt` is JSON null.
2. Complete a task with a non-null `completedAt` (`PUT { "status": "complete", "completedAt": "2026-09-28T03:00:00.000Z" }`), then send a rename-only `PUT { "title": "renamed" }` → `completedAt` is unchanged (still the non-null value). Matches tasks.md T032 `PutTask_RenameOnly_LeavesCompletedAtUnchanged`; a null-before/null-after check could not distinguish absent from null.

---

## R-02: DF-01 — POST blank title → 500 without triggering `[ApiController]` auto-400

**Problem**: `[ApiController]` automatically returns `400 ModelState` errors when any validation attribute fails. The spec requires POST blank/null title → 500, not 400.

**Decision**: `CreateTaskDto.Title` is `string?` with **zero validation attributes**. No `[Required]`, no `[MinLength]`. `ModelState` stays valid for any input.

The EF entity enforces correctness at the database level:
- `NOT NULL` constraint on `"Title"` column → rejects C# `null`.
- `CHECK (trim("Title") <> '')` constraint → rejects empty or whitespace-only strings.

Either violation throws `DbUpdateException` from `SaveChangesAsync`. The global exception handler converts any unhandled exception to `500 { "message": "Lỗi hệ thống" }`.

**Why this mirrors Mongoose**: In the Node controller, the `catch` block does not check for `ValidationError` on the create path — it falls straight to `res.status(500)`. The .NET design achieves the same outcome through a different mechanism but produces identical HTTP behavior.

**Important caveat for testers**: The frontend's `AddTask.jsx:13` guards blank input before sending, so this 500 path is never triggered by normal UI usage. It exists only for direct API calls.

---

## R-03: DF-02 — Malformed id → 500

**Problem**: The original Node controller throws `CastError` (MongoDB ObjectId parsing) → unhandled catch → 500. The new backend uses UUID. A malformed id string (e.g., `"abc"`) must also produce 500.

**Decision**: Route template uses `{id}` with no type constraint (not `{id:guid}`). The controller receives `id` as `string`. The service calls `Guid.Parse(id)`. A malformed string throws `FormatException`, which propagates to the global exception handler → 500.

**Verified execution order**: Title validation runs before the service call. A PUT with a malformed id AND a blank title returns 400 (title check fires first). A PUT with a malformed id AND a valid title returns 500 (title check passes, service throws FormatException). This matches the original controller behavior exactly.

**Alternatives considered**:
- `{id:guid}` route constraint — ASP.NET returns 404 for a non-matching route, not 500. Ruled out.
- `Guid.TryParse` + manual 400 — contradicts DF-02 (must be 500).

---

## R-04: Timestamp Serialization — Exactly 3 Fractional Digits, Trailing Z

**Problem**: System.Text.Json serializes `DateTime` with up to 7 fractional digits (ticks precision). The original Node/Mongoose backend serializes to ISO 8601 with 3 digits (milliseconds). The frontend (`TaskCard.jsx:145`) displays `new Date(task.createdAt).toLocaleString()`; inconsistent precision doesn't break display but would break string equality in tests and confuse `new Date()` parsing in some environments.

**Decision**: Custom `JsonConverter<DateTime>` using format string `"yyyy-MM-ddTHH:mm:ss.fffZ"`. Always converts to UTC before formatting. Registered globally in `builder.Services.AddControllers().AddJsonOptions(...)`. A companion `JsonConverter<DateTime?>` handles nullable dates.

**Millisecond truncation on write**: PostgreSQL `timestamptz` stores up to microsecond precision. Without truncation, a value written as `2026-09-28T10:00:00.001999Z` would read back as `2026-09-28T10:00:00.001999Z` and serialize as `2026-09-28T10:00:00.001Z` (truncated by formatter) — which is consistent for display but creates a subtle inconsistency for sort (the DB ordering uses microseconds; the serialized representation loses them).

To keep storage and wire format aligned:

```
truncated = new DateTime(value.Ticks - value.Ticks % TimeSpan.TicksPerMillisecond, value.Kind)
```

Applied in `TaskService` before setting `CreatedAt` and `UpdatedAt`. The stored value and the serialized value are then identical in precision.

---

## R-05: Timezone Resolution — Windows dev vs Linux/Docker

**Problem**: The original Node backend uses IANA id `Asia/Ho_Chi_Minh` (from the `TZ` environment variable, defaulting to that value). The .NET rewrite needs the same behavior on both a Windows developer host and a Linux/Docker production host.

**Decision**: Use `TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")` directly — no converter package required. Since .NET 6, the runtime ships with ICU + IANA tzdata bundled on Windows, so IANA ids resolve on both platforms. Adding a NuGet package for this would violate constitution §V (dependency minimalism) with no benefit.

**Per FR-014**: the effective id is `Environment.GetEnvironmentVariable("TZ")` if set, otherwise `"Asia/Ho_Chi_Minh"`. This mirrors the Node backend's `process.env.TZ ||= "Asia/Ho_Chi_Minh"`. Resolution runs once at startup; the resulting `TimeZoneInfo` is registered as a singleton for DI.

**Platform caveats**:
- **Windows** (dev): `Asia/Ho_Chi_Minh` resolves natively via bundled ICU/tzdata. Verified by the first `DateRangeCalculatorTests` unit test, which asserts `TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")` does not throw — a deterministic sanity check that fails loudly if the local runtime is misconfigured.
- **Linux/Docker**: slim base images (e.g. `mcr.microsoft.com/dotnet/aspnet:10.0-alpine`, `-noble-chiseled`) do NOT include the OS `tzdata` package by default. No Dockerfile is in scope for this rewrite; if the API is ever containerized, its base image MUST include tzdata (`apk add --no-cache tzdata` for Alpine, `apt-get install -y tzdata` for Debian/Ubuntu). Without it, `FindSystemTimeZoneById` throws `TimeZoneNotFoundException` at startup.

**Alternatives considered**:
- `TimeZoneConverter` NuGet package — an extra dependency that only became necessary before .NET 6's bundled tzdata. Rejected as unnecessary weight.
- Manual try/catch on two IDs (`Asia/Ho_Chi_Minh` and `SE Asia Standard Time`) — brittle and confusing; the bundled tzdata makes the IANA id work everywhere.

**Timezone injection**: `DateRangeCalculator` receives the resolved `TimeZoneInfo` through DI. Tests pass a fixed `TimeZoneInfo` resolved once in the test fixture.

---

## R-06: TimeProvider Injection for Clock Control

**Problem**: Date-range boundaries (`today`, `week`, `month`) depend on "now". Tests need a controllable clock to create tasks with specific `createdAt` values and verify correct filtering.

**Decision**: `System.TimeProvider` (built in since .NET 8; available in .NET 10) injected via DI into `TaskService` and `DateRangeCalculator`. Tests use `FakeTimeProvider` from the `Microsoft.Extensions.TimeProvider.Testing` NuGet package (namespace `Microsoft.Extensions.Time.Testing`).

**Package**: `Microsoft.Extensions.TimeProvider.Testing` is a separate NuGet package listed in the plan's test project manifest. `FakeTimeProvider.Advance(TimeSpan)` moves the clock forward between task creations, guaranteeing distinct `createdAt` timestamps and providing the DF-04 mitigation required by the spec Clarifications.

**Pattern**:
- Production: `builder.Services.AddSingleton(TimeProvider.System)`
- Tests: `factory.WithWebHostBuilder(b => b.ConfigureServices(s => s.AddSingleton<TimeProvider>(fake)))`

---

## R-07: EF Queries Replacing MongoDB `$facet`

**Problem**: The Mongo implementation runs four aggregation branches concurrently in a single `$facet`. EF Core has no `$facet` equivalent, and — critically — `DbContext` is **not thread-safe**. Attempting to `await Task.WhenAll(...)` on multiple queries against the same context raises `InvalidOperationException: A second operation was started on this context instance before a previous operation completed`.

**Decision**: Four LINQ queries sharing a base `IQueryable<TaskEntity>` (not yet materialized), each awaited **sequentially** on the same context:

```
base            IQueryable filtered by dateMatch (or full table if no date)
filtered        base filtered by statusMatch (or same as base if no status filter)

taskPage       = await filtered.OrderBy(sort).Skip(skip).Take(limit).ToListAsync()
totalCount     = await filtered.CountAsync()
activeCount    = await base.CountAsync(t => t.Status == "active")
completeCount  = await base.CountAsync(t => t.Status == "complete")
```

Each query completes before the next begins. For the target scale (SC-005: 10k rows, single user) the four sequential round-trips remain well under the 1 s budget. If performance ever becomes a bottleneck, the correct escalation is a separate `DbContext` per parallel branch — not `Task.WhenAll` on a shared context.

**Sort expression**: `query.OrderBy(t => t.Status == "active" ? 0 : 1).ThenByDescending(t => t.CreatedAt)`. EF Core translates this to `ORDER BY CASE WHEN "Status" = 'active' THEN 0 ELSE 1 END, "CreatedAt" DESC`.

---

## R-08: Integration Test Strategy — Testcontainers vs Compose DB

| Concern | Testcontainers | Shared Compose DB |
|---------|---------------|-------------------|
| Test isolation | Fresh DB per test run | Requires manual cleanup between tests |
| CI pipeline | Docker daemon only; no pre-running services | `docker compose up` must run before `dotnet test` |
| Startup overhead | ~2–4 s per `dotnet test` invocation | 0 (DB already running) |
| Port conflicts | Random ephemeral port | Fixed 5432; may conflict with running app |
| Reproducibility | Identical every run | Depends on prior test state if cleanup fails |

**Decision**: Testcontainers for all integration tests. The `PostgresContainerFixture` (implementing `IAsyncLifetime`) starts one PostgreSQL 16 container per test collection and shares it across all tests in the collection. Each test is responsible for seeding and cleaning its own data (or uses transactions rolled back after the test). The compose DB is reserved for the running application.

---

## R-09: Global Exception Handler

**Decision**: `IExceptionHandler` (introduced .NET 8, available .NET 10) registered via `app.UseExceptionHandler()`. This approach avoids `try/catch` in every controller action.

The handler catches all unhandled exceptions and writes:

```json
HTTP 500
{ "message": "Lỗi hệ thống" }
```

Controller-level 400 responses (`title` blank on PUT, status enum on PUT) are returned directly — they do not go through the exception handler.

**Content-Type**: The handler must set `Content-Type: application/json` explicitly; the default exception handler does not.

---

## R-10: Title Trimming (Mongoose `trim: true` Parity)

**Problem**: The original Mongoose schema sets `title: { type: String, required: true, trim: true }`. Mongoose auto-trims on save. The spec Key Entities section calls out this behavior. The .NET rewrite must preserve it so the response echoes the stored (trimmed) value, matching the frontend's expectation of what it will read back.

**Decision**: `TaskService` calls `title.Trim()` before writing on both `Create` and `Update` (rename) paths. Consequences:

- `"  Đi chợ  "` submitted on POST is stored as `"Đi chợ"` and returned as `"Đi chợ"`.
- `"\t\n"` (tabs/newlines only) trims to `""`, which fails the DB `CHECK (trim("Title") <> '')` constraint. On POST → `DbUpdateException` → 500 (DF-01). On PUT → controller's `string.IsNullOrWhiteSpace` check returns 400 before the DB is touched.
- Only one trim call per path; no double-trim between service and DB.

**Tests**:
- Integration (service-level, against the Testcontainers Postgres — no in-memory provider is approved): `TaskServiceTests.Create_TrimsTitle_BeforeSave` — assert the entity's `Title` after `Create("  x  ")` equals `"x"`. Same for rename.
- Integration: `TasksControllerTests.PostTask_PaddedTitle_StoresTrimmed` — POST `{ "title": "  x  " }`, GET the list, assert the response contains `"title": "x"`.
- Integration: `TasksControllerTests.PostTask_TabsAndNewlinesOnly_Returns500` — POST `{ "title": "\t\n" }`, assert HTTP 500 with `{ "message": "Lỗi hệ thống" }`.

---

## R-11: PUT `completedAt` Parsing and Storage

**Problem**: Two constraints:
1. Npgsql (the PostgreSQL EF Core provider) requires `DateTime` values with `Kind == DateTimeKind.Utc` for `timestamptz` columns. Passing a `Local`-kind value throws at `SaveChangesAsync`.
2. `DateTime.Parse("2026-09-28T03:00:00.000Z")` on many systems returns `DateTimeKind.Local` (because the parser normalizes the value to the local timezone). Using it directly would blow up on the Complete button — the single most common frontend action.

**Decision**: Parse via `JsonElement.GetDateTime()`, which returns UTC-kind for ISO strings ending in `Z`. Equivalent explicit form: `DateTimeOffset.Parse(str, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).UtcDateTime`. Then truncate to milliseconds using the same helper as `CreatedAt`/`UpdatedAt` (see R-04) so the stored value round-trips exactly.

**Pipeline** (only when `dto.CompletedAt.HasValue && dto.CompletedAt.Value.ValueKind == JsonValueKind.String`):

```
try
    parsed    = dto.CompletedAt.Value.GetDateTime()          // UTC kind
    truncated = TruncateToMs(parsed)                          // millisecond precision
    entity.CompletedAt = truncated                            // safe to persist
catch (FormatException)
    return 400 { "message": "Dữ liệu nhiệm vụ không hợp lệ" }
```

**Why 400 not 500 for invalid dates**: api-contract §5.8 documents that an unparseable `completedAt` in the original backend surfaces as `ValidationError` from Mongoose → controller catch → 400 with the generic `"Dữ liệu nhiệm vụ không hợp lệ"` message. The .NET rewrite preserves this by catching `FormatException` from `GetDateTime()` and returning the same 400. It does not fall through to the global 500 handler.

**Tests**:
- Integration: `TasksControllerTests.PutTask_FrontendCompletePayload_PersistsUtc` — send the exact payload the frontend sends on Complete: `{ "status": "complete", "completedAt": "2026-09-28T03:00:00.000Z" }`. Assert HTTP 200. Assert `completedAt` in the response equals the input string exactly (round-trip test — ISO parse → UTC store → serializer → same 3-fractional-digit output).
- Integration: `TasksControllerTests.PutTask_InvalidCompletedAt_Returns400_WithInvalidDataMessage` — send `{ "completedAt": "not-a-date" }`. Assert HTTP 400 with body `{ "message": "Dữ liệu nhiệm vụ không hợp lệ" }`.
