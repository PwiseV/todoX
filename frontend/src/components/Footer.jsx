import React from "react";

const Footer = ({ completedTasksCount = 0, activeTasksCount = 0 }) => {
  const totalTasksCount = completedTasksCount + activeTasksCount;

  // Chưa có nhiệm vụ nào thì không hiện gì cả
  if (totalTasksCount === 0) return null;

  let message;
  if (completedTasksCount === 0) {
    message = `Hãy bắt đầu làm ${activeTasksCount} nhiệm vụ nào!`;
  } else if (activeTasksCount === 0) {
    message = `🎉 Tuyệt vời! Bạn đã hoàn thành tất cả ${completedTasksCount} nhiệm vụ. Nghỉ ngơi thôi!`;
  } else {
    message = `🎉 Bạn đã hoàn thành ${completedTasksCount}/${totalTasksCount} nhiệm vụ, còn ${activeTasksCount} việc nữa thôi. Cố lên!`;
  }

  return (
    <div className="text-center">
      <p className="text-sm text-muted-foreground">{message}</p>
    </div>
  );
};

export default Footer;
