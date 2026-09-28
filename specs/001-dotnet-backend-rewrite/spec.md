# Feature Specification: .NET Backend Rewrite

**Feature Branch**: `dotnet-backend`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "Rewrite the todoX backend so the existing React frontend keeps working unchanged. Users can create, rename, complete/reopen and delete tasks; list tasks filtered by creation date (today / this week starting Monday / this month / all) and by status (all / active / completed), with pagination; and see active/completed counts for the selected date range. Behavior must follow docs/api-contract.md. Serving the frontend build in production is out of scope."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Manage Individual Tasks (Priority: P1)

A user can create new tasks, rename existing tasks, mark tasks as complete or reopen them, and delete tasks they no longer need.

**Why this priority**: Task management is the core of the application — without it, no other feature delivers value.

**Independent Test**: Can be fully tested by performing create, rename, complete, reopen, and delete operations and verifying the task list reflects each change.

**Acceptance Scenarios**:

1. **Given** the task list, **When** the user submits a new task title, **Then** the task appears in the list with "active" status.
2. **Given** an existing active task, **When** the user renames it with a valid title, **Then** the task displays the updated title.
3. **Given** an existing active task, **When** the user marks it complete, **Then** its status changes to "complete" and it moves below active tasks.
4. **Given** a completed task, **When** the user reopens it, **Then** its status changes back to "active" and it moves above completed tasks.
5. **Given** an existing task, **When** the user deletes it, **Then** the task no longer appears in the list, and the deleted task's data is returned to the caller.
6. **Given** a rename request with a blank title, **When** the request is submitted, **Then** the system rejects it with a specific validation error message.
7. **Given** an update with an invalid status value, **When** the request is submitted, **Then** the system rejects it with a validation error message.

---

### User Story 2 - Browse Tasks with Date and Status Filters (Priority: P2)

A user can view tasks filtered by when they were created (today, this week starting Monday, this month, or all time) and by their status (all, active only, or completed only), with pagination to navigate large lists.

**Why this priority**: Filtering and pagination let users manage growing task lists without being overwhelmed; without them the application becomes unusable at scale.

**Independent Test**: Can be fully tested by creating tasks across multiple dates and statuses, applying each filter combination, and verifying only matching tasks appear on the correct page.

**Acceptance Scenarios**:

1. **Given** tasks created on multiple days, **When** the user selects "today", **Then** only tasks created on the current calendar day are shown.
2. **Given** tasks created across weeks, **When** the user selects "week", **Then** only tasks created since Monday of the current week appear.
3. **Given** tasks created across months, **When** the user selects "month", **Then** only tasks created this calendar month appear.
4. **Given** an unrecognized `dateQuery` value, **When** the request is made, **Then** no date filter is applied and all tasks are eligible.
5. **Given** mixed active and completed tasks, **When** the user filters by "active", **Then** only active tasks appear.
6. **Given** mixed active and completed tasks, **When** the user filters by "completed", **Then** only completed tasks appear.
7. **Given** an unrecognized `filter` value (including the raw DB value "complete"), **When** the request is made, **Then** no status filter is applied.
8. **Given** more tasks than fit on one page, **When** the user navigates to a later page, **Then** the correct slice of tasks is shown.
9. **Given** a `page` value beyond the last page, **When** the request is made, **Then** an empty task list is returned with correct pagination metadata (no redirect or clamp).
10. **Given** any combination of filters, **When** tasks are returned, **Then** active tasks always appear before completed tasks; within each group, newer tasks appear first.

---

### User Story 3 - View Task Counts per Date Range (Priority: P3)

A user can see how many active and completed tasks exist within the selected date range, regardless of which status filter is currently active.

**Why this priority**: The counts drive the tab badges in the UI and must remain stable when switching between status filters to avoid disorienting the user.

**Independent Test**: Can be fully tested by querying the task list while toggling between status filters and confirming the active/completed badge numbers do not change.

**Acceptance Scenarios**:

1. **Given** tasks in the "today" range, **When** the user is on the "active" filter, **Then** both `activeCount` and `completeCount` still reflect all tasks in the "today" range.
2. **Given** a date range with 3 active and 5 completed tasks, **When** the user switches to the "completed" filter, **Then** `activeCount` remains 3 and `completeCount` remains 5.
3. **Given** the "all" date range, **When** counts are retrieved, **Then** they reflect all tasks regardless of creation date.
4. **Given** any query, **When** `totalPages` is computed, **Then** it is at least 1 even when no tasks match.

---

### Edge Cases

- What happens when `page` is non-numeric or less than 1? → Treated as page 1.
- What happens when `limit` is outside [1, 50]? → Silently clamped to the nearest valid bound; the clamped value is echoed back.
- What happens when `dateQuery` is absent or an unrecognized string? → No date filter is applied.
- What happens when `filter` is absent, `"all"`, or any unrecognized value (including `"complete"`)? → No status filter is applied.
- What happens when a task ID in an update or delete request is malformed? → Returns 500 (preserved quirk; the id route parameter MUST be accepted as a raw string and passed through to the database layer so a malformed value produces a system error rather than an automatic 400 from a typed route binder).
- What happens when `title` is absent or blank in a create request? → Returns 500 (preserved quirk; consistent with current backend behavior).
- What happens when a requested task does not exist? → Update returns 404 with message `"Nhiệm vụ không tồn tại"`; delete returns 404 with message `"Nhiệm vụ không tồn tại!"` (trailing exclamation mark).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST expose a health-check endpoint at `/api/health` that returns `{ "status": "ok", "time": "<ISO timestamp>" }` with status 200.
- **FR-002**: The system MUST allow a task to be created by supplying a title; the new task defaults to "active" status with a null completion timestamp, and is returned in full on success (status 201).
- **FR-003**: The system MUST allow an existing task's title, status, and completion timestamp to be updated; only fields present in the request body are applied. An explicit null for completedAt MUST set the stored value to null; an absent field MUST leave it unchanged.
- **FR-004**: An update request supplying a blank or whitespace-only title MUST be rejected with status 400 and message `"Tiêu đề nhiệm vụ không được để trống"`.
- **FR-005**: An update request supplying a status value outside the allowed set MUST be rejected with status 400 and message `"Dữ liệu nhiệm vụ không hợp lệ"`.
- **FR-006**: An update or delete for a non-existent task ID MUST return status 404; the message for update is `"Nhiệm vụ không tồn tại"` and for delete is `"Nhiệm vụ không tồn tại!"`.
- **FR-007**: The system MUST allow an existing task to be deleted; on success the deleted task object is returned with status 200.
- **FR-008**: The system MUST return a paginated task list; active tasks MUST appear before completed tasks, and within each group tasks MUST be ordered newest-first by creation time.
- **FR-009**: The task list MUST support date-range filtering via `dateQuery`: `today` (from 00:00 today), `week` (from 00:00 Monday of the current week), `month` (from 00:00 the 1st of the current month); any other value or absence of the parameter returns all tasks.
- **FR-010**: The task list MUST support status filtering via `filter`: `active` returns only active tasks; `completed` returns only tasks with status `complete`; any other value or absence returns all statuses.
- **FR-011**: `page` MUST default to 1 and be clamped to a minimum of 1; `limit` MUST default to 5 and be clamped to [1, 50]; both clamped values MUST be echoed in the response. A `page` above the last page is echoed unchanged; only the minimum of 1 applies.
- **FR-012**: The task list response MUST include `totalCount` (tasks matching both date and status filters), `totalPages` (≥ 1), `activeCount` (active tasks in the date range regardless of `filter`), `completeCount` (completed tasks in the date range regardless of `filter`), `page`, and `limit`.
- **FR-013**: Task objects in all responses MUST include fields `_id`, `title`, `status`, `completedAt`, `createdAt`, `updatedAt`, and `__v` with the exact names and semantics defined in `docs/api-contract.md` §2. `_id` MUST be a UUID serialized as a lowercase hyphenated string (the frontend treats it as an opaque string; no 24-hex format is required). `__v` MUST always be `0` (the frontend does not read this field; it is present for wire-format compatibility only).
- **FR-014**: Date-range boundaries MUST be calculated using the `Asia/Ho_Chi_Minh` timezone (or the value of the `TZ` environment variable if set); the start of `week` is Monday.
- **FR-015**: All unhandled system errors MUST return status 500 with body `{ "message": "Lỗi hệ thống" }`.
- **FR-016**: The API MUST serve all endpoints under the `/api` prefix and MUST allow cross-origin requests from `http://localhost:5173` in non-production environments.

### Key Entities

- **Task**: Represents a to-do item. Attributes: unique identifier (`_id`, UUID string), title (text, required, trimmed), status (enum: `"active"` or `"complete"`, default `"active"`), completion timestamp (`completedAt`, nullable, client-managed), creation timestamp (`createdAt`, system-managed), last-update timestamp (`updatedAt`, system-managed), version key (`__v`, always `0`).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The existing React frontend operates against the new backend without any frontend code changes — all user flows (create, rename, complete, reopen, delete, filter, paginate) work end-to-end.
- **SC-002**: Every business rule in `docs/api-contract.md` §4 is covered by an automated test that fails against an empty implementation before the implementation is written.
- **SC-003**: A fresh developer environment can reach a working health-check response by running two commands (start infrastructure + start the server).
- **SC-004**: All automated tests pass before any feature change is committed to the main line.
- **SC-005**: The task list response returns in under 1 second for a dataset of up to 10,000 tasks under normal single-user load.

## Assumptions

- Authentication and authorization are out of scope; the API is public within the local development environment.
- Serving the React frontend static build from the new backend is explicitly out of scope.
- The database is provisioned locally for development; production deployment configuration is out of scope. The API is served at `http://localhost:5001` in development — confirmed against `frontend/src/lib/axios.js:7`; in production the frontend calls `/api` (relative path), which is outside the scope of this rewrite.
- `completedAt` is entirely client-managed; the backend does not automatically set or validate it against `status`.
- The behavioral quirks documented in `docs/api-contract.md` §5 (e.g., POST returning 500 for blank titles, malformed ObjectId returning 500) are preserved as-is to maintain frontend compatibility; intentional deviations require prior authorization recorded in `docs/api-contract.md` §6.
- All timestamps (`createdAt`, `updatedAt`, `completedAt`, and `time` in `/api/health`) are serialized as ISO 8601 UTC with exactly three fractional digits and a trailing Z, e.g. `2026-09-26T02:15:00.000Z`, matching the original backend.
- No rate limiting is required.
- The `week` boundary uses Monday as the start of week, consistent with Vietnamese locale convention used by the original backend.

## Clarifications

### Session 2026-09-28

- Q: Are the §5 behavioral quirks (POST blank title → 500, malformed id → 500, differing 404 messages) preserved or fixed? → A: Preserved. Id route parameters MUST be accepted as raw strings so malformed ids still produce 500 rather than an automatic 400/404. These are tracked for future remediation in `docs/api-contract.md` §7.
- Q: What identifier type replaces MongoDB ObjectId for `_id` in PostgreSQL? → A: UUID serialized as a lowercase hyphenated string. Frontend search confirmed `_id` is used only as an opaque string in URL segments and React keys; no 24-hex format check exists; no frontend changes required.
- Q: Is returning constant `0` for `__v` sufficient given frontend usage? → A: Yes. Frontend search found no reads of `__v` in `frontend/src`; the field is present for structural wire compatibility only.
- Q: Does the frontend's configured API base URL match `http://localhost:5001`? → A: Yes. `frontend/src/lib/axios.js:7` hard-codes `http://localhost:5001/api` for development. No mismatch.
- Q: How is unstable sort order for tasks sharing the same `createdAt` handled? → A: Deferred. Recorded in `docs/api-contract.md` §7 as a latent issue. Tests MUST use distinct `createdAt` values to ensure deterministic ordering. Because time is injected in tests, a fake clock MUST be advanced between task creations, otherwise all tasks share one timestamp.
- Q: How does the frontend call PUT? → A: Complete sends `{ status: "complete", completedAt: <ISO string> }`; reopen sends `{ status: "active", completedAt: null }` (explicit null); rename sends only `{ title }`. (`TaskCard.jsx:35–37, 64–75`)
- Q: Does the frontend depend on error status codes or messages? → A: No. Every catch shows a hard-coded toast and never reads `error.response`. It also blocks blank titles before sending (`AddTask.jsx:13`, `TaskCard.jsx:32`).
- Q: Which GET /tasks fields does the frontend rely on? → A: `tasks`, `activeCount`, `completeCount`, `totalPages`, `page`; it always sends `limit=5`. If the response `page` exceeds `totalPages` it sets its own page to `totalPages`, so the server MUST echo the requested `page` unclamped and `totalPages` MUST be ≥ 1. `totalCount` and `limit` are ignored by the frontend.
