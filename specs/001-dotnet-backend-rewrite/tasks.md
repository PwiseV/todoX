---

description: "Task list for the .NET backend rewrite of todoX"
---

# Tasks: .NET Backend Rewrite

**Input**: Design documents from `/specs/001-dotnet-backend-rewrite/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/tasks-api.md, contracts/health-api.md, quickstart.md

**Tests**: REQUIRED. Constitution Principle IV (NON-NEGOTIABLE) says every `docs/api-contract.md` §4 business rule needs an xUnit test that is written first and fails against an empty implementation. Each rule is therefore a **test task (red)** followed by an **implementation task (green)**. A test task may add the smallest compile-only stubs it needs (signatures that throw `NotImplementedException` or return placeholder values) so the suite builds and fails by assertion, not by compile error.

**Organization**: Setup and Foundational phases come first, then one phase per user story (US1 → US2 → US3), then Polish.

**Commit rule**: one task = one small commit. The implementer commits; this document only plans the work.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1 / US2 / US3, mapped to spec.md user stories
- All source paths are under `backend-dotnet/` (Constitution Principle I). `frontend/` and `backend/` MUST NOT be touched.

## Conventions used by every task

- **Unit tests** live in `backend-dotnet/TodoX.Tests/Unit/` and are tagged `[Trait("Category", "Unit")]` so `dotnet test --filter "Category=Unit"` runs without Docker (quickstart.md).
- **Integration tests** live in `backend-dotnet/TodoX.Tests/Integration/`, join the shared Postgres collection from T019, and start each test from an empty `Tasks` table.
- **DF-04 mitigation**: integration tests MUST advance the `FakeTimeProvider` (for example by 1 s) between task creations, so no two tasks share a `createdAt`. `FakeTimeProvider` cannot move backwards, so seed tasks oldest first.
- **Vietnamese messages**: assert on parsed JSON (`JsonDocument`), not on raw response text, because System.Text.Json escapes non-ASCII characters by default.
- `TasksControllerTests` is one `partial class` spread across several files (`TasksControllerTests.*.cs`), so each story's tests are in their own file and can be written in parallel. Test method names match the plan.md Test Plan exactly, because the constitution's test gate checks for these names.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Solution skeleton, approved packages, local infrastructure. No business logic.

- [X] T001 Create `backend-dotnet/.gitignore` ignoring `bin/`, `obj/`, `*.user`, and `TestResults/`, and add the negation `!*.sln`. Do **not** ignore `TodoX.Api/appsettings.Development.json`; it is committed (T007). The repo-root `.gitignore` ignores `*.sln`; without the negation `backend-dotnet/TodoX.sln` would never be committed.
- [X] T002 Scaffold the solution and API project: in `backend-dotnet/` run `dotnet new sln -n TodoX --format sln` (the plan names `TodoX.sln`, and .NET 10 defaults to `.slnx`) and `dotnet new webapi --use-controllers -n TodoX.Api -f net10.0`, then add the project to the solution. Delete the template's `WeatherForecast.cs` and `Controllers/WeatherForecastController.cs`, and remove any template OpenAPI/Swagger package reference and its calls in `backend-dotnet/TodoX.Api/Program.cs`, because that package is not in the plan's manifest. Result: `backend-dotnet/TodoX.sln`, `backend-dotnet/TodoX.Api/TodoX.Api.csproj`.
- [X] T003 Add exactly the two approved API packages to `backend-dotnet/TodoX.Api/TodoX.Api.csproj`: `Npgsql.EntityFrameworkCore.PostgreSQL` 10.x and `Microsoft.EntityFrameworkCore.Design` 10.x. Add no others (Constitution V).
- [ ] T004 Scaffold `backend-dotnet/TodoX.Tests/TodoX.Tests.csproj` (xUnit, `net10.0`), add it to `backend-dotnet/TodoX.sln`, and add a project reference to `TodoX.Api`. The package list MUST be exactly the six approved test packages: `xunit` 2.x, `xunit.runner.visualstudio` 2.x, `Microsoft.NET.Test.Sdk`, `Testcontainers.PostgreSql` 3.x, `Microsoft.AspNetCore.Mvc.Testing` 10.x, `Microsoft.Extensions.TimeProvider.Testing` 10.10.0. Remove template extras such as `coverlet.collector`, and use xUnit 2, not `xunit.v3`, if the template picks v3. Create the empty folders `Unit/`, `Integration/`, and `Integration/Fixtures/`, and delete the template `UnitTest1.cs`.
- [X] T005 [P] Set `backend-dotnet/TodoX.Api/Properties/launchSettings.json` to a single `http` profile with `"applicationUrl": "http://localhost:5001"` and `ASPNETCORE_ENVIRONMENT=Development`. Remove the https profile and the `launchUrl` to swagger.
- [ ] T006 [P] Create `backend-dotnet/docker-compose.yml` with a PostgreSQL 16 service bound to `"127.0.0.1:5432:5432"`, `POSTGRES_HOST_AUTH_METHOD=trust`, `POSTGRES_DB=todox`, and a named volume. No password and no `.env` file (plan.md Key Design Decision 11). Put the comment `# dev only - trust auth, localhost-only bind, not for any non-local use.` above the service.
- [ ] T007 [P] Put only non-secret configuration in `backend-dotnet/TodoX.Api/appsettings.json`: `Cors:AllowedOrigin = "http://localhost:5173"`, logging, and no connection string. Commit `backend-dotnet/TodoX.Api/appsettings.Development.json` directly (no `.example` template, no copy step) with `ConnectionStrings:TodoX = "Host=localhost;Port=5432;Database=todox;Username=postgres"`. It contains no password.

**Checkpoint**: `dotnet build backend-dotnet/TodoX.sln` succeeds, and `docker compose up -d` (run in `backend-dotnet/`) starts Postgres.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Entity, schema, JSON wire format, error handling, DI wiring, test harness, and health endpoint. Every user story depends on these.

**⚠️ CRITICAL**: No user-story work begins until this phase is complete.

### Persistence

- [ ] T008 [P] Create `backend-dotnet/TodoX.Api/Entities/TaskEntity.cs` with properties `Guid Id`, `string Title`, `string Status`, `DateTime? CompletedAt`, `DateTime CreatedAt`, `DateTime UpdatedAt` (data-model.md "EF Core Entity").
- [ ] T009 Create `backend-dotnet/TodoX.Api/Data/AppDbContext.cs` with `DbSet<TaskEntity> Tasks`. Fluent config in `OnModelCreating`, taken verbatim from data-model.md: `ToTable("Tasks")`; `Id`: `HasDefaultValueSql("gen_random_uuid()")`; `Title`: `.IsRequired()` + `HasCheckConstraint("CK_Tasks_Title_NotBlank", "trim(\"Title\") <> ''")`; `Status`: `.IsRequired().HasDefaultValue("active")` + `HasCheckConstraint("CK_Tasks_Status_Enum", "\"Status\" IN ('active', 'complete')")`; `CompletedAt` nullable `timestamptz`; `CreatedAt`, `UpdatedAt`: `.IsRequired()`. Add no `__v` column and no uniqueness constraint on `Title`. In the same commit, register the context in `backend-dotnet/TodoX.Api/Program.cs` with `AddDbContext<AppDbContext>` using Npgsql and `ConnectionStrings:TodoX`; this is the only place that registration is added. Depends on T008.
- [ ] T010 Generate the initial migration: `dotnet ef migrations add InitialCreate --project backend-dotnet/TodoX.Api --output-dir Data/Migrations`. This requires the `dotnet-ef` global tool, version 10.x (plan.md "Tooling"). Check in `backend-dotnet/TodoX.Api/Data/Migrations/*`, and confirm the generated SQL contains both CHECK constraints and `gen_random_uuid()`. Depends on T009.

### Timestamp converter (wire format: exactly 3 fractional digits, trailing `Z`)

- [ ] T011 [P] **Test first**: write `backend-dotnet/TodoX.Tests/Unit/MillisecondDateTimeConverterTests.cs` covering both `MillisecondDateTimeConverter` (`JsonConverter<DateTime>`) and `NullableMillisecondDateTimeConverter` (`JsonConverter<DateTime?>`). Cases:
  - a UTC value with 7-digit ticks (`…:00.1234567`) serializes as `"…:00.123Z"`, truncated, not rounded
  - a whole-second UTC value serializes as `".000Z"`
  - a `DateTimeKind.Local` value is converted to UTC before formatting
  - `null` serializes as JSON `null`, not omitted
  - every output matches `^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$` (quickstart V-07)
  - reading `"2026-09-28T03:00:00.000Z"` returns `DateTimeKind.Utc`

  Add compile-only stubs of both classes in `backend-dotnet/TodoX.Api/Infrastructure/MillisecondDateTimeConverter.cs` that throw `NotImplementedException`. The tests must run red.
- [ ] T012 Implement both converters in `backend-dotnet/TodoX.Api/Infrastructure/MillisecondDateTimeConverter.cs` using format `"yyyy-MM-ddTHH:mm:ss.fffZ"` with invariant culture, converting to UTC before writing. T011 turns green. Depends on T011.

### Millisecond truncation helper

- [ ] T013 [P] **Test first**: write `backend-dotnet/TodoX.Tests/Unit/DateTimeTruncationTests.cs` for the static helper `DateTimeTruncation.TruncateToMilliseconds(DateTime)`. It must drop sub-millisecond ticks (`Ticks - Ticks % TimeSpan.TicksPerMillisecond`), preserve `Kind`, and leave an already-truncated value unchanged. Add a stub in `backend-dotnet/TodoX.Api/Infrastructure/DateTimeTruncation.cs` that throws. The tests must run red.
- [ ] T014 Implement `backend-dotnet/TodoX.Api/Infrastructure/DateTimeTruncation.cs`. T013 turns green. Depends on T013.

### Global exception handler (FR-015)

- [ ] T015 [P] **Test first**: write `backend-dotnet/TodoX.Tests/Unit/GlobalExceptionHandlerTests.cs`. Call `GlobalExceptionHandler.TryHandleAsync` with a `DefaultHttpContext` whose `Response.Body` is a `MemoryStream`, once each for `FormatException`, `DbUpdateException`, and `InvalidOperationException`. For each, assert:
  - the return value is `true`
  - `StatusCode == 500`
  - `ContentType` starts with `application/json`
  - the parsed body is exactly `{ "message": "Lỗi hệ thống" }` with no other properties (no ProblemDetails fields)

  Add a stub `GlobalExceptionHandler : IExceptionHandler` in `backend-dotnet/TodoX.Api/Infrastructure/GlobalExceptionHandler.cs` that returns `false`. The tests must run red.
- [ ] T016 Implement `backend-dotnet/TodoX.Api/Infrastructure/GlobalExceptionHandler.cs`. It writes status 500 and a JSON body `{ "message": "Lỗi hệ thống" }`, sets `Content-Type: application/json` explicitly (research.md R-09), logs the exception, and returns `true`. T015 turns green. Depends on T015.

### Wire DTO and composition root

- [ ] T017 [P] Create `backend-dotnet/TodoX.Api/DTOs/TaskResponseDto.cs` with the data-model.md field table:
  - `[JsonPropertyName("_id")] string Id`, filled from `entity.Id.ToString()` (lowercase hyphenated UUID)
  - `Title`, `Status`, `DateTime? CompletedAt`, `DateTime CreatedAt`, `DateTime UpdatedAt`
  - `[JsonPropertyName("__v")] int V`, always the constant `0`
  - a static `FromEntity(TaskEntity)` mapper

  Property order must match api-contract §2: `_id, title, status, completedAt, createdAt, updatedAt, __v`.
- [ ] T018 Wire `backend-dotnet/TodoX.Api/Program.cs`:
  - `AddControllers().AddJsonOptions(...)`: camelCase names, register both converters from T012 globally, and **do not** set `DefaultIgnoreCondition` (nulls must be serialized)
  - `AddSingleton(TimeProvider.System)`
  - `AddExceptionHandler<GlobalExceptionHandler>()` and `AddProblemDetails()` (without it, `UseExceptionHandler()` throws at startup), then `app.UseExceptionHandler()`
  - a CORS policy allowing `Cors:AllowedOrigin`, any header and any method, enabled only when `!app.Environment.IsProduction()` (FR-016)
  - `Database.MigrateAsync()` at startup only in Development
  - `MapControllers()`
  - `public partial class Program { }` at the end, for `WebApplicationFactory`

  Depends on T012, T016.

### Integration test harness

- [ ] T019 Create `backend-dotnet/TodoX.Tests/Integration/Fixtures/PostgresContainerFixture.cs`: an `IAsyncLifetime` that starts `postgres:16` via `Testcontainers.PostgreSql`, exposes the connection string, and applies EF migrations once after the container starts. Include an `[CollectionDefinition("Postgres")]` in the same file. Create `backend-dotnet/TodoX.Tests/Integration/Fixtures/TodoXWebFactory.cs`: a `WebApplicationFactory<Program>` that replaces the `AppDbContext` connection string with the container's, and has a helper `CreateClient(FakeTimeProvider clock)` that uses `WithWebHostBuilder(b => b.ConfigureServices(s => s.AddSingleton<TimeProvider>(clock)))` so each test controls its own clock. Also add a helper that truncates the `Tasks` table. Depends on T010, T018.
- [ ] T020 Create the shared part of `backend-dotnet/TodoX.Tests/Integration/TasksControllerTests.cs`. It is a `public partial class TasksControllerTests : IAsyncLifetime` in `[Collection("Postgres")]`. `InitializeAsync` truncates `Tasks` and creates a fresh `FakeTimeProvider` starting at `2026-09-30T05:00:00Z` plus its `HttpClient`. Helpers:
  - `CreateTaskAsync(string title)`: POSTs, then advances the clock by 1 s (DF-04)
  - `SetNow(DateTimeOffset)`: forward-only
  - `ReadJsonAsync(HttpResponseMessage)`

  Add no test methods yet. Depends on T019.

### Health endpoint (FR-001)

- [ ] T021 **Test first**: write `backend-dotnet/TodoX.Tests/Integration/HealthControllerTests.cs` with `[Collection("Postgres")]` and test `GetHealth_ReturnsOkAndMillisecondTimestamp`. With a `FakeTimeProvider` fixed at `2026-09-28T02:15:00.123456Z`, `GET /api/health` returns 200 and `{ "status": "ok", "time": "2026-09-28T02:15:00.123Z" }`, and `time` matches the V-07 regex. The test must run red, because the route does not exist yet (404). Depends on T019.
- [ ] T022 Implement `backend-dotnet/TodoX.Api/Controllers/HealthController.cs`: `[ApiController]`, route `api/health`, `GET` returns `{ status = "ok", time = timeProvider.GetUtcNow().UtcDateTime }`, serialized through the global converter. T021 turns green. Depends on T021.

**Checkpoint**: `dotnet test` is green. After `docker compose up -d` and `dotnet run --project TodoX.Api` (both in `backend-dotnet/`), `curl http://localhost:5001/api/health` returns the SC-003 response.

---

## Phase 3: User Story 1 - Manage Individual Tasks (Priority: P1) 🎯 MVP

**Goal**: Create, rename, complete, reopen, and delete tasks via `POST /api/tasks`, `PUT /api/tasks/{id}`, `DELETE /api/tasks/{id}`, with the preserved quirks DF-01, DF-02, and DF-03.

**Independent Test**: With only this phase done, the integration tests in `TasksControllerTests.Create.cs`, `.Update.cs`, `.CompletedAt.cs`, and `.Delete.cs` pass. Quickstart V-01 through V-05 pass via curl. Verification uses the PUT/DELETE response bodies and direct `AppDbContext` reads, so the list endpoint (US2) is not needed.

### DTOs and service contract

- [ ] T023 [P] [US1] Create `backend-dotnet/TodoX.Api/DTOs/CreateTaskDto.cs`: `{ string? Title }` with **no validation attributes** (no `[Required]`, no `[MinLength]`). This is intentional for DF-01, so `[ApiController]` never auto-returns 400.
- [ ] T024 [P] [US1] Create `backend-dotnet/TodoX.Api/DTOs/UpdateTaskDto.cs` with no validation attributes:
  - `string? Title`: null or absent means ignore
  - `string? Status`: null or absent means ignore
  - `JsonElement? CompletedAt`: absent means `HasValue == false`, JSON null means `ValueKind == Null`, an ISO string means `ValueKind == String` (research.md R-01)
- [ ] T025 [US1] **Test first (title trimming, service level)**: create the stub `backend-dotnet/TodoX.Api/Services/ITaskService.cs` with:
  - `Task<TaskEntity> CreateAsync(string? title)`
  - `Task<TaskEntity?> UpdateAsync(string id, TaskUpdate update)`, which returns null when not found
  - `Task<TaskEntity?> DeleteAsync(string id)`
  - the record `TaskUpdate(string? Title, string? Status, bool CompletedAtSpecified, DateTime? CompletedAt)`

  Create a stub `backend-dotnet/TodoX.Api/Services/TaskService.cs` (constructor takes `AppDbContext`, `TimeProvider`) whose methods throw `NotImplementedException`, and register it scoped in `backend-dotnet/TodoX.Api/Program.cs`. Then write `backend-dotnet/TodoX.Tests/Integration/TaskServiceTests.cs` (`[Collection("Postgres")]`, builds `TaskService` directly on the container DB):
  - `Create_TrimsTitle_BeforeSave`: `"  x  "` is stored as `"x"`
  - `Rename_TrimsTitle_BeforeSave`: update with Title `"  y  "` is stored as `"y"`
  - `Create_SetsDefaults`: `Status == "active"`, `CompletedAt == null`, `CreatedAt == UpdatedAt == fake now truncated to ms`, Kind `Utc`

  The tests must run red. Depends on T020, T023, T024.
- [ ] T026 [US1] Implement `TaskService.CreateAsync` in `backend-dotnet/TodoX.Api/Services/TaskService.cs`:
  - `title?.Trim()`; a null title is passed through as null so the DB `NOT NULL` constraint fails (DF-01)
  - `Status = "active"`, `CompletedAt = null`
  - `CreatedAt = UpdatedAt = DateTimeTruncation.TruncateToMilliseconds(timeProvider.GetUtcNow().UtcDateTime)`
  - `SaveChangesAsync`, and no catching of `DbUpdateException`

  `Create_TrimsTitle_BeforeSave` and `Create_SetsDefaults` turn green. Depends on T025.

### POST /api/tasks

- [ ] T027 [US1] **Test first**: write `backend-dotnet/TodoX.Tests/Integration/TasksControllerTests.Create.cs` (partial class):
  - `PostTask_ValidTitle_Returns201WithFullShape`: 201. Body has exactly the keys `_id, title, status, completedAt, createdAt, updatedAt, __v`. `_id` matches `^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$`. `status == "active"`, `completedAt` is JSON null (present, not omitted), `__v == 0`, `createdAt == updatedAt`, and both match the V-07 regex.
  - `PostTask_PaddedTitle_StoresTrimmed`: `"  x  "` returns title `"x"`, and a DB read confirms `"x"`.
  - `PostTask_EmptyTitle_Returns500`: `{ "title": "" }` returns 500 `{ "message": "Lỗi hệ thống" }`.
  - `PostTask_MissingTitle_Returns500`: `{}` returns 500 with the same body.
  - `PostTask_TabsAndNewlinesOnly_Returns500`: `{ "title": "\t\n" }` returns 500 with the same body.
  - `PostTask_ExtraFields_Ignored`: `{ "title": "a", "status": "complete" }` returns `status == "active"`.

  The tests must run red. Depends on T026.
- [ ] T028 [US1] Create `backend-dotnet/TodoX.Api/Controllers/TasksController.cs`: `[ApiController]`, `[Route("api/tasks")]`, `POST` takes `[FromBody] CreateTaskDto` and calls `CreateAsync(dto.Title)`. It returns `StatusCode(201, TaskResponseDto.FromEntity(...))` with no `CreatedAtAction` (the Location header is unnecessary). The controller has no try/catch; errors reach the global handler. T027 turns green. Depends on T027.

### PUT /api/tasks/{id}: validation, rename, status, 404, DF-02

- [ ] T029 [US1] **Test first**: write `backend-dotnet/TodoX.Tests/Integration/TasksControllerTests.Update.cs` (partial class):
  - `PutTask_ValidRename_Returns200`: the title is updated, `createdAt` is unchanged, and `updatedAt` equals the advanced fake clock and differs from `createdAt`.
  - `PutTask_PaddedTitle_StoresTrimmed`: `"  Padded task  "` becomes `"Padded task"`.
  - `PutTask_BlankTitle_Returns400`: `"   "` returns 400 `{ "message": "Tiêu đề nhiệm vụ không được để trống" }`.
  - `PutTask_EmptyStringTitle_Returns400`: `""` gives the same 400.
  - `PutTask_InvalidStatus_Returns400`: `"done"` returns 400 `{ "message": "Dữ liệu nhiệm vụ không hợp lệ" }`.
  - `PutTask_StatusComplete_Returns200`
  - `PutTask_NonExistentId_Returns404_NoExclamation`: a random well-formed UUID returns 404 `{ "message": "Nhiệm vụ không tồn tại" }`.
  - `PutTask_MalformedId_ValidTitle_Returns500`: `not-a-valid-uuid` returns 500 `{ "message": "Lỗi hệ thống" }` (DF-02).
  - `PutTask_MalformedId_BlankTitle_Returns400`: title validation runs before id parsing (research.md R-03).

  The tests must run red. Depends on T028.
- [ ] T030 [US1] Implement `TaskService.UpdateAsync` in `backend-dotnet/TodoX.Api/Services/TaskService.cs`:
  - `Guid.Parse(id)`, and let `FormatException` propagate (DF-02)
  - find by id and return `null` if missing
  - apply `Title.Trim()` if non-null and `Status` if non-null
  - apply `CompletedAt` only when `CompletedAtSpecified`
  - always set `UpdatedAt` to the truncated now; never touch `CreatedAt`
  - save and return the entity

  `Rename_TrimsTitle_BeforeSave` from T025 turns green. Depends on T029.
- [ ] T031 [US1] Add `PUT {id}` to `backend-dotnet/TodoX.Api/Controllers/TasksController.cs`. `id` is a raw `string` with **no** `:guid` constraint. Validation order:
  1. If `dto.Title != null && string.IsNullOrWhiteSpace(dto.Title)`, return 400 "Tiêu đề nhiệm vụ không được để trống".
  2. If `dto.Status != null` and it is not `"active"` or `"complete"`, return 400 "Dữ liệu nhiệm vụ không hợp lệ".
  3. Build a `TaskUpdate` (completedAt handling comes in T033; for now `CompletedAtSpecified = false`).
  4. Call the service. `null` returns 404 "Nhiệm vụ không tồn tại" (no `!`). Otherwise return 200 `TaskResponseDto`.

  T029 turns green. Depends on T030.

### PUT: absent vs null `completedAt` (FR-003, research.md R-01, R-11)

- [ ] T032 [US1] **Test first**: write `backend-dotnet/TodoX.Tests/Integration/TasksControllerTests.CompletedAt.cs` (partial class):
  - `PutTask_FrontendCompletePayload_PersistsUtc`: `{ "status": "complete", "completedAt": "2026-09-28T03:00:00.000Z" }` returns 200 with `completedAt` equal to that exact string. A DB read shows `Kind == Utc` and the same instant.
  - `PutTask_CompletedAtWithSubMs_TruncatedToMs`: `"2026-09-28T03:00:00.1239Z"` round-trips as `"2026-09-28T03:00:00.123Z"`.
  - `PutTask_ReopenPayload_ClearsCompletedAt`: complete first, then `{ "status": "active", "completedAt": null }` returns `completedAt` as JSON null, and the DB shows null.
  - `PutTask_RenameOnly_LeavesCompletedAtUnchanged`: complete with a non-null `completedAt` first, then `{ "title": "renamed" }`. `completedAt` still equals the earlier value. It must be non-null, so absent and null are really distinguished.
  - `PutTask_InvalidCompletedAt_Returns400_WithInvalidDataMessage`: `{ "status": "complete", "completedAt": "not-a-date" }` returns 400 `{ "message": "Dữ liệu nhiệm vụ không hợp lệ" }`, and the stored task is unchanged.

  The tests must run red. Depends on T031.
- [ ] T033 [US1] Implement three-state `completedAt` handling in `backend-dotnet/TodoX.Api/Controllers/TasksController.cs` (PUT action), after title/status validation and before the service call:
  - `dto.CompletedAt.HasValue == false`: `CompletedAtSpecified = false`
  - `ValueKind == Null`: specified, value `null`
  - `ValueKind == String`: `GetDateTime()`, then `DateTimeTruncation.TruncateToMilliseconds`, then ensure `Kind == Utc`. A `FormatException` or `InvalidOperationException` returns 400 "Dữ liệu nhiệm vụ không hợp lệ".
  - any other `ValueKind` (number, bool, object): 400 "Dữ liệu nhiệm vụ không hợp lệ"

  T032 turns green. Depends on T032.

### DELETE /api/tasks/{id}

- [ ] T034 [US1] **Test first**: write `backend-dotnet/TodoX.Tests/Integration/TasksControllerTests.Delete.cs` (partial class):
  - `DeleteTask_Existing_Returns200WithDeletedTask`: the body is the full task shape with the same `_id`/`title`, and the row is gone in the DB.
  - `DeleteTask_Twice_Returns404_WithExclamation`: the second call returns 404 `{ "message": "Nhiệm vụ không tồn tại!" }` (DF-03, trailing `!`).
  - `DeleteTask_MalformedId_Returns500`: `not-a-valid-uuid` returns 500 `{ "message": "Lỗi hệ thống" }` (DF-02).

  The tests must run red. Depends on T031.
- [ ] T035 [US1] Implement `TaskService.DeleteAsync` in `backend-dotnet/TodoX.Api/Services/TaskService.cs` (`Guid.Parse` throws when malformed; find, remove, save, return the removed entity or `null`) and add `DELETE {id}` to `backend-dotnet/TodoX.Api/Controllers/TasksController.cs` (raw `string id`; `null` returns 404 "Nhiệm vụ không tồn tại!"; otherwise 200 `TaskResponseDto`). T034 turns green. Depends on T034.

**Checkpoint**: US1 is complete. All `TaskServiceTests` and `TasksControllerTests.{Create,Update,CompletedAt,Delete}` pass. Quickstart V-01 through V-05 and V-07 pass via curl.

---

## Phase 4: User Story 2 - Browse Tasks with Date and Status Filters (Priority: P2)

**Goal**: `GET /api/tasks` with `dateQuery`, `filter`, `page`, `limit`, the active-first then newest-first sort, `totalCount`, and `totalPages`.

**Independent Test**: The `DateRangeCalculatorTests`, `StatusMappingTests`, and `PaginationTests` unit tests and `TasksControllerTests.List.cs` pass. Tasks are seeded with a controlled clock across previous month, this month, this week, and today, and every filter/page combination returns exactly the expected slice. Count badges are deferred to US3: until then `activeCount`/`completeCount` are placeholder `0`.

### Date range calculator (api-contract §4.1, FR-009, FR-014)

- [ ] T036 [P] [US2] **Test first**: write `backend-dotnet/TodoX.Tests/Unit/DateRangeCalculatorTests.cs`, using `FakeTimeProvider` and the zone from `DateRangeCalculator.ResolveTimeZone(null)`:
  - `TimeZone_AsiaHoChiMinh_Resolves`: `FindSystemTimeZoneById("Asia/Ho_Chi_Minh")` does not throw. This runs first. Environment sanity check - exempt from the red/Constitution IV requirement (it verifies runtime tzdata, not project code).
  - `ResolveTimeZone_UsesTzEnvValue_WhenSet`: `ResolveTimeZone("UTC")` returns the UTC zone.
  - `ResolveTimeZone_DefaultsToHcm_WhenNullOrEmpty`
  - `Today_StartsAtMidnightHcm`: now `2026-09-30T05:00:00Z` (Wed 12:00 ICT) gives start `2026-09-29T17:00:00Z`.
  - `Today_JustAfterLocalMidnight`: now `2026-09-27T17:30:00Z` (Mon 00:30 ICT) gives `2026-09-27T17:00:00Z`.
  - `Week_StartsOnMondayHcm`: now `2026-09-30T05:00:00Z` gives `2026-09-27T17:00:00Z` (Mon 2026-09-28 00:00 ICT).
  - `Week_OnSunday_GoesBackToMonday`: now `2026-10-04T05:00:00Z` (Sun) gives `2026-09-27T17:00:00Z`.
  - `Month_StartsOnFirstOfMonth`: now `2026-09-30T05:00:00Z` gives `2026-08-31T17:00:00Z`.
  - `All_ReturnsNullStartDate`: theory over `"all"`, `null`, `""`, `"foo"`, `"weeks"`, `"TODAY"` (case-sensitive); each returns `null`.

  Returned values are UTC `DateTime`. Add a stub `backend-dotnet/TodoX.Api/Infrastructure/DateRangeCalculator.cs` (constructor `(TimeProvider, TimeZoneInfo)`, method `DateTime? GetStartDate(string? dateQuery)`, static `TimeZoneInfo ResolveTimeZone(string? tzEnv)`) that throws. The tests must run red.
- [ ] T037 [US2] Implement `backend-dotnet/TodoX.Api/Infrastructure/DateRangeCalculator.cs`: convert now to the zone's local time; `today` is local midnight; `week` is local midnight minus `((int)DayOfWeek + 6) % 7` days; `month` is day 1 local midnight; convert back to UTC. Register it in `backend-dotnet/TodoX.Api/Program.cs`: `TimeZoneInfo` singleton from `ResolveTimeZone(Environment.GetEnvironmentVariable("TZ"))`, resolved once at startup, plus `DateRangeCalculator` as a singleton. T036 turns green. Depends on T036.

### Status filter mapping (api-contract §4.2, FR-010)

- [ ] T038 [P] [US2] **Test first**: write `backend-dotnet/TodoX.Tests/Unit/StatusMappingTests.cs` for `StatusFilter.ToStatus(string? filter)`, which returns `string?`:
  - `Active_MapsToActive`: `"active"` gives `"active"`
  - `Completed_MapsToComplete`: `"completed"` gives `"complete"`
  - `All_ReturnsNoStatusCondition`: `"all"`, `null`, `""`, and `"foo"` give `null`
  - `RawComplete_TreatedAsAll`: `"complete"` gives `null`
  - `CaseSensitive`: `"Active"` and `"COMPLETED"` give `null`

  Add a stub `backend-dotnet/TodoX.Api/Services/StatusFilter.cs` that throws. The tests must run red.
- [ ] T039 [US2] Implement `backend-dotnet/TodoX.Api/Services/StatusFilter.cs`. T038 turns green. Depends on T038.

### Pagination (api-contract §4.3, FR-011; parity with JS `parseInt` and `||` fallback)

- [ ] T040 [P] [US2] **Test first**: write `backend-dotnet/TodoX.Tests/Unit/PaginationTests.cs` for `Pagination.ParsePage(string?)`, `Pagination.ParseLimit(string?)`, `Pagination.TotalPages(int totalCount, int limit)`, and `Pagination.Skip(int page, int limit)`. The rules mirror `Math.max(1, parseInt(page) || 1)` and `Math.min(50, Math.max(1, parseInt(limit) || 5))`:
  - `Page_Defaults_And_MinClamp`: `null`, `""`, `"abc"`, `"0"`, `"-5"` give 1; `"3"` gives 3; `"2abc"` gives 2 and `" 4"` gives 4 (parseInt reads the leading integer); `"1.9"` gives 1.
  - `Page_AboveTotal_NotClamped`: `"9999"` gives 9999.
  - `Limit_Clamped_To_Range`: `null` and `"abc"` give 5; `"0"` gives **5** (preserved quirk DF-05, `docs/api-contract.md` §7: JS `0 || 5`); `"-3"` gives 1; `"1000"` gives 50; `"51"` gives 50; `"50"` gives 50; `"7"` gives 7.
  - `TotalPages_AtLeastOne`: (0,5) gives 1, (5,5) gives 1, (6,5) gives 2, (11,5) gives 3.
  - `Skip_IsPageMinusOneTimesLimit`: (3,5) gives 10.

  Add a stub `backend-dotnet/TodoX.Api/Services/Pagination.cs` that throws. The tests must run red.
- [ ] T041 [US2] Implement `backend-dotnet/TodoX.Api/Services/Pagination.cs` with a leading-integer parser (optional leading whitespace and sign, then digits; stop at the first non-digit; no digits means "NaN", which falls back). T040 turns green. Depends on T040.

### GET /api/tasks

- [ ] T042 [P] [US2] Create `backend-dotnet/TodoX.Api/DTOs/TaskListResponseDto.cs` with `tasks` (`TaskResponseDto[]`) and the ints `activeCount`, `completeCount`, `totalCount`, `totalPages`, `page`, `limit`, in that order (contracts/tasks-api.md).
- [ ] T043 [US2] **Test first**: write `backend-dotnet/TodoX.Tests/Integration/TasksControllerTests.List.cs` (partial class). Seed oldest first with a forward-only clock: tasks at `2026-08-15T03:00Z` (last month), `2026-09-10T03:00Z` (this month, earlier week), `2026-09-28T02:00Z` (this week, Monday), and `2026-09-30T01:00Z` / `2026-09-30T02:00Z` (today). Complete some of them via PUT, then set now to `2026-09-30T05:00Z`. Tests:
  - `GetTasks_DateQuery_Today`, `GetTasks_DateQuery_Week`, `GetTasks_DateQuery_Month`: each returns exactly the expected `_id` set.
  - `GetTasks_DateQuery_All`: `all`, absent, and `foo` return everything.
  - `GetTasks_Filter_Active`, `GetTasks_Filter_Completed`
  - `GetTasks_Filter_All`: `all`, absent, and `complete` return every status.
  - `GetTasks_Sort_ActiveFirst`
  - `GetTasks_Sort_NewestFirstWithinGroup`
  - `GetTasks_Pagination_SecondPageSlice`: 7 tasks with `limit=5`; page 2 holds the last 2 in sort order.
  - `GetTasks_PageZero_TreatedAsPage1`
  - `GetTasks_NonNumericPageAndLimit_Returns200WithDefaults`: `page=abc&limit=xyz` returns 200, `page == 1`, `limit == 5`. This must not be an automatic 400 from model binding.
  - `GetTasks_LimitClampedAndEchoed`: `limit=1000` returns `limit == 50`.
  - `GetTasks_PageBeyondTotal_EchoesFarPage`: `page=9999` returns `tasks == []`, `page == 9999`, `totalPages >= 1`.
  - `GetTasks_EmptyDb_TotalPagesIsOne`
  - `GetTasks_TotalCount_RespectsBothFilters`: `dateQuery=week&filter=active` gives a `totalCount` equal to that intersection.
  - `GetTasks_ResponseShape`: all seven top-level keys are present.

  The tests must run red. Depends on T037, T039, T041, T042.
- [ ] T044 [US2] Add `Task<TaskListResult> GetTasksAsync(string? dateQuery, string? filter, string? page, string? limit)` to `backend-dotnet/TodoX.Api/Services/ITaskService.cs` and implement it in `backend-dotnet/TodoX.Api/Services/TaskService.cs` (inject `DateRangeCalculator`). Define `record TaskListResult(List<TaskEntity> Tasks, int TotalCount, int ActiveCount, int CompleteCount, int TotalPages, int Page, int Limit)` in `backend-dotnet/TodoX.Api/Services/ITaskService.cs`. Queries follow research.md R-07 and are **awaited sequentially**, never with `Task.WhenAll`:
  - `base` = `Tasks` filtered by `CreatedAt >= start` when the start date is non-null; there is no end bound
  - `filtered` = `base` filtered by `Status == mapped` when the mapped status is non-null
  - page = `filtered.OrderBy(t => t.Status == "active" ? 0 : 1).ThenByDescending(t => t.CreatedAt).Skip(skip).Take(limit)`
  - `totalCount = filtered.CountAsync()`
  - `totalPages = Pagination.TotalPages(...)`

  For now set `activeCount` and `completeCount` to the placeholder `0` with a `// US3` comment. Depends on T043.
- [ ] T045 [US2] Add `GET` to `backend-dotnet/TodoX.Api/Controllers/TasksController.cs`, binding `[FromQuery] string? dateQuery, string? filter, string? page, string? limit` as **strings**. Typed `int` binding would make `[ApiController]` return 400 for `page=abc`. Map the result to `TaskListResponseDto` and return 200. T043 turns green. Depends on T044.

**Checkpoint**: US1 and US2 tests pass. The frontend can list, filter, and paginate, but the badges show 0 until US3.

---

## Phase 5: User Story 3 - View Task Counts per Date Range (Priority: P3)

**Goal**: `activeCount`/`completeCount` reflect the `dateQuery` range only, whatever `filter` is set to (api-contract §4.5, FR-012).

**Independent Test**: `TasksControllerTests.Counts.cs` passes. With 3 active and 5 complete tasks in range, switching `filter` never changes the badges.

- [ ] T046 [US3] **Test first**: write `backend-dotnet/TodoX.Tests/Integration/TasksControllerTests.Counts.cs` (partial class), seeding with the forward-only clock:
  - `GetTasks_Counts_IndependentOfFilter`: 3 active and 5 complete today. For `filter` in `all`, `active`, `completed`, `foo`, every response has `activeCount == 3` and `completeCount == 5`.
  - `GetTasks_Counts_RespectDateQuery`: tasks older than today are excluded from the counts under `dateQuery=today` and included under `dateQuery=all`.
  - `GetTasks_Counts_AllRange_IncludesEverything`
  - `GetTasks_Counts_ZeroInOneRange_NonZeroInAnother`: seed active and complete tasks only outside "today". Assert `activeCount == 0` and `completeCount == 0` for `dateQuery=today`, and both non-zero for `dateQuery=all`.

  The tests must run red, because T044 returns the placeholder `0`. Depends on T045.
- [ ] T047 [US3] Replace the placeholder in `backend-dotnet/TodoX.Api/Services/TaskService.cs` `GetTasksAsync` with `activeCount = await base.CountAsync(t => t.Status == "active")` and `completeCount = await base.CountAsync(t => t.Status == "complete")`. Both run on the **date-filtered base**, not `filtered`, awaited sequentially after the page and total queries. T046 turns green. Depends on T046.

**Checkpoint**: All three stories are complete, and every Principle IV named test from the plan.md Test Plan exists and passes.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T048 [P] Add `backend-dotnet/TodoX.Tests/Integration/PerformanceTests.cs` (`[Trait("Category", "Performance")]`, `[Collection("Postgres")]`): bulk-insert 10,000 tasks with distinct `CreatedAt` through `AppDbContext`, warm up once, then assert `GET /api/tasks?dateQuery=all&filter=all&page=1&limit=5` completes in under 1 s (SC-005). If it fails, raise a plan amendment (for example an index on `(Status, CreatedAt)` via a new migration) before changing the schema.
- [ ] T049 [P] Write `backend-dotnet/README.md` covering prerequisites; the two-command bootstrap with no copy step (`docker compose up -d` and `dotnet run --project TodoX.Api`, both in `backend-dotnet/`); `dotnet test` and `dotnet test --filter "Category=Unit"`; the `TZ` env override; the Linux `tzdata` requirement (research.md R-05); the local Postgres trust-auth note (dev only, localhost-only bind); and the list of preserved quirks DF-01 to DF-05.
- [ ] T050 [P] Fix the bootstrap step in `specs/001-dotnet-backend-rewrite/quickstart.md`: Step 1 says to run `docker compose up -d` from the repo root, but the compose file is at `backend-dotnet/docker-compose.yml` (Principle I). Change it to run in `backend-dotnet/`. No config-copy step is needed (trust auth, committed `appsettings.Development.json`). This is a docs-only change and is allowed under Principle I.
- [ ] T051 Run `dotnet test backend-dotnet/TodoX.sln` (Docker running): everything must be green. Run `dotnet test backend-dotnet/TodoX.sln --filter "Category=Unit"` with Docker stopped: it must pass, proving the unit tests have no container dependency. Record the results in the PR description.
- [ ] T052 Manual API validation: in `backend-dotnet/`, run `docker compose up -d` and then `dotnet run --project TodoX.Api`, and execute quickstart.md V-01 through V-07 with curl against `http://localhost:5001`, including the health smoke check. Every status code and body must match. Log any mismatch as a new task; do not patch it silently.
- [ ] T053 **Frontend end-to-end check (quickstart V-08, SC-001)**:
  1. Make sure the Node backend (`backend/`) is **not** running, because it also serves port 5001.
  2. With the .NET API running from T052, start the unchanged frontend: `cd frontend && npm run dev`, then open `http://localhost:5173`.
  3. Verify in the browser: create; complete (the task moves below the active ones); reopen (it moves back above); rename; delete; the date filter (today / this week / this month / all); the status filter (all / active / completed); badge counts staying stable across status-filter switches; the pagination arrows; and no CORS errors in the DevTools console.
  4. Confirm that `git status frontend/ backend/` shows no changes.
- [ ] T054 Final compliance audit:
  - (a) `git diff main --stat -- frontend backend` is empty (Principle I).
  - (b) `dotnet list backend-dotnet/TodoX.sln package` shows exactly the 8 approved packages (Constitution V).
  - (c) Grep the tracked files under `backend-dotnet/` for `Password=`, `POSTGRES_PASSWORD`, and literal passwords; there must be none. Confirm local Postgres needs no password: `docker-compose.yml` sets `POSTGRES_HOST_AUTH_METHOD=trust`, binds only `127.0.0.1:5432`, and carries the dev-only comment, and the committed `appsettings.Development.json` connection string has no `Password`.
  - (d) Every test name in the plan.md Test Plan table exists in the solution.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (T001–T007)**: T002 must run before T003 and T004. T005, T006, and T007 are [P] once T002 exists.
- **Foundational (T008–T022)**: depends on Setup and blocks all stories.
  - Chain T008 → T009 → T010 (T009 includes the `AddDbContext` registration T010 needs).
  - Pairs T011→T012, T013→T014, T015→T016; the three pairs can run in parallel with each other.
  - T018 needs T012 and T016. T019 needs T010 and T018. T020 needs T019. Then T021 → T022.
- **US1 (T023–T035)**: depends on Foundational.
- **US2 (T036–T045)**: depends on Foundational. The unit-rule pairs (T036–T041) and T042 can start in parallel with US1. T043–T045 need `TasksController.cs` and `TaskService.cs` to exist (T028, T031), so in practice they start after US1's PUT work.
- **US3 (T046–T047)**: depends on US2's GET endpoint (T045).
- **Polish (T048–T054)**: depends on all stories. T048, T049, and T050 are [P]. T051 → T052 → T053 → T054 run in order.

### User story dependencies

- **US1 (P1)**: independent after Foundational. This is the MVP.
- **US2 (P2)**: its pure rules (date, status, pagination) are independent. Its GET endpoint adds to the controller and service files US1 creates.
- **US3 (P3)**: extends US2's `GetTasksAsync`, so it cannot ship without US2.

### Within each story

- The test task is committed red before its implementation task, with no exceptions for §4 rules.
- DTOs, then service, then controller.
- `TasksController.cs` and `TaskService.cs` are edited by several tasks. Those tasks are strictly sequential.

---

## Parallel Examples

### Foundational

```text
Task: T011 MillisecondDateTimeConverterTests (+stub)   → then T012
Task: T013 DateTimeTruncationTests (+stub)             → then T014
Task: T015 GlobalExceptionHandlerTests (+stub)         → then T016
Task: T017 TaskResponseDto
```

### User Story 1

```text
Task: T023 CreateTaskDto.cs
Task: T024 UpdateTaskDto.cs
```

### User Story 2 (the pure-rule pairs can also overlap with US1)

```text
Task: T036 DateRangeCalculatorTests (+stub)   → then T037
Task: T038 StatusMappingTests (+stub)         → then T039
Task: T040 PaginationTests (+stub)            → then T041
Task: T042 TaskListResponseDto.cs
```

### Polish

```text
Task: T048 PerformanceTests.cs
Task: T049 backend-dotnet/README.md
Task: T050 quickstart.md bootstrap fix
```

---

## Implementation Strategy

### MVP first (User Story 1 only)

1. Phase 1 Setup, then Phase 2 Foundational: health is green and `dotnet test` passes.
2. Phase 3 (US1): create, rename, complete, reopen, and delete work via curl (quickstart V-01 to V-05, V-07).
3. **Stop and validate.** The frontend cannot run end to end yet, because it needs `GET /api/tasks` from US2.

### Incremental delivery

1. Add US2: the frontend lists, filters, and paginates, with badges at 0. This is the first point where a partial frontend check is possible.
2. Add US3: badges are correct. Run the full V-08 frontend end-to-end check (T053).
3. Polish: performance, docs, and the compliance audit.

**Policy**: the `dotnet-backend` branch is not merged into `main` until T053 (frontend end-to-end check) passes. Commits to `dotnet-backend` during US1/US2 are normal feature-branch commits, not merges, so Principle III's pre-merge frontend check is not yet triggered.

---

## Notes

- A [P] task touches different files and has no dependency on an incomplete task.
- Every "Test first" task must be committed while its tests are red. Its paired implementation task is the commit that turns them green.
- Do not add NuGet packages beyond the 8 in plan.md. That requires a plan amendment first (Constitution V).
- Any behavior change beyond §6 deviations and §7 deferred fixes must first be recorded in `docs/api-contract.md` §6 (Constitution III).
