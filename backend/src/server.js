import express from 'express';
import tasksRoute from './routes/tasksRouter.js';
import {connectDB} from './config/db.js';
import dotenv from 'dotenv';

dotenv.config();

const PORT = process.env.PORT || 5001;

const app = express();

app.use(express.json());

app.use("/api/tasks", tasksRoute);

connectDB().then(() => {
        app.listen(PORT, () => {
        console.log(`Server bắt đầu trên cổng ${PORT}`);
    });
});

