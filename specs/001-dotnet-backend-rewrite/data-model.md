# Data Model: .NET Backend Rewrite

**Branch**: `dotnet-backend` | **Date**: 2026-09-28

---

## Database Entity: Tasks

### PostgreSQL Table — `Tasks`

| Column | PG Type | Constraints | Notes |
|--------|---------|-------------|-------|
| `Id` | `uuid` | PRIMARY KEY, DEFAULT `gen_random_uuid()` | Stored as UUID; exposed as `_id` string on wire |
| `Title` | `text` | NOT NULL, CHECK (`trim("Title") <> ''`) | Required; whitespace-only rejected at DB level (DF-01 mechanism) |
| `Status` | `text` | NOT NULL, DEFAULT `'active'`, CHECK (`"Status" IN ('active', 'complete')`) | Two-value enum; enforced at DB level |
| `CompletedAt` | `timestamptz` | NULL allowed | Client-managed; server never auto-sets or validates against `Status` |
| `CreatedAt` | `timestamptz` | NOT NULL | Server-set on create; truncated to milliseconds before write |
| `UpdatedAt` | `timestamptz` | NOT NULL | Server-set on every save; truncated to milliseconds before write |

**Notes**:
- No `__v` column. The value `0` is a compile-time constant in the response DTO (§6 authorized deviation).
- No `title` uniqueness constraint (mirrors original Mongoose schema).
- PostgreSQL `timestamptz` stores UTC internally; .NET reads back as `DateTimeKind.Utc`.

---

## EF Core Entity

**File**: `TodoX.Api/Entities/TaskEntity.cs`

```
class TaskEntity
  Guid      Id           PK
  string    Title        required, non-blank (DB constraint)
  string    Status       "active" | "complete"; DB default "active"
  DateTime? CompletedAt  nullable; client-managed
  DateTime  CreatedAt    server-managed; UTC; millisecond precision
  DateTime  UpdatedAt    server-managed; UTC; millisecond precision
```

**EF Core fluent configuration** (`AppDbContext.OnModelCreating`):
- `ToTable("Tasks")`
- `Id`: `HasDefaultValueSql("gen_random_uuid()")`
- `Title`: `.IsRequired()` + `HasCheckConstraint("CK_Tasks_Title_NotBlank", "trim(\"Title\") <> ''")`
- `Status`: `.IsRequired().HasDefaultValue("active")` + `HasCheckConstraint("CK_Tasks_Status_Enum", "\"Status\" IN ('active', 'complete')")`
- `CreatedAt`, `UpdatedAt`: `.IsRequired()`

---

## Wire Format DTOs

### `TaskResponseDto` — used by all endpoints (GET list, POST 201, PUT 200, DELETE 200)

Maps `TaskEntity` to the exact JSON shape required by `docs/api-contract.md` §2.

| JSON field | C# property name | Value source | Attribute |
|-----------|-----------------|--------------|-----------|
| `_id` | `Id` | `entity.Id.ToString()` (lowercase hyphenated UUID) | `[JsonPropertyName("_id")]` |
| `title` | `Title` | direct string | — |
| `status` | `Status` | `"active"` or `"complete"` | — |
| `completedAt` | `CompletedAt` | `DateTime?`; null serialized as JSON null | — |
| `createdAt` | `CreatedAt` | UTC DateTime; ISO 8601 3-frac converter | — |
| `updatedAt` | `UpdatedAt` | UTC DateTime; ISO 8601 3-frac converter | — |
| `__v` | `V` | compile-time constant `0` | `[JsonPropertyName("__v")]` |

All `DateTime` fields pass through `MillisecondDateTimeConverter`; `DateTime?` passes through `NullableMillisecondDateTimeConverter`. Both registered globally in `JsonSerializerOptions`. Nulls are **always serialized** (not omitted) per api-contract §2.

### `CreateTaskDto` — POST /api/tasks body

```
{ "title": string? }   // Title has no [Required] — DF-01 intentional design
```

The controller reads only `title`. All other fields are ignored. `TaskService` **trims** the title before saving (Mongoose `trim: true` parity — see plan.md §9, research.md R-10):

- `"  Đi chợ  "` → stored as `"Đi chợ"` and echoed as `"Đi chợ"`.
- `"\t\n"` → trims to `""`, fails the DB `CHECK (trim("Title") <> '')` constraint → 500 (DF-01).

Default values on create:
- `Status` = `"active"` (DB default)
- `CompletedAt` = `null`
- `CreatedAt` / `UpdatedAt` = `TimeProvider.GetUtcNow().UtcDateTime` truncated to ms

### `UpdateTaskDto` — PUT /api/tasks/:id body

```
{
  "title":       string?       // null or absent = ignore; present non-null = trim, then apply
  "status":      string?       // null or absent = ignore; "active"/"complete" = apply; other = 400
  "completedAt": JsonElement   // NOT nullable; branch on ValueKind (table below)
}
```

`CompletedAt` as a non-nullable `JsonElement` is the mechanism that distinguishes JSON null from absent (see research.md R-01). `JsonElement?` cannot: System.Text.Json yields `HasValue == false` for both.

| Wire state | `CompletedAt.ValueKind` | Effect on `TaskEntity.CompletedAt` |
|-----------|-------------------------|------------------------------------|
| Field absent | `Undefined` (`default(JsonElement)`) | Unchanged |
| `"completedAt": null` | `Null` | Set to `null` |
| `"completedAt": "<ISO 8601>"` | `String` | Parsed to UTC, truncated to ms, stored |
| Number, boolean, object, array | any other kind | Not applied; 400 `{ "message": "Dữ liệu nhiệm vụ không hợp lệ" }` (api-contract §8) |

**`completedAt` parsing** (see plan.md §10, research.md R-11): when `ValueKind == JsonValueKind.String`, parse via `JsonElement.GetDateTimeOffset().UtcDateTime` (always `Kind == Utc`, as Npgsql requires for `timestamptz`; `"…+07:00"` is converted to the matching `Z` instant) and truncate to milliseconds. Do not use `GetDateTime()`, which returns `Kind == Local` for offset strings. An unparseable date string returns HTTP 400 `{ "message": "Dữ liệu nhiệm vụ không hợp lệ" }` — not 500 — matching api-contract §5.8.

### `TaskListResponseDto` — GET /api/tasks response

```json
{
  "tasks":        TaskResponseDto[],
  "activeCount":  int,
  "completeCount": int,
  "totalCount":   int,
  "totalPages":   int,
  "page":         int,
  "limit":        int
}
```

Field semantics (api-contract §4):
- `totalCount`: tasks matching **both** date filter and status filter → used for `totalPages` calculation.
- `activeCount`, `completeCount`: tasks matching **only** date filter, grouped by status → drives tab badges regardless of `filter`.
- `totalPages`: `max(1, ceil(totalCount / limit))` — always ≥ 1.
- `page`: the requested page number, clamped to minimum 1 only; never clamped to `totalPages`.
- `limit`: the clamped limit value [1, 50].

---

## Task State Transitions

```
CREATE
  POST /api/tasks { "title": "..." }
  → status: "active", completedAt: null, createdAt: now, updatedAt: now

COMPLETE
  PUT /api/tasks/:id { "status": "complete", "completedAt": "<ISO>" }
  → status: "complete", completedAt: <parsed date>, updatedAt: now

REOPEN
  PUT /api/tasks/:id { "status": "active", "completedAt": null }
  → status: "active", completedAt: null, updatedAt: now

RENAME
  PUT /api/tasks/:id { "title": "new title" }
  → title: "new title", status/completedAt unchanged, updatedAt: now

DELETE
  DELETE /api/tasks/:id
  → task removed; full TaskResponseDto returned in body
```

`createdAt` is immutable after creation. `updatedAt` is server-set on every write, regardless of which fields change.

---

## Timestamp Handling Summary

| Stage | Action |
|-------|--------|
| Service layer — write | Truncate `DateTime` to milliseconds: `new DateTime(dt.Ticks - dt.Ticks % TimeSpan.TicksPerMillisecond, dt.Kind)` |
| EF Core | Stores truncated UTC value in PostgreSQL `timestamptz` |
| EF Core read | Returns `DateTime` with `DateTimeKind.Utc` (Npgsql default) |
| JSON serialization | `MillisecondDateTimeConverter` formats as `yyyy-MM-ddTHH:mm:ss.fffZ` |
| Wire result | `"2026-09-28T10:00:00.001Z"` — always 3 fractional digits, always trailing Z |

This pipeline guarantees string equality between what was stored and what is returned.
