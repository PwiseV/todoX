import express from 'express';
import tasksRoute from './routes/tasksRouter.js';
import {connectDB} from './config/db.js';
import dotenv from 'dotenv';
import cors from 'cors'
import path from 'path';

dotenv.config();

// Render (và hầu hết máy chủ) chạy theo giờ UTC, khiến bộ lọc "Hôm nay"
// bắt đầu lúc 7 giờ sáng giờ Việt Nam thay vì nửa đêm.
// Đặt sẵn ở đây để không phụ thuộc vào việc nhớ khai báo trên dashboard.
// Phải đặt TRƯỚC khi tạo đối tượng Date đầu tiên thì Node mới nhận.
process.env.TZ = process.env.TZ || "Asia/Ho_Chi_Minh";

const PORT = process.env.PORT || 5001;
const isProduction = process.env.NODE_ENV === "production";
// Trả về thư mục đang chạy lệnh (khi deploy là thư mục backend)
const __dirname = path.resolve();

const app = express();

// middlewares
app.use(express.json());

// Khi deploy, frontend và backend dùng chung một origin nên không cần CORS.
// Chỉ lúc chạy máy local mới có chuyện Vite (5173) gọi sang Express (5001).
if (!isProduction) {
    app.use(cors({origin: "http://localhost:5173"}))
}

app.use("/api/tasks", tasksRoute);

// Kiểm tra sức khỏe - tiện để tự ping đánh thức server Render đang ngủ
app.get("/api/health", (req, res) => {
    res.status(200).json({ status: "ok", time: new Date().toISOString() });
});

if (isProduction) {
    const distPath = path.join(__dirname, "../frontend/dist");

    app.use(express.static(distPath));

    // Mọi đường dẫn còn lại trả về index.html để react-router tự xử lý.
    // Phải đặt SAU các route /api, nếu không nó nuốt hết request API.
    app.get("*", (req, res) => {
        res.sendFile(path.join(distPath, "index.html"));
    });
}

connectDB().then(() => {
    const server = app.listen(PORT, () => {
        console.log(`Server bắt đầu trên cổng ${PORT}`);
        console.log(`Múi giờ đang dùng: ${process.env.TZ}`);
    });

    // Không có listener cho 'error' thì Node ném "Unhandled 'error' event"
    // và giết cả tiến trình - đó là kiểu chết cứng khó hiểu nhất.
    server.on("error", (error) => {
        if (error.code === "EADDRINUSE") {
            console.error(
                `Cổng ${PORT} đang bị một tiến trình khác chiếm. ` +
                `Hãy tắt tiến trình đó rồi chạy lại, hoặc đổi PORT trong .env.`
            );
        } else {
            console.error("Lỗi khi khởi động server:", error);
        }
        process.exit(1);
    });
});
