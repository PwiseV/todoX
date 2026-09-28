# API Contract — todoX Backend

Tài liệu này mô tả hợp đồng API của backend Node/Express + MongoDB hiện tại
(`backend/src`). Nguồn tham chiếu:

- `backend/src/server.js` — khai báo app, prefix `/api`, timezone.
- `backend/src/routes/tasksRouter.js` — khai báo route `/api/tasks`.
- `backend/src/controllers/tasksControllers.js` — nghiệp vụ chính.
- `backend/src/models/Task.js` — schema Mongoose.
- `backend/src/config/db.js` — kết nối MongoDB.

§1–§5 mô tả backend Node/Express hiện tại (không đề xuất sửa code). §6–§7 mô tả backend .NET mới.

---

## 1. Thông tin chung

| Mục | Giá trị |
| --- | --- |
| Base URL (local) | `http://localhost:5001` |
| Base URL (production) | Cùng origin với frontend (Express serve `frontend/dist`) |
| Prefix API | `/api` |
| Content-Type | `application/json` |
| Auth | Không có (public) |
| CORS | Chỉ bật ở non-production, cho phép origin `http://localhost:5173` |
| Timezone của process | `process.env.TZ || "Asia/Ho_Chi_Minh"` (đặt trong `server.js`) |

Tất cả response lỗi hệ thống đều có dạng: `{ "message": "Lỗi hệ thống" }` với
status `500`.

---

## 2. Schema Task

Định nghĩa tại `backend/src/models/Task.js`.

| Trường | Kiểu | Mặc định | Ràng buộc |
| --- | --- | --- | --- |
| `_id` | ObjectId (string 24 hex) | Mongo tự sinh | Khóa chính |
| `title` | String | — (bắt buộc) | `required: true`, `trim: true` |
| `status` | String enum | `"active"` | Chỉ nhận `"active"` hoặc `"complete"` |
| `completedAt` | Date \| null | `null` | Không validate ràng buộc với `status` |
| `createdAt` | Date | Mongo tự sinh (timestamps) | Read-only |
| `updatedAt` | Date | Mongo tự sinh (timestamps) | Read-only |
| `__v` | Number | Mongo tự sinh | Version key của Mongoose |

Ví dụ JSON:

```json
{
  "_id": "6650e5b1a1c9f2a4b8f0e123",
  "title": "Đi chợ",
  "status": "active",
  "completedAt": null,
  "createdAt": "2026-09-26T02:15:00.000Z",
  "updatedAt": "2026-09-26T02:15:00.000Z",
  "__v": 0
}
```

Ghi chú:

- Enum `status` dùng dạng ngắn `complete` (không phải `completed`). Bộ lọc UI
  `filter=completed` được ánh xạ về `status: "complete"` trong controller.
- `completedAt` không được server tự cập nhật khi `status` đổi — client phải
  gửi kèm khi cần.
- Không có ràng buộc unique trên `title`.

---

## 3. Endpoints

### 3.1 `GET /api/health`

Trả về trạng thái sống của server (dùng để ping đánh thức Render).

- **Query / Body**: không.
- **Response 200**:

```json
{
  "status": "ok",
  "time": "2026-09-26T02:15:00.000Z"
}
```

### 3.2 `GET /api/tasks`

Lấy danh sách task kèm số đếm và phân trang.

#### Query params

| Tham số | Kiểu | Mặc định | Giá trị hợp lệ | Ghi chú |
| --- | --- | --- | --- | --- |
| `dateQuery` | string | (không lọc) | `today` \| `week` \| `month` \| `all` \| khác | Bất kỳ giá trị nào khác 3 giá trị đầu đều được coi là "không lọc theo ngày" |
| `filter` | string | (tất cả) | `active` \| `completed` \| `all` \| khác | `completed` → `status: complete`. Giá trị khác `active/completed` = không lọc |
| `page` | int | `1` | `>= 1` | Ép qua `parseInt`; NaN hoặc <1 sẽ về 1 |
| `limit` | int | `5` | `1..50` | Bị kẹp trong `[1, 50]` |

#### Response 200

```json
{
  "tasks": [
    {
      "_id": "6650e5b1a1c9f2a4b8f0e123",
      "title": "Đi chợ",
      "status": "active",
      "completedAt": null,
      "createdAt": "2026-09-26T02:15:00.000Z",
      "updatedAt": "2026-09-26T02:15:00.000Z",
      "__v": 0
    }
  ],
  "activeCount": 3,
  "completeCount": 7,
  "totalCount": 3,
  "totalPages": 1,
  "page": 1,
  "limit": 5
}
```

Ý nghĩa các trường:

- `tasks`: danh sách task ở trang hiện tại, đã lọc theo `dateQuery` và
  `filter`.
- `totalCount`: tổng số task khớp **cả** `dateQuery` **và** `filter` — dùng
  để tính `totalPages`.
- `activeCount`, `completeCount`: đếm task theo `status` trong phạm vi
  `dateQuery` (cố ý **không** phụ thuộc vào `filter`, để badge tab luôn ổn
  định khi người dùng chuyển tab).
- `totalPages = max(1, ceil(totalCount / limit))` — luôn `>= 1` kể cả khi
  không có task nào.
- `page`, `limit`: giá trị đã được ép/kẹp biên (echo lại).

#### Response 500

```json
{ "message": "Lỗi hệ thống" }
```

### 3.3 `POST /api/tasks`

Tạo task mới.

#### Request body

```json
{ "title": "Đi chợ" }
```

- Chỉ đọc `title` từ body. Các trường khác bị bỏ qua.
- `status` sẽ mặc định `"active"`, `completedAt` mặc định `null`.

#### Response 201

Trả nguyên object Task vừa tạo (xem schema).

#### Response 500

- Trường hợp `title` không hợp lệ (thiếu, rỗng sau `trim`) hiện nay **không
  được kiểm tra riêng** trong controller. Mongoose sẽ ném `ValidationError`,
  nhưng nhánh `catch` không phân loại nên trả `500 { message: "Lỗi hệ thống" }`
  thay vì `400`. Đây là điểm khác biệt so với `PUT` (xem mục "Rủi ro" bên
  dưới).

### 3.4 `PUT /api/tasks/:id`

Cập nhật một task.

#### Path params

- `id`: ObjectId (24 hex). ID không hợp lệ sẽ tạo `CastError` → rơi vào
  nhánh 500 (không được xử lý riêng).

#### Request body (partial update)

Bất kỳ tập con nào của:

```json
{
  "title": "Đi siêu thị",
  "status": "complete",
  "completedAt": "2026-09-26T03:00:00.000Z"
}
```

Quy tắc:

- Chỉ các trường `!== undefined` mới được đưa vào bản cập nhật.
- `title`: nếu có mặt, phải là `string` non-empty sau `trim()`. Sai → `400`.
- `status`: nếu có mặt, không được kiểm ở controller — chuyển thẳng cho
  Mongoose validate enum (`runValidators: true`). Không thuộc enum →
  `ValidationError` → `400 { message: "Dữ liệu nhiệm vụ không hợp lệ" }`.
- `completedAt`: không validate ở controller. Client tự chịu trách nhiệm
  gửi ISO date string hoặc `null`.

#### Responses

| Status | Body | Khi nào |
| --- | --- | --- |
| 200 | Task sau khi cập nhật | Thành công |
| 400 | `{ "message": "Tiêu đề nhiệm vụ không được để trống" }` | `title` gửi lên nhưng rỗng/không phải string |
| 400 | `{ "message": "Dữ liệu nhiệm vụ không hợp lệ" }` | `ValidationError` từ Mongoose (ví dụ status ngoài enum) |
| 404 | `{ "message": "Nhiệm vụ không tồn tại" }` | Không có task với `id` đó |
| 500 | `{ "message": "Lỗi hệ thống" }` | Lỗi khác (bao gồm `CastError` do id sai định dạng) |

### 3.5 `DELETE /api/tasks/:id`

Xóa một task.

#### Responses

| Status | Body | Khi nào |
| --- | --- | --- |
| 200 | Task vừa xóa (nguyên object) | Thành công |
| 404 | `{ "message": "Nhiệm vụ không tồn tại!" }` | Không có task với `id` đó |
| 500 | `{ "message": "Lỗi hệ thống" }` | Lỗi khác (bao gồm `CastError`) |

Lưu ý: message 404 ở endpoint này có dấu `!` ở cuối, khác với 404 của `PUT`
(không có `!`). Client so khớp chuỗi cần biết sự khác biệt này.

---

## 4. Quy tắc nghiệp vụ

### 4.1 Lọc theo `dateQuery`

Thực hiện ở `getStartDate(dateQuery)` trong `tasksControllers.js`. So khớp
theo trường `createdAt >= startDate`. Mốc `now = new Date()` dùng giờ của
process (đã set `TZ = Asia/Ho_Chi_Minh` trong `server.js`).

| Giá trị | Mốc bắt đầu | Ghi chú |
| --- | --- | --- |
| `today` | 00:00:00.000 hôm nay | `setHours(0,0,0,0)` |
| `week` | 00:00:00.000 của thứ Hai tuần này | Quy đổi `(getDay() + 6) % 7` để tuần bắt đầu thứ Hai (thói quen VN); nếu hôm nay Chủ nhật thì lùi 6 ngày |
| `month` | 00:00:00.000 ngày 1 tháng hiện tại | `new Date(year, month, 1)` |
| `all` / khác / thiếu | `null` — **không** lọc theo ngày | Bao gồm cả các giá trị lạ như `"foo"` |

Không có mốc kết thúc (`$lte`) — mọi task từ mốc trở về sau đều khớp, bao
gồm cả task có `createdAt` trong tương lai (nếu có).

### 4.2 Lọc theo `filter`

Ánh xạ ở `getStatusMatch(filter)`:

| `filter` (UI) | Điều kiện Mongo |
| --- | --- |
| `active` | `{ status: "active" }` |
| `completed` | `{ status: "complete" }` |
| `all` / khác / thiếu | `{}` (không lọc) |

Ghi chú: UI dùng `completed` (có `d`), DB dùng `complete` (không `d`). Đây là
sự bất đối xứng có chủ ý; nếu FE gửi `complete` thẳng lên, endpoint sẽ **không**
lọc gì (rơi vào default).

### 4.3 Phân trang

- `page` mặc định `1`, ép qua `Math.max(1, parseInt(page) || 1)`.
- `limit` mặc định `5`, kẹp trong `[1, 50]` bằng
  `Math.min(50, Math.max(1, parseInt(limit) || 5))`.
- `skip = (page - 1) * limit`.
- `totalPages = max(1, ceil(totalCount / limit))`.
- Nếu `page` vượt quá `totalPages`, server vẫn trả 200 với `tasks: []` (không
  có redirect/clamp trên server; trách nhiệm hiển thị thuộc về client).

### 4.4 Thứ tự sắp xếp

Trong nhánh `tasks` của `$facet`:

1. Thêm trường phụ `sortOrder = 0` nếu `status === "active"`, ngược lại `1`.
2. `$sort: { sortOrder: 1, createdAt: -1 }` → task chưa xong lên đầu; trong
   mỗi nhóm, task mới tạo hơn lên trước.
3. `$unset: "sortOrder"` để trường phụ không lộ ra API.

Ghi chú: sắp xếp diễn ra **trước** `$skip`/`$limit`, nên phân trang tôn trọng
thứ tự này. Tie-breaker chỉ có `createdAt` — hai task cùng `status` và cùng
`createdAt` chính xác đến ms sẽ có thứ tự không xác định.

### 4.5 Số đếm cho badge

Trong `$facet`, sau khi đã `$match: dateMatch`:

- `totalCount`: đếm số document khớp **cả** `dateMatch` **và** `statusMatch`
  (đúng với danh sách đang hiển thị) — dùng để tính `totalPages`.
- `activeCount`: đếm document có `status: "active"` trong phạm vi `dateMatch`
  (không quan tâm `filter` UI đang chọn).
- `completeCount`: tương tự với `status: "complete"`.

Hàm ý: khi chuyển tab, `activeCount`/`completeCount` không đổi (miễn là
`dateQuery` không đổi) — đây là hành vi cố ý.

---

## 5. Điểm mơ hồ / rủi ro trong code hiện tại

Ghi lại để tham khảo — **không** đề xuất sửa trong tài liệu này.

1. **Múi giờ phụ thuộc process env.** `server.js` set `process.env.TZ ||=
   "Asia/Ho_Chi_Minh"`. Nếu môi trường chạy đã set `TZ` sang giá trị khác
   (ví dụ `UTC` trong container), toàn bộ mốc `today`/`week`/`month` sẽ bị
   dịch mà không có cảnh báo. Không có endpoint để client kiểm tra timezone
   server đang dùng (chỉ log ra console).
2. **`dateQuery` không có mốc kết thúc.** Bộ lọc chỉ dùng `$gte: startDate`,
   nên `today` thực chất là "từ 00:00 hôm nay trở đi" — task có `createdAt`
   trong tương lai vẫn khớp. Với dữ liệu thực tế thì hiếm, nhưng cần biết
   khi test hoặc seed dữ liệu.
3. **`dateQuery`/`filter` không validate chặt.** Giá trị lạ đều rơi vào
   default (không lọc) mà không trả 400. Bug ở client (typo `weeks`,
   `Complete`, `COMPLETED`…) sẽ bị nuốt âm thầm.
4. **Bất đối xứng `completed` (UI) vs `complete` (DB).** Nếu ai đó gọi API
   trực tiếp với `filter=complete`, kết quả sẽ **không** như mong đợi (không
   lọc). Cần biết khi viết integration test hoặc client thứ hai.
5. **`POST /api/tasks` không phân loại ValidationError.** Thiếu `title` hoặc
   `title` rỗng sau `trim` → Mongoose ném `ValidationError` → rơi vào catch
   chung → trả `500` (đúng lý phải là `400`). `PUT` xử lý đúng nhánh này,
   nhưng `POST` thì không — bất đối xứng giữa hai endpoint.
6. **Không xử lý `CastError` của ObjectId.** `PUT`/`DELETE` với `id` không
   phải 24 hex sẽ trả `500` thay vì `400`/`404`.
7. **`completedAt` không được server tự set.** Khi client `PUT` để đánh dấu
   hoàn thành mà quên gửi `completedAt`, DB sẽ giữ nguyên giá trị cũ (mặc
   định `null`). Không có "nguồn sự thật" tự động giữa `status` và
   `completedAt`.
8. **`PUT` chấp nhận `completedAt` bất kỳ.** Không validate kiểu; chuỗi
   không phải date hợp lệ có thể được Mongoose cast lỗi → 400 với thông
   điệp chung `"Dữ liệu nhiệm vụ không hợp lệ"`, không nói rõ trường nào.
9. **Message 404 khác nhau giữa `PUT` và `DELETE`** (`"Nhiệm vụ không tồn
   tại"` vs `"Nhiệm vụ không tồn tại!"`). Client nào so khớp chuỗi sẽ dễ
   nhầm.
10. **Không có rate limit / auth.** Bất kỳ ai có URL đều có thể CRUD toàn
    bộ task; chấp nhận được trong bối cảnh app cá nhân, nhưng cần lưu ý khi
    public.
11. **`page` vượt biên không được thông báo.** `page=9999` trên tập rỗng
    trả 200 với `tasks: []`, `totalPages: 1`, `page: 9999` — client phải tự
    phát hiện chênh lệch.
12. **`limit` bị kẹp âm thầm.** Client gửi `limit=1000` sẽ nhận về
    `limit: 50` — không có warning; dễ gây hiểu lầm khi debug pagination.
13. **CORS chỉ mở cho `localhost:5173`.** Nếu dev chạy Vite ở port khác (do
    xung đột) sẽ bị chặn CORS mà không có log rõ ràng ở backend.
14. **`__v` bị lộ ra response.** Không nghiêm trọng nhưng là chi tiết nội
    bộ của Mongoose; nếu sau này chuyển ORM/DB, client đã bám vào field này
    sẽ vỡ.

---

## 6. Intentional Deviations (dotnet-backend rewrite)

Ghi lại các điểm backend mới cố ý khác với Node/Mongoose. Mỗi deviation đã
được xác nhận là **không yêu cầu thay đổi frontend**.

### 6.1 `_id` — UUID thay cho MongoDB ObjectId

| Mục | Node/Mongoose | dotnet-backend |
| --- | --- | --- |
| Kiểu | ObjectId (24 hex) | UUID (lowercase hyphenated string, e.g. `"a1b2c3d4-..."`) |
| Wire field name | `_id` | `_id` (giữ nguyên) |

**Lý do**: PostgreSQL không có kiểu ObjectId. UUID là lựa chọn bền vững
nhất cho primary key trên PostgreSQL.

**Xác nhận frontend**: `TaskCard.jsx`,
`TaskList.jsx` — `_id` chỉ được dùng làm opaque string trong URL path params
(`/tasks/${task._id}`) và React `key`; không có kiểm tra định dạng 24-hex.
Không cần thay đổi bất kỳ file frontend nào.

### 6.2 `__v` — Luôn trả về `0`

| Mục | Node/Mongoose | dotnet-backend |
| --- | --- | --- |
| Giá trị | Tự tăng bởi Mongoose | Hằng số `0` |

**Lý do**: Bản viết lại không triển khai version key vì frontend không đọc field này. Frontend không đọc
`__v` ở bất kỳ đâu trong `frontend/src`; field chỉ hiện diện để giữ đúng
wire shape.

**Xác nhận frontend**: Tìm kiếm toàn bộ `frontend/src` — không tìm thấy
tham chiếu nào đến `__v`.

---

## 7. Deferred Fixes

Các quirk từ §5 được **giữ nguyên** trong dotnet-backend để frontend hoạt
động end-to-end trước. Sẽ chỉ sửa sau khi frontend đã xác nhận hoạt động
tốt trên backend mới.

| # | Quirk | §5 ref | Hệ quả cần lưu ý |
| --- | --- | --- | --- |
| DF-01 | `POST /api/tasks` với `title` rỗng/thiếu trả `500` thay vì `400` | §5.5 | Blank/missing title on POST must be reproduced deliberately: an explicit check that ends in the generic 500 handler. ASP.NET's automatic 400 from `[ApiController]` model validation MUST NOT trigger (no `[Required]`-style validation attributes, or suppress the automatic filter). A database NOT NULL constraint alone is not enough because an empty string passes it. |
| DF-02 | `PUT`/`DELETE` với `id` sai định dạng trả `500` thay vì `400`/`404` | §5.6 | The id is parsed in code; a parse failure (FormatException) goes to the global exception handler → 500. In the original controller, title validation runs first; a PUT with a malformed id but a valid title skips the 400, reaches `findByIdAndUpdate`, throws a CastError, and exits via the generic 500 handler. |
| DF-03 | Message 404 khác nhau giữa `PUT` (`"Nhiệm vụ không tồn tại"`) và `DELETE` (`"Nhiệm vụ không tồn tại!"`) | §5.9 | Giữ nguyên sự bất đối xứng; client so khớp chuỗi không cần sửa. |
| DF-04 | Thứ tự sắp xếp không xác định khi hai task có cùng `createdAt` chính xác đến ms | §4.4 | Latent issue — dễ kích hoạt khi seed data hoặc bulk insert. Pagination có thể không ổn định trong trường hợp này. Test cases PHẢI dùng `createdAt` khác nhau để đảm bảo thứ tự xác định. |

Verified 2026-09-28: the frontend never reads error status or message and blocks blank titles before sending, so fixing these quirks later cannot break it.
