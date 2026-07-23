import mongoose from 'mongoose';

const connectDB = async () => {
    try{
        await mongoose.connect(process.env.MONGODB_CONNECTION_STRING);

        console.log("Liên kết CSDL thành công!");
    } catch (error) {
        console.error("Lỗi khi kết nối CSDL:", error);
        process.exit(1); // đóng cổng database nếu không kết nối được
    }
};

export { connectDB };