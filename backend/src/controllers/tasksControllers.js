import Task from '../models/Task.js';

// Trả về mốc thời gian bắt đầu ứng với bộ lọc, hoặc null nếu không lọc.
// Dùng Date của Node nên mốc được tính theo giờ của máy chạy server.
const getStartDate = (dateQuery) => {
    const now = new Date();

    switch (dateQuery) {
        case "today": {
            const start = new Date(now);
            start.setHours(0, 0, 0, 0);
            return start;
        }
        case "week": {
            const start = new Date(now);
            // getDay(): 0 = Chủ nhật, 1 = Thứ hai...
            // Quy đổi để tuần bắt đầu từ Thứ hai cho đúng thói quen Việt Nam.
            const dayOfWeek = (start.getDay() + 6) % 7;
            start.setDate(start.getDate() - dayOfWeek);
            start.setHours(0, 0, 0, 0);
            return start;
        }
        case "month":
            return new Date(now.getFullYear(), now.getMonth(), 1);
        default:
            // "all" hoặc giá trị lạ -> không lọc gì cả
            return null;
    }
};

// Quy đổi bộ lọc của giao diện (all/active/completed)
// sang điều kiện status trong CSDL (active/complete).
const getStatusMatch = (filter) => {
    switch (filter) {
        case "active":
            return { status: "active" };
        case "completed":
            return { status: "complete" };
        default:
            return {};
    }
};

export const getAllTasks = async (req, res) => {
    try {
        const { dateQuery, filter } = req.query;

        // Ép kiểu và kẹp biên để tham số bậy bạ không làm vỡ truy vấn
        const page = Math.max(1, parseInt(req.query.page) || 1);
        const limit = Math.min(50, Math.max(1, parseInt(req.query.limit) || 5));
        const skip = (page - 1) * limit;

        const startDate = getStartDate(dateQuery);
        const dateMatch = startDate ? { createdAt: { $gte: startDate } } : {};
        const statusMatch = getStatusMatch(filter);

        // Lọc ngày đặt TRƯỚC $facet -> mọi nhánh bên dưới đều tính
        // trên cùng một tập dữ liệu.
        const result = await Task.aggregate([
            { $match: dateMatch },
            {
                $facet: {
                    tasks: [
                        { $match: statusMatch },
                        // Việc chưa xong luôn nằm trên, trong mỗi nhóm thì mới nhất trước.
                        // Dùng trường phụ thay vì sort thẳng theo status, vì sort theo
                        // chuỗi chỉ tình cờ đúng ("active" < "complete") và sẽ sai ngay
                        // khi thêm trạng thái mới.
                        {
                            $addFields: {
                                sortOrder: {
                                    $cond: [{ $eq: ["$status", "active"] }, 0, 1],
                                },
                            },
                        },
                        { $sort: { sortOrder: 1, createdAt: -1 } },
                        { $skip: skip },
                        { $limit: limit },
                        // Bỏ trường phụ đi để nó không lọt ra ngoài API
                        { $unset: "sortOrder" },
                    ],
                    // Tổng số task khớp CẢ ngày lẫn trạng thái -> dùng để tính số trang.
                    // Phải cùng điều kiện với nhánh tasks, nếu không số trang sẽ sai.
                    totalCount: [{ $match: statusMatch }, { $count: "count" }],
                    // Hai số đếm cho badge: tính trên toàn khoảng ngày,
                    // cố ý KHÔNG phụ thuộc tab đang chọn.
                    activeCount: [{ $match: { status: "active" } }, { $count: "count" }],
                    completeCount: [{ $match: { status: "complete" } }, { $count: "count" }],
                }
            }
        ])

        const tasks = result[0].tasks;
        const totalCount = result[0].totalCount[0]?.count || 0;
        const activeCount = result[0].activeCount[0]?.count || 0;
        const completeCount = result[0].completeCount[0]?.count || 0;
        const totalPages = Math.max(1, Math.ceil(totalCount / limit));

        res.status(200).json({
            tasks,
            activeCount,
            completeCount,
            totalCount,
            totalPages,
            page,
            limit,
        });
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