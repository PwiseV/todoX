# Implementation Plan: .NET Backend Rewrite

**Branch**: `dotnet-backend` | **Date**: 2026-09-28 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/001-dotnet-backend-rewrite/spec.md`

## Summary

Rewrite the todoX backend as an ASP.NET Core 10 Web API (controllers, EF Core + Npgsql, PostgreSQL via Docker Compose) so the existing React frontend operates unchanged against `http://localhost:5001`. The new backend reproduces every behavioral contract in `docs/api-contract.md` §1–§4, preserves the §7 deferred quirks (DF-01 through DF-05), and applies only the §6 authorized deviations (UUID string `_id`, constant `__v` = 0).

## Technical Context

**Language/Version**: C# 13, .NET 10

**Primary Dependencies**: ASP.NET Core Web API (controllers), Entity Framework Core 10 (Npgsql provider), System.Text.Json (built-in)

**Storage**: PostgreSQL 16 via Docker Compose (application); Testcontainers.PostgreSql (integration tests)

**Testing**: xUnit 2, `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`), Testcontainers.PostgreSql

**Target Platform**: Linux/Docker (containerized Postgres); Windows or Linux for development host; .NET 10 cross-platform

**Performance Goals**: Task list response ≤ 1 s for 10,000 tasks under single-user load (SC-005)

**Constraints**: Port 5001 fixed in `launchSettings.json`; all routes under `/api`; CORS open to `http://localhost:5173` in non-production; no authentication

**Scale/Scope**: Single-user personal task manager; no concurrency, rate-limiting, or auth requirements

## Constitution Check

*Constitution v1.2.0 — evaluated pre-design.*

| Principle | Requirement | Status |
|-----------|-------------|--------|
| I. Scope Isolation | All work in `backend-dotnet/`; `frontend/` and `backend/` frozen | ✅ Pass — plan targets `backend-dotnet/` only; any new docs go to `docs/` as read-only guidance |
| II. Prescribed Stack | ASP.NET Core Web API, .NET 10, controllers, EF Core, PostgreSQL, Docker Compose | ✅ Pass — stack matches exactly; no alternate ORM, framework, or DB engine introduced |
| III. Frontend Compatibility | Routes/methods/JSON shapes match api-contract.md; §6 deviations authorized; §7 quirks preserved | ✅ Pass — UUID `_id` and `__v`=0 are authorized in §6; DF-01/DF-02/DF-03/DF-04/DF-05 preserved per §7 |
| IV. Test-First for Business Rules | xUnit tests for every §4 rule, written before/alongside impl, must fail against empty impl | ✅ Pass — full test matrix enumerated in Test Plan section |
| V. Supply Chain & Secret Hygiene | All NuGet packages listed in plan.md; secrets via git-ignored config | ✅ Pass — complete manifest below; local Postgres uses trust auth bound to `127.0.0.1` (Key Design Decision 11), so the committed `appsettings.Development.json` holds a password-free connection string and no secret exists to leak |
| Dependency Rule | Every NuGet package listed in plan.md | ✅ Pass — 8 packages listed, each justified (2 production, 6 test) |

**No violations. Plan is clear to proceed.**

---

## NuGet Package Manifest

### API project — `TodoX.Api`

| Package | Version | Justification |
|---------|---------|---------------|
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.x (match runtime) | EF Core PostgreSQL provider; pulls in `Microsoft.EntityFrameworkCore` transitively |
| `Microsoft.EntityFrameworkCore.Design` | 10.x (match EF) | Enables `dotnet ef migrations add` / `database update` CLI at build time |

### Test project — `TodoX.Tests`

| Package | Version | Justification |
|---------|---------|---------------|
| `xunit` | 2.x | Sole approved test framework (constitution §Technology Stack) |
| `xunit.runner.visualstudio` | 2.x | Test runner adapter for VS/Rider and `dotnet test` |
| `Microsoft.NET.Test.Sdk` | latest | .NET test platform SDK; required for `dotnet test` to discover xUnit tests |
| `Testcontainers.PostgreSql` | 4.x (4.15.0) | Starts a real PostgreSQL instance per test session; integration tests run against the same DB engine as production. Amended 2026-09-30 from 3.x: 3.10.0 (latest 3.x) depends on SSH.NET 2023.0.0 with high-severity advisories GHSA-mggc-4xg6-vcxf and GHSA-q939-rpr3-3284 (NuGet NU1903); 4.15.0 depends on the fixed SSH.NET 2026.0.0 |
| `Microsoft.AspNetCore.Mvc.Testing` | 10.x | `WebApplicationFactory<Program>` tests the full request pipeline in-process without a network round-trip |
| `Microsoft.Extensions.TimeProvider.Testing` | 10.10.0 (latest stable on NuGet as of 2026-09-30; versions independently of the .NET runtime) | Provides `FakeTimeProvider` (namespace `Microsoft.Extensions.Time.Testing`) used to control the injected `TimeProvider` in tests; advances the clock between task creations for DF-04 mitigation |

**Total: 8 NuGet packages** (2 production, 6 test-only).

### Tooling

| Tool | Version | Justification |
|------|---------|---------------|
| `dotnet-ef` (global .NET tool) | 10.x (match EF Core major; 10.0.12 latest stable as of 2026-09-30) | EF Core CLI for `dotnet ef migrations add` (tasks.md T010). Installed via `dotnet tool install --global dotnet-ef --version 10.*`; not a project package reference |

---

## Key Design Decisions

*(Full rationale and alternatives in [research.md](research.md))*

### 1. Partial PUT — distinguishing `null` from absent

`UpdateTaskDto.CompletedAt` is typed as `JsonElement?` (nullable value type).

| Wire value | C# value | Interpretation |
|-----------|----------|----------------|
| Field absent from JSON | `null` (`HasValue = false`) | Do not touch `CompletedAt` in DB |
| `"completedAt": null` | `JsonElement` with `ValueKind.Null` | Set `CompletedAt = null` in DB |
| `"completedAt": "2026-..."` | `JsonElement` with `ValueKind.String` | Parse and set `CompletedAt` to the date |

This is the only field that requires this treatment. `Title` and `Status` follow the conventional `string?` pattern (null = absent = ignore).

**Testing**: Send `PUT { "status": "active", "completedAt": null }` → verify completedAt cleared. Complete a task with a non-null `completedAt` (`PUT { "status": "complete", "completedAt": "2026-09-28T03:00:00.000Z" }`), then send a rename-only `PUT { "title": "x" }` → verify `completedAt` is unchanged (still the non-null value; matches tasks.md T032 `PutTask_RenameOnly_LeavesCompletedAtUnchanged`).

### 2. DF-01 — POST blank title → 500 despite `[ApiController]`

`[ApiController]` auto-returns 400 only when `ModelState.IsValid == false`. `ModelState` is invalid only when validation attributes (`[Required]`, `[MinLength]`, etc.) are present and fail. Strategy:

- `CreateTaskDto.Title` is `string?` with **no validation attributes** → `ModelState` stays valid for any input including null and `""`.
- The EF entity has `NOT NULL` + `CHECK (trim("Title") <> '')` via `HasCheckConstraint`.
- A null or whitespace title passes model binding and reaches EF's `SaveChangesAsync`, which throws `DbUpdateException` wrapping a PostgreSQL constraint violation.
- The global exception handler catches it → `500 { "message": "Lỗi hệ thống" }`.

This exactly mirrors the Mongoose `ValidationError` → unhandled catch → 500 path in the Node controller.

### 3. DF-02 — Malformed id → 500

Route template: `[Route("/api/tasks/{id}")]` — `{id}` has no type constraint (no `:guid`). The controller receives `id` as `string`. The service calls `Guid.Parse(id)`, which throws `FormatException` for malformed input. This propagates to the global exception handler → `500 { "message": "Lỗi hệ thống" }`.

**Verified order from original controller**: title validation runs first (lines 131–138); a PUT with a malformed id but a valid (non-blank) title skips the 400, reaches the DB call, and exits via 500. The .NET implementation mirrors this: the controller performs title validation before calling the service (which does the Guid.Parse).

### 4. Timestamp serialization — exactly 3 fractional digits, trailing Z

Default `System.Text.Json` serialization of `DateTime` produces variable precision (e.g., 7 digits). A custom `JsonConverter<DateTime>` formats as `"yyyy-MM-ddTHH:mm:ss.fffZ"` (always UTC, always 3 digits). A companion `JsonConverter<DateTime?>` handles nullables. Both registered globally in `JsonSerializerOptions`.

**Millisecond truncation on write**: PostgreSQL `timestamptz` stores microseconds (6 digits). To prevent microsecond creep breaking string equality and sort stability, all `DateTime` values are truncated to milliseconds before `SaveChangesAsync`:

```
truncated = new DateTime(dt.Ticks - dt.Ticks % TimeSpan.TicksPerMillisecond, dt.Kind)
```

Applied to `CreatedAt` and `UpdatedAt` in the service layer on every write.

### 5. Timezone — Windows dev vs Linux/Docker

Use `TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")` directly. Since .NET 6, the runtime ships ICU with IANA tzdata bundled on Windows, so the IANA ID resolves on both Windows and Linux without a converter package. On Linux/Docker slim base images, the OS `tzdata` package MUST be installed (`apt-get install -y tzdata` for Debian/Ubuntu-based images) or the ID will fail to resolve at startup.

Per FR-014, the effective timezone id is resolved from the `TZ` environment variable if set (matching the original Node backend's `process.env.TZ ||= "Asia/Ho_Chi_Minh"`); otherwise it falls back to `"Asia/Ho_Chi_Minh"`. This resolution happens once at startup and the resulting `TimeZoneInfo` is registered as a singleton for DI.

The first `DateRangeCalculatorTests` unit test asserts that `Asia/Ho_Chi_Minh` resolves successfully on the developer's host (Windows for this project) — a fast, deterministic sanity check that fails loudly if the runtime does not ship IANA data.

### 6. TimeProvider injection

`System.TimeProvider` (introduced .NET 8, available .NET 10) is injected into `TaskService` and `DateRangeCalculator`. Tests use `FakeTimeProvider` from the `Microsoft.Extensions.TimeProvider.Testing` NuGet package (namespace `Microsoft.Extensions.Time.Testing`) — listed in the test project manifest above. `FakeTimeProvider.Advance(TimeSpan)` moves the clock forward between task creations, guaranteeing distinct `createdAt` values (DF-04 mitigation).

**Pattern**:
- Production: `builder.Services.AddSingleton(TimeProvider.System)`
- Tests: `factory.WithWebHostBuilder(b => b.ConfigureServices(s => s.AddSingleton<TimeProvider>(fake)))`

### 7. EF queries replacing MongoDB `$facet`

The Mongo implementation runs four aggregation branches in one `$facet` stage. EF Core's `DbContext` is **not thread-safe** — two concurrent queries on the same context raise `InvalidOperationException`. The four queries must therefore be awaited **sequentially** on the same context:

```
base     = context.Tasks.Where(dateFilter)          // nullable; omitted if no date filter
filtered = base.Where(statusFilter)                  // nullable; omitted if no status filter

taskPage       = await filtered.OrderBy(sortExpr).Skip(skip).Take(limit).ToListAsync()
totalCount     = await filtered.CountAsync()
activeCount    = await base.CountAsync(t => t.Status == "active")
completeCount  = await base.CountAsync(t => t.Status == "complete")
```

Each query is issued and awaited before the next begins. No EF query is run more than once. For the target scale (SC-005: 10k rows, single user) the four round-trips are still well under the 1 s budget.

**Sort expression**: `OrderBy(t => t.Status == "active" ? 0 : 1).ThenByDescending(t => t.CreatedAt)` — translates to SQL `ORDER BY CASE WHEN "Status" = 'active' THEN 0 ELSE 1 END, "CreatedAt" DESC`.

### 8. Integration test strategy — Testcontainers vs Compose DB

| Concern | Testcontainers | Shared Compose DB |
|---------|---------------|-------------------|
| Isolation | Each test session gets a fresh DB | Requires explicit cleanup between tests |
| CI setup | Docker daemon only; no pre-running services | Compose must be started in CI before tests |
| Startup time | ~2–4 s per `dotnet test` invocation | Instant (DB already running) |
| Port conflicts | Random port assigned by Testcontainers | Fixed port 5432; may conflict |
| Chosen | ✅ Yes | For app only (not tests) |

**Decision**: Testcontainers for all integration tests. The compose DB runs the application; tests are self-contained.

### 9. Title trimming (Mongoose `trim: true` parity)

`TaskService` calls `title.Trim()` before writing on both create and rename. Consequences:
- `"  x  "` is stored as `"x"`.
- A title of only whitespace (spaces, tabs, newlines) trims to `""`, which fails the `CHECK (trim("Title") <> '')` constraint. On POST this yields 500 (DF-01: `DbUpdateException` → global handler); on PUT the controller's `string.IsNullOrWhiteSpace` check returns 400 before the DB is ever hit.

Trimming happens once in the service layer to keep the DB and the response DTO consistent (the response echoes what was stored, not what was sent).

### 10. PUT `completedAt` parsing (Npgsql UTC constraint)

Npgsql accepts only `DateTime` values with `Kind == Utc` for `timestamptz` columns. `DateTime.Parse(...)` returns `DateTimeKind.Local`, which would throw at `SaveChangesAsync` and produce a spurious 500 — breaking the Complete button on the frontend. Correct parsing pipeline when `dto.CompletedAt.Value.ValueKind == JsonValueKind.String`:

1. `dto.CompletedAt.Value.GetDateTime()` — returns UTC-kind for ISO strings ending in `Z` (equivalent to `DateTimeOffset.Parse(str, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).UtcDateTime`).
2. Truncate to milliseconds using the same helper as `CreatedAt`/`UpdatedAt`.
3. Wrap step 1 in `try/catch (FormatException)`; on failure return 400 `{ "message": "Dữ liệu nhiệm vụ không hợp lệ" }` (matches api-contract §5.8: invalid date strings surface the generic validation-error message, not 500).

### 11. Local Postgres — trust auth, localhost-only (two-command bootstrap)

The constitution requires `docker compose up` + `dotnet run` with no other step, and forbids committed credentials. Both hold by having **no credential at all** locally:

- `docker-compose.yml`: the postgres service binds `"127.0.0.1:5432:5432"` and sets `POSTGRES_HOST_AUTH_METHOD=trust`; no password, no `.env` file. The file carries the comment "dev only - trust auth, localhost-only bind, not for any non-local use."
- `TodoX.Api/appsettings.Development.json` is **committed** with the password-free connection string `Host=localhost;Port=5432;Database=todox;Username=postgres`.
- Integration tests are unaffected (Testcontainers generates its own credentials at runtime).

---

## Test Plan — Business Rules Coverage (Constitution IV)

Every §4 rule requires a named failing-first test. Tests are written before or alongside implementation.

| Business Rule (api-contract §4) | Unit Test | Integration Test |
|---------------------------------|-----------|-----------------|
| `today` → 00:00 Asia/HCM | `DateRangeCalculatorTests.Today_StartsAtMidnightHcm` | `TasksControllerTests.GetTasks_DateQuery_Today` |
| `week` → Monday 00:00 Asia/HCM | `DateRangeCalculatorTests.Week_StartsOnMondayHcm` | `TasksControllerTests.GetTasks_DateQuery_Week` |
| Sunday → previous Monday (week) | `DateRangeCalculatorTests.Week_OnSunday_GoesBackToMonday` | — |
| `month` → 1st of month 00:00 | `DateRangeCalculatorTests.Month_StartsOnFirstOfMonth` | `TasksControllerTests.GetTasks_DateQuery_Month` |
| `all`/absent/unrecognized → no filter | `DateRangeCalculatorTests.All_ReturnsNullStartDate` | `TasksControllerTests.GetTasks_DateQuery_All` |
| `filter=completed` → status `complete` | `StatusMappingTests.Completed_MapsToComplete` | `TasksControllerTests.GetTasks_Filter_Completed` |
| `filter=active` → status `active` | `StatusMappingTests.Active_MapsToActive` | `TasksControllerTests.GetTasks_Filter_Active` |
| `filter=all`/absent/unrecognized → no filter | `StatusMappingTests.All_ReturnsNoStatusCondition` | `TasksControllerTests.GetTasks_Filter_All` |
| `filter=complete` (raw DB value) → no filter | `StatusMappingTests.RawComplete_TreatedAsAll` | — |
| `page` default 1; clamped min 1 | `PaginationTests.Page_Defaults_And_MinClamp` | `TasksControllerTests.GetTasks_PageZero_TreatedAsPage1` |
| `limit` default 5; clamped [1, 50] | `PaginationTests.Limit_Clamped_To_Range` | — |
| `page` > `totalPages` echoed unchanged | `PaginationTests.Page_AboveTotal_NotClamped` | `TasksControllerTests.GetTasks_PageBeyondTotal_EchoesFarPage` |
| `totalPages` ≥ 1 always | `PaginationTests.TotalPages_AtLeastOne` | `TasksControllerTests.GetTasks_EmptyDb_TotalPagesIsOne` |
| Sort: active before complete | — | `TasksControllerTests.GetTasks_Sort_ActiveFirst` |
| Sort: newest first within group | — | `TasksControllerTests.GetTasks_Sort_NewestFirstWithinGroup` |
| `activeCount`/`completeCount` ignore `filter` | — | `TasksControllerTests.GetTasks_Counts_IndependentOfFilter` |
| `totalCount` respects both date and status filters | — | `TasksControllerTests.GetTasks_TotalCount_RespectsBothFilters` |
| Title trimmed before save (POST + PUT) | — | `TaskServiceTests.Create_TrimsTitle_BeforeSave`, `TaskServiceTests.Rename_TrimsTitle_BeforeSave` (service-level, against Testcontainers Postgres — no in-memory provider is approved), `TasksControllerTests.PostTask_PaddedTitle_StoresTrimmed` |
| Whitespace-only title (tabs/newlines) on POST → 500 | — | `TasksControllerTests.PostTask_TabsAndNewlinesOnly_Returns500` |
| PUT `completedAt` invalid ISO → 400 | — | `TasksControllerTests.PutTask_InvalidCompletedAt_Returns400_WithInvalidDataMessage` |
| PUT with exact frontend Complete payload persists correctly | — | `TasksControllerTests.PutTask_FrontendCompletePayload_PersistsUtc` |

---

## Project Structure

### Documentation (this feature)

```text
specs/001-dotnet-backend-rewrite/
├── plan.md              ← this file
├── research.md          ← Phase 0 decisions and rationale
├── data-model.md        ← Entity + DB schema + DTO shapes
├── contracts/
│   ├── tasks-api.md     ← /api/tasks endpoint contracts
│   └── health-api.md    ← /api/health endpoint contract
├── quickstart.md        ← Step-by-step validation guide
└── tasks.md             ← Phase 2 output (created by /speckit-tasks)
```

### Source Code — `backend-dotnet/`

```text
backend-dotnet/
├── TodoX.Api/
│   ├── Controllers/
│   │   ├── HealthController.cs          GET /api/health
│   │   └── TasksController.cs           CRUD + paginated list
│   ├── Data/
│   │   ├── AppDbContext.cs
│   │   └── Migrations/                  EF Core migrations (checked in)
│   ├── DTOs/
│   │   ├── TaskResponseDto.cs           wire shape; _id/__v via JsonPropertyName
│   │   ├── CreateTaskDto.cs             { title: string? }; no [Required] — DF-01
│   │   ├── UpdateTaskDto.cs             partial; CompletedAt: JsonElement?
│   │   └── TaskListResponseDto.cs       tasks + counts + pagination fields
│   ├── Entities/
│   │   └── TaskEntity.cs                EF Core entity; Guid PK
│   ├── Services/
│   │   ├── ITaskService.cs              service contract + TaskUpdate and TaskListResult records
│   │   ├── TaskService.cs               business logic, EF queries
│   │   ├── StatusFilter.cs              filter → status mapping ("completed" → "complete")
│   │   └── Pagination.cs                page/limit parsing (parseInt parity, DF-05), skip, totalPages
│   ├── Infrastructure/
│   │   ├── DateRangeCalculator.cs       today/week/month in Asia/Ho_Chi_Minh; TZ resolution
│   │   ├── MillisecondDateTimeConverter.cs   ISO 8601 3-frac-digit serializer
│   │   ├── DateTimeTruncation.cs        truncate DateTime to milliseconds
│   │   └── GlobalExceptionHandler.cs    IExceptionHandler → 500 { "message": "Lỗi hệ thống" }
│   ├── appsettings.json                 non-secret config (CORS origin, env marker)
│   ├── appsettings.Development.json     committed; password-free connection string (trust auth, Decision 11)
│   ├── Properties/
│   │   └── launchSettings.json          applicationUrl: "http://localhost:5001"
│   └── TodoX.Api.csproj
├── TodoX.Tests/
│   ├── Unit/
│   │   ├── DateRangeCalculatorTests.cs  today/week/month boundary logic
│   │   ├── PaginationTests.cs           page/limit clamping, totalPages edge cases
│   │   ├── StatusMappingTests.cs        "completed" → "complete" filter mapping
│   │   ├── MillisecondDateTimeConverterTests.cs   3-frac-digit wire format
│   │   ├── DateTimeTruncationTests.cs   ms truncation helper
│   │   └── GlobalExceptionHandlerTests.cs   500 body + content type
│   ├── Integration/
│   │   ├── TasksControllerTests.cs      partial class: shared setup/helpers
│   │   ├── TasksControllerTests.Create.cs       POST
│   │   ├── TasksControllerTests.Update.cs       PUT validation/rename/404/DF-02
│   │   ├── TasksControllerTests.CompletedAt.cs  PUT absent vs null completedAt
│   │   ├── TasksControllerTests.Delete.cs       DELETE
│   │   ├── TasksControllerTests.List.cs         GET filters/pagination/sort
│   │   ├── TasksControllerTests.Counts.cs       GET activeCount/completeCount
│   │   ├── TaskServiceTests.cs          service-level title trimming (Testcontainers)
│   │   ├── PerformanceTests.cs          SC-005, 10k tasks < 1 s
│   │   ├── HealthControllerTests.cs     /api/health response shape + timestamp format
│   │   └── Fixtures/
│   │       ├── PostgresContainerFixture.cs   Testcontainers: IAsyncLifetime; shared per collection
│   │       └── TodoXWebFactory.cs            WebApplicationFactory<Program>; injects test DB
│   └── TodoX.Tests.csproj
├── TodoX.sln
├── docker-compose.yml                   PostgreSQL 16; 127.0.0.1:5432; trust auth, no password (dev only)
├── .gitignore                           bin/obj etc.; `!*.sln` overrides the repo-root *.sln ignore
└── README.md                            two-command bootstrap, test commands, preserved quirks DF-01..DF-05
```

**Structure Decision**: Single solution, two projects (API + tests). No monorepo layers — this is a single-service rewrite. `backend-dotnet/` is self-contained per Principle I.

## Complexity Tracking

> No constitution violations. No complexity justification required.
