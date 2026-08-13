import AddTask from "@/components/AddTask";
import DateTimeFilter from "@/components/DateTimeFilter";
import Footer from "@/components/Footer";
import Header from "@/components/Header";
import StatsAndFilter from "@/components/StatsAndFilter";
import TaskList from "@/components/TaskList";
import TaskListPagination from "@/components/TaskListPagination";
import React, { useEffect, useState } from "react";
import { toast } from "sonner";
import api from "@/lib/axios";

const TASKS_PER_PAGE = 5;

const HomePage = () => {
  const [taskBuffer, setTaskBuffer] = useState([]);
  const [activeTaskCount, setActiveTaskCount] = useState(0);
  const [completeTaskCount, setCompleteTaskCount] = useState(0);
  const [filter, setFilter] = useState("all");
  const [dateQuery, setDateQuery] = useState("all");
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  // Khởi tạo true để lần vào đầu tiên hiện khung chờ,
  // thay vì nháy "Chưa có nhiệm vụ" trong lúc dữ liệu chưa về.
  const [isLoading, setIsLoading] = useState(true);

  // Việc lọc và phân trang đều do backend làm, nên đổi bất kỳ tham số nào
  // cũng phải gọi lại API.
  useEffect(() => {
    fetchTasks();
  }, [dateQuery, filter, page]);

  // logic
  const fetchTasks = async () => {
    setIsLoading(true);
    try {
      const res = await api.get("/tasks", {
        params: { dateQuery, filter, page, limit: TASKS_PER_PAGE },
      });
      setTaskBuffer(res.data.tasks);
      setActiveTaskCount(res.data.activeCount);
      setCompleteTaskCount(res.data.completeCount);
      setTotalPages(res.data.totalPages);

      // Xóa hết task ở trang cuối sẽ khiến trang hiện tại không còn tồn tại.
      // Kéo người dùng về trang cuối cùng còn hợp lệ thay vì hiện danh sách rỗng.
      if (res.data.page > res.data.totalPages) {
        setPage(res.data.totalPages);
      }
    } catch (error) {
      console.error("Lỗi xảy ra khi truy xuất tasks:", error);
      toast.error("Lỗi xảy ra khi truy xuất tasks.");
    } finally {
      // finally để dù lỗi cũng thoát khỏi trạng thái chờ,
      // không thì khung chờ đứng im mãi mãi.
      setIsLoading(false);
    }
  };

  const handleTaskChanged = () =>{
    fetchTasks();
  }

  // Đổi bộ lọc thì phải về trang 1, nếu không sẽ mắc kẹt ở
  // trang 3 của một bộ lọc chỉ có 1 trang.
  const handleFilterChange = (newFilter) => {
    setFilter(newFilter);
    setPage(1);
  };

  const handleDateQueryChange = (newDateQuery) => {
    setDateQuery(newDateQuery);
    setPage(1);
  };

  return (
    <div className="min-h-screen w-full bg-white relative overflow-hidden">
      {/* Soft Blue Radial Background */}
      <div
        className="absolute inset-0 z-0"
        style={{
          background: "#ffffff",
          backgroundImage: `
       radial-gradient(circle at top center, rgba(59, 130, 246, 0.5),transparent 70%)
     `,
        }}
      />
      {/* Your Content Here */}
      <div className="container pr-8 mx-auto relative z-10">
        <div className="w-full max-w-2xl p-6 mx-auto space-y-6">
          {/* Đầu trang */}
          <Header />

          {/*  Tạo nhiệm vụ */}
          <AddTask handleNewTaskAdded={handleTaskChanged} />

          {/* Thống kê và bộ lọc */}
          <StatsAndFilter
            filter={filter}
            setFilter={handleFilterChange}
            activeTasksCount={activeTaskCount}
            completedTasksCount={completeTaskCount}
          />

          {/* Danh sách nhiệm vụ - backend đã lọc và cắt trang sẵn */}
          <TaskList
            filteredTasks={taskBuffer}
            filter={filter}
            handleTaskChanged={handleTaskChanged}
            isLoading={isLoading}
          />

          {/* Phân trang và lọc theo Date  */}
          <div className="flex flex-col items-center justify-between gap-6 sm:flex-row">
            <TaskListPagination
              page={page}
              totalPages={totalPages}
              setPage={setPage}
            />
            <DateTimeFilter
              dateQuery={dateQuery}
              setDateQuery={handleDateQueryChange}
            />
          </div>

          {/* Chân trang */}
          <Footer
            activeTasksCount={activeTaskCount}
            completedTasksCount={completeTaskCount}
          />
        </div>
      </div>
    </div>
  );
};

export default HomePage;
