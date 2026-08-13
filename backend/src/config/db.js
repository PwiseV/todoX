import mongoose from 'mongoose';

const connectDB = async () => {
    try{
        await mongoose.connect(process.env.MONGODB_CONNECTION_STRING);

        // Sự cố kết nối SAU khi đã chạy (rớt mạng, Atlas ngắt phiên nhàn rỗi...)
        // không đi qua khối try/catch này. Không lắng nghe thì tiến trình chết cứng.
        mongoose.connection.on("error", (error) => {
            console.error("Lỗi kết nối CSDL:", error.message);
        });

        mongoose.connection.on("disconnected", () => {
            console.warn("Mất kết nối CSDL, mongoose đang tự kết nối lại...");
        });

        console.log("Liên kết CSDL thành công!");
    } catch (error) {
        console.error("Lỗi khi kết nối CSDL:", error);
        process.exit(1); // đóng cổng database nếu không kết nối được
    }
};

export { connectDB };