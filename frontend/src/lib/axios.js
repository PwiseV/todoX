import axios from 'axios'

// Lúc dev: Vite chạy cổng 5173, Express chạy cổng 5001 -> phải gọi tuyệt đối.
// Khi deploy: Express phục vụ luôn file build nên cùng origin -> dùng đường
// dẫn tương đối, khỏi cần biết tên miền và cũng không dính CORS.
const BASE_URL =
    import.meta.env.MODE === "development" ? "http://localhost:5001/api" : "/api";

const api = axios.create({
    baseURL: BASE_URL,
})

export default api;
