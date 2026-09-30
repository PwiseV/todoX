# Quickstart Validation Guide: .NET Backend Rewrite

**Branch**: `dotnet-backend` | **Date**: 2026-09-28

This guide describes how to bootstrap the backend and verify the primary acceptance criteria end-to-end. It is a validation guide, not an implementation recipe.

---

## Prerequisites

- Docker Desktop (or Docker Engine + Compose plugin) running
- .NET 10 SDK installed (`dotnet --version` should show `10.x`)
- Node.js (for running the existing frontend) — optional for the SC-001 manual check
- Working directory: repo root

---

## Bootstrap (SC-003: Two-command startup)

**Step 1 — Start PostgreSQL**:

```
docker compose up -d
```

Expected: PostgreSQL 16 container starts, port 5432 accessible on localhost.

**Step 2 — Start the API**:

```
cd backend-dotnet
dotnet run --project TodoX.Api
```

Expected: Kestrel binds to `http://localhost:5001`. Migrations apply automatically on startup (dev mode). Console shows startup logs.

---

## Smoke Check — Health Endpoint (FR-001)

```
curl http://localhost:5001/api/health
```

Expected response:
```json
HTTP 200
{ "status": "ok", "time": "2026-09-28T02:15:00.000Z" }
```

Verify: `time` has exactly 3 fractional digits and trailing Z.

---

## Validation Scenarios

### V-01: Create a Task (FR-002)

```
POST http://localhost:5001/api/tasks
{ "title": "Test task" }
```

Expected:
- HTTP 201
- Body contains `_id` (UUID format: `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`), `status: "active"`, `completedAt: null`, `__v: 0`
- `createdAt` and `updatedAt` equal, 3 fractional digits, trailing Z

Save the returned `_id` value as `TASK_ID` for subsequent tests.

---

### V-02: Rename a Task (FR-003, FR-004)

**Valid rename**:
```
PUT http://localhost:5001/api/tasks/{TASK_ID}
{ "title": "Renamed task" }
```
Expected: HTTP 200; `title` updated; `completedAt` unchanged (null); `updatedAt` ≠ `createdAt`.

**Padded title trims on save**:
```
PUT http://localhost:5001/api/tasks/{TASK_ID}
{ "title": "  Padded task  " }
```
Expected: HTTP 200; response `title` is exactly `"Padded task"` (leading/trailing spaces stripped).

**Blank title**:
```
PUT http://localhost:5001/api/tasks/{TASK_ID}
{ "title": "   " }
```
Expected: HTTP 400; `{ "message": "Tiêu đề nhiệm vụ không được để trống" }`.

**Tab/newline-only title on POST → 500 (DF-01)**:
```
POST http://localhost:5001/api/tasks
{ "title": "\t\n" }
```
Expected: HTTP 500; `{ "message": "Lỗi hệ thống" }` — trims to `""`, fails the `CHECK (trim("Title") <> '')` constraint, `DbUpdateException` → global handler.

---

### V-03: Complete and Reopen a Task (FR-003, FR-013, partial-PUT)

**Complete**:
```
PUT http://localhost:5001/api/tasks/{TASK_ID}
{ "status": "complete", "completedAt": "2026-09-28T03:00:00.000Z" }
```
Expected: HTTP 200; `status: "complete"`; `completedAt: "2026-09-28T03:00:00.000Z"`.

**Reopen** (explicit null for completedAt):
```
PUT http://localhost:5001/api/tasks/{TASK_ID}
{ "status": "active", "completedAt": null }
```
Expected: HTTP 200; `status: "active"`; `completedAt: null`.

**Rename only** (completedAt must survive unchanged):
```
PUT http://localhost:5001/api/tasks/{TASK_ID}
{ "title": "New name" }
```
Expected: HTTP 200; `completedAt` unchanged (still null from reopen above).

**Invalid completedAt string → 400 (api-contract §5.8)**:
```
PUT http://localhost:5001/api/tasks/{TASK_ID}
{ "status": "complete", "completedAt": "not-a-date" }
```
Expected: HTTP 400; `{ "message": "Dữ liệu nhiệm vụ không hợp lệ" }` — `JsonElement.GetDateTimeOffset()` throws `FormatException`, caught in the controller.

---

### V-04: Delete a Task (FR-007, DF-03)

```
DELETE http://localhost:5001/api/tasks/{TASK_ID}
```
Expected: HTTP 200; body is the full deleted task object.

**Delete again** (404 with exclamation mark):
```
DELETE http://localhost:5001/api/tasks/{TASK_ID}
```
Expected: HTTP 404; `{ "message": "Nhiệm vụ không tồn tại!" }` (note trailing `!`).

**PUT non-existent** (404 without exclamation mark):
```
PUT http://localhost:5001/api/tasks/{TASK_ID}
{ "title": "x" }
```
Expected: HTTP 404; `{ "message": "Nhiệm vụ không tồn tại" }` (no trailing `!`).

---

### V-05: Preserved Quirks (DF-01, DF-02)

**DF-01 — POST blank title → 500**:
```
POST http://localhost:5001/api/tasks
{ "title": "" }
```
Expected: HTTP 500; `{ "message": "Lỗi hệ thống" }`.

**DF-01 — POST null title → 500**:
```
POST http://localhost:5001/api/tasks
{}
```
Expected: HTTP 500; `{ "message": "Lỗi hệ thống" }`.

**DF-02 — Malformed id → 500**:
```
PUT http://localhost:5001/api/tasks/not-a-valid-uuid
{ "title": "x" }
```
Expected: HTTP 500; `{ "message": "Lỗi hệ thống" }`.

---

### V-06: Task List — Filters, Pagination, and Counts (FR-008 – FR-012)

Seed at least 6 tasks across different dates and statuses before running these checks. See `test plan` in `plan.md` for the exact seeding pattern used in integration tests.

**All tasks**:
```
GET http://localhost:5001/api/tasks?dateQuery=all&filter=all&page=1&limit=5
```
Verify: `totalPages >= 1`, `activeCount + completeCount = totalCount` only when `filter=all`.

**Active-only filter**:
```
GET http://localhost:5001/api/tasks?filter=active
```
Verify: all returned `tasks` have `status: "active"`; `activeCount` and `completeCount` are unchanged from the all-filter request.

**Counts are filter-independent**:
- Compare `activeCount` from `?filter=active` vs `?filter=completed` — must be identical.
- Compare `completeCount` similarly — must be identical.

**Sort order**:
Verify first task in response has `status: "active"` when both statuses exist.

**Page beyond total**:
```
GET http://localhost:5001/api/tasks?page=9999
```
Verify: `tasks: []`, `totalPages >= 1`, `page: 9999` (echoed unchanged).

---

### V-07: Timestamp Format (spec Assumptions)

Inspect any `createdAt`, `updatedAt`, `completedAt`, or `/api/health` `time` value.

Verify: matches regex `^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$` — exactly 3 fractional digits, trailing Z, no offset.

---

### V-08: Frontend End-to-End (SC-001)

1. Start the React frontend: `cd frontend && npm run dev` (Vite at `http://localhost:5173`).
2. Open `http://localhost:5173` in a browser.
3. Verify:
   - Creating a task adds it to the list.
   - Completing a task moves it below active tasks.
   - Reopening a task moves it back above.
   - Renaming a task updates the title.
   - Deleting a task removes it from the list.
   - Date filter (today / this week / this month / all) narrows the list correctly.
   - Status filter (all / active / completed) narrows the list correctly.
   - Badge counts (active / completed) update correctly and remain stable when switching status filters.
   - Pagination arrows navigate between pages.

No frontend code changes should be required for any of the above to work.

---

## Automated Tests (SC-002, SC-004)

```
cd backend-dotnet
dotnet test
```

Expected: all tests pass. Docker must be running (Testcontainers spins up PostgreSQL automatically).

To run only unit tests (no Docker needed):
```
dotnet test --filter "Category=Unit"
```
