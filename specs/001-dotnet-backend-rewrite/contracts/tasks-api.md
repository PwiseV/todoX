# Contract: /api/tasks

> **Derived from `docs/api-contract.md`; on any conflict `docs/api-contract.md` wins.**

**Source of truth**: `docs/api-contract.md` §3.2–§3.5, §4  
**Date**: 2026-09-28

All request/response bodies are `application/json`. All timestamps are ISO 8601 UTC with exactly 3 fractional digits and trailing Z (e.g. `2026-09-28T10:00:00.000Z`).

---

## GET /api/tasks

### Query Parameters

| Param | Type | Default | Clamping / Mapping |
|-------|------|---------|-------------------|
| `dateQuery` | string | (none — all tasks) | `today` / `week` / `month` apply a start-date filter; any other value or absent = no filter |
| `filter` | string | (none — all statuses) | `active` → `status = "active"`; `completed` → `status = "complete"`; any other value or absent = no filter |
| `page` | int | `1` | Parsed as int; NaN or < 1 → 1; values > totalPages echoed unchanged |
| `limit` | int | `5` | Parsed as int; clamped to [1, 50]; exception: `0` falls back to `5`, not `1` (DF-05) |

### Response 200

```json
{
  "tasks": [
    {
      "_id":         "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "title":       "Đi chợ",
      "status":      "active",
      "completedAt": null,
      "createdAt":   "2026-09-28T02:15:00.000Z",
      "updatedAt":   "2026-09-28T02:15:00.000Z",
      "__v":         0
    }
  ],
  "activeCount":   3,
  "completeCount": 7,
  "totalCount":    3,
  "totalPages":    1,
  "page":          1,
  "limit":         5
}
```

**Invariants**:
- `tasks` ordered: active before complete; within each group, newest `createdAt` first.
- `totalPages = max(1, ceil(totalCount / limit))` — always ≥ 1.
- `activeCount` / `completeCount` reflect the **date range only**, ignoring `filter`. They do not change when `filter` changes.
- `totalCount` reflects **both** `dateQuery` and `filter`.
- `page` echoes the requested value (clamped only to min 1); if requested page > totalPages, the server returns an empty `tasks` array.

### Response 500

```json
{ "message": "Lỗi hệ thống" }
```

---

## POST /api/tasks

### Request Body

```json
{ "title": "Đi chợ" }
```

Only `title` is read. All other fields are ignored.

### Response 201

Full `TaskResponseDto` for the created task:

```json
{
  "_id":         "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "title":       "Đi chợ",
  "status":      "active",
  "completedAt": null,
  "createdAt":   "2026-09-28T02:15:00.000Z",
  "updatedAt":   "2026-09-28T02:15:00.000Z",
  "__v":         0
}
```

### Response 500 — blank/missing title (DF-01, preserved quirk)

A null or whitespace-only title does **not** return 400. The request passes model binding (no `[Required]` attribute), fails at the DB `NOT NULL` / `CHECK` constraint, and the unhandled `DbUpdateException` returns:

```json
HTTP 500
{ "message": "Lỗi hệ thống" }
```

---

## PUT /api/tasks/:id

### Path Parameter

`id` — accepted as a raw string. A malformed UUID (e.g. `"abc"`) is not rejected by the route; `Guid.Parse` throws `FormatException` → 500 (DF-02).

### Request Body (partial update)

Any subset of:

```json
{
  "title":       "Đi siêu thị",
  "status":      "complete",
  "completedAt": "2026-09-28T03:00:00.000Z"
}
```

Field rules:
- `title`: if present and non-null, must be a non-blank string after `trim()`. Blank → 400. The trimmed value is stored (so `"  x  "` is persisted as `"x"`).
- `status`: if present, must be `"active"` or `"complete"`. Other values → 400.
- `completedAt`: three states — absent (leave unchanged), JSON null (clear to null), ISO string (parse as UTC via `JsonElement.GetDateTimeOffset().UtcDateTime`, so `Z` and offset strings like `+07:00` both store the correct UTC instant; truncate to ms, then store). An unparseable date string → 400 (api-contract §5.8). A number or boolean → 400 (see api-contract §8).
- Absent fields are not written to the DB.

### Responses

| Status | Body | Condition |
|--------|------|-----------|
| 200 | Full `TaskResponseDto` after update | Success |
| 400 | `{ "message": "Tiêu đề nhiệm vụ không được để trống" }` | `title` present but blank (a non-string `title` gets ASP.NET's default 400 body instead — see `docs/api-contract.md` §8) |
| 400 | `{ "message": "Dữ liệu nhiệm vụ không hợp lệ" }` | `status` present but outside allowed enum, or `completedAt` is an unparseable date string (api-contract §5.8) |
| 404 | `{ "message": "Nhiệm vụ không tồn tại" }` | No task with given id (well-formed UUID not found) |
| 500 | `{ "message": "Lỗi hệ thống" }` | Malformed id (FormatException) or other unhandled error |

**Note**: the 404 message here has **no trailing exclamation mark** (differs from DELETE).

---

## DELETE /api/tasks/:id

### Path Parameter

`id` — same raw-string rules as PUT (malformed → 500).

### Responses

| Status | Body | Condition |
|--------|------|-----------|
| 200 | Full `TaskResponseDto` of the deleted task | Success |
| 404 | `{ "message": "Nhiệm vụ không tồn tại!" }` | No task with given id (note: trailing `!`) |
| 500 | `{ "message": "Lỗi hệ thống" }` | Malformed id or other unhandled error |

**Note**: the 404 message here has a **trailing exclamation mark** — intentionally different from PUT (DF-03, preserved quirk).
