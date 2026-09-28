# Contract: /api/health

> **Derived from `docs/api-contract.md`; on any conflict `docs/api-contract.md` wins.**

**Source of truth**: `docs/api-contract.md` §3.1  
**Date**: 2026-09-28

---

## GET /api/health

No query parameters. No request body. No authentication required.

### Response 200

```json
{
  "status": "ok",
  "time":   "2026-09-28T02:15:00.000Z"
}
```

**Field rules**:
- `status`: always the string `"ok"`.
- `time`: current server UTC time at moment of request; serialized as ISO 8601 UTC with exactly 3 fractional digits and trailing Z (same `MillisecondDateTimeConverter` used for task timestamps).

**Usage**: The original Node backend exposes this endpoint to "ping-wake" a Render free-tier instance after idle sleep. The .NET backend retains it for compatibility. The frontend does not call this endpoint; it is for ops use only.
