import Task from '../models/Task.js';

export const getAllTasks = async (req, res) => {
    try {
        const result = await Task.aggregate([
            {
                $facet: {
                    tasks: [{$sort: {createdAt: -1}}],
                    activeCount: [{ $match: { status: "active"}}, {$count: "count"}],
                    completeCount: [{ $match: { status: "complete"}}, {$count: "count"}],
                }
            }
        ])

        const tasks = result[0].tasks;
        const activeCount = result[0].activeCount[0]?.count || 0;
        const completeCount = result[0].completeCount[0]?.count || 0;

        res.status(200).json({tasks, activeCount , completeCount});
    } catch (error) {
        console.error("Lỗi khi getAllTasks", error);
        res.status(500).json({ message: "Lỗi hệ thống" });
    }
};

export const createTask = async (req, res) => {
    try {
        const {title} =  req.body;
        const task = new Task({title});

        const newTask = await task.save();
        res.status(201).json(newTask); 
    } catch (error) {
        console.error("Lỗi khi gọi createTask!",error);
        res.status(500).json({ message : "Lỗi hệ thống"});
    }
};

export const updateTask = async (req,res) => {
    try {
        const {title , status , completedAt} = req.body;

        if (title !== undefined) {
            // typeof loại luôn null, số, object... mọi thứ không phải chuỗi
            if (typeof title !== "string" || title.trim() === "") {
                return res
                    .status(400)
                    .json({ message: "Tiêu đề nhiệm vụ không được để trống" });
            }
        }

        // Chỉ đưa vào bản cập nhật những trường client thực sự gửi lên
        const updates = {};
        if (title !== undefined) updates.title = title.trim();
        if (status !== undefined) updates.status = status;
        if (completedAt !== undefined) updates.completedAt = completedAt;

        const updatedTask = await Task.findByIdAndUpdate(
            req.params.id,
            updates,
            { returnDocument: "after", runValidators: true }
        );

        if(!updatedTask ){
            return res.status(404).json({message: "Nhiệm vụ không tồn tại"})
        }
        
        res.status(200).json(updatedTask);

    } catch (error) {
        if (error.name === "ValidationError") {
            return res
                .status(400)
                .json({ message: "Dữ liệu nhiệm vụ không hợp lệ" });
        }
        console.error("Lỗi khi gọi updateTask!", error);
        res.status(500).json({ message: "Lỗi hệ thống" });
    }
};

export const deleteTask = async (req,res) => {
    try {
        const deleteTask = await Task.findByIdAndDelete(req.params.id);

        if (!deleteTask) {
            return res.status(404).json({message: "Nhiệm vụ không tồn tại!"})
        }

        res.status(200).json(deleteTask);
    } catch (error) {
        console.error("Lỗi khi gọi deleteTask!",error);
        res.status(500).json({ message : "Lỗi hệ thống"});
    }
}