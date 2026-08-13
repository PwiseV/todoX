import React, { useEffect, useState } from "react";
import TaskEmptyState from "./TaskEmptyState";
import TaskCard from "./TaskCard";
import { Card } from "./ui/card";

const TaskList = ({ filteredTasks, filter, handleTaskChanged, isLoading }) => {
  const [showWakeUpHint, setShowWakeUpHint] = useState(false);

  // Render gói miễn phí cho server ngủ sau 15 phút không ai truy cập,
  // đánh thức lại mất tới khoảng 50 giây. Chờ lâu mà không có lời giải thích
  // thì người dùng tưởng app hỏng, nên sau 4 giây thì nói rõ cho họ biết.
  useEffect(() => {
    if (!isLoading) {
      setShowWakeUpHint(false);
      return;
    }
    const timer = setTimeout(() => setShowWakeUpHint(true), 4000);
    return () => clearTimeout(timer);
  }, [isLoading]);

  if (isLoading) {
    return (
      <div className="space-y-3">
        {Array.from({ length: 3 }).map((_, index) => (
          <Card
            key={index}
            className="p-4 border-0 bg-gradient-card shadow-custom-md"
          >
            <div className="flex items-center gap-4 animate-pulse">
              <div className="flex-shrink-0 rounded-full size-8 bg-muted" />
              <div className="flex-1 space-y-2">
                <div className="w-1/2 h-4 rounded bg-muted" />
                <div className="w-1/4 h-3 rounded bg-muted" />
              </div>
            </div>
          </Card>
        ))}

        {showWakeUpHint && (
          <p className="text-sm text-center text-muted-foreground">
            Máy chủ đang khởi động lại, xin đợi một chút...
          </p>
        )}
      </div>
    );
  }

  if (!Array.isArray(filteredTasks) || filteredTasks.length === 0) {
    return <TaskEmptyState filter={filter} />;
  }

  return (
    <div className="space-y-3">
      {filteredTasks.map((task, index) => (
        <TaskCard
          key={task._id ?? index}
          task={task}
          index={index}
          handleTaskChanged={handleTaskChanged}
        />
      ))}
    </div>
  );
};

export default TaskList;
