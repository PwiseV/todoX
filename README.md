# ✅ TodoX

> Một cái app to-do "chill" để luyện tay code full-stack: React ở mặt tiền, Express + MongoDB ở hậu trường. Code along, học tới đâu ghi công tới đó! 🚀

## 🧩 App làm được gì?

Quản lý việc cần làm siêu gọn: thêm task, sửa task, xoá task, lọc theo ngày giờ, xem thống kê nhanh. Không drama, không rườm rà — vào là làm việc luôn.

## 🛠️ Công nghệ dùng

**Frontend** (`/frontend`)
- ⚛️ React 19 + Vite
- 🎨 Tailwind CSS 4 + shadcn-style components
- 🧭 React Router
- 📡 Axios gọi API
- 🔔 Sonner (toast thông báo)

**Backend** (`/backend`)
- 🟢 Node.js + Express
- 🍃 MongoDB (Mongoose)
- 🔐 dotenv cho biến môi trường

## 📂 Cấu trúc thư mục

```
todoX/
├── backend/          # API Express + MongoDB
│   └── src/
│       ├── config/       # kết nối DB
│       ├── controllers/  # xử lý logic
│       ├── models/       # schema Task
│       └── routes/       # định nghĩa route
└── frontend/         # Giao diện React
    └── src/
        ├── components/   # component UI
        ├── pages/        # các trang
        └── lib/          # helper & data
```

## 🚀 Chạy thử ở local

### 1. Clone về

```bash
git clone <repo-url>
cd todoX
```

### 2. Backend

```bash
cd backend
npm install
```

Tạo file `.env` trong `backend/` với nội dung:

```
MONGODB_CONNECTION_STRING=<connection-string-mongodb-cua-ban>
PORT=5001
```

Chạy server:

```bash
npm run dev
```

### 3. Frontend

```bash
cd frontend
npm install
npm run dev
```

Mở trình duyệt lên và bắt đầu ghi task thôi! 📝

## 📡 API chính

| Method | Endpoint          | Mô tả              |
|--------|--------------------|---------------------|
| GET    | `/api/tasks`       | Lấy danh sách task  |
| POST   | `/api/tasks`       | Tạo task mới        |
| PUT    | `/api/tasks/:id`   | Cập nhật task       |
| DELETE | `/api/tasks/:id`   | Xoá task            |

## 🗺️ Kế hoạch (đang cập nhật)

- [ ] Hoàn thiện các tính năng còn lại
- [ ] Deploy frontend + backend
- [ ] Thêm auth (có thể)

---

Made with ☕ và vài dòng code vui vẻ mỗi ngày.
