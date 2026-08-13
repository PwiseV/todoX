import React from "react";
import {
  Pagination,
  PaginationContent,
  PaginationEllipsis,
  PaginationItem,
  PaginationLink,
  PaginationNext,
  PaginationPrevious,
} from "@/components/ui/pagination";

// Số ô số tối đa hiện cùng lúc (không tính dấu ...).
// Ít hơn ngưỡng này thì hiện hết cho đơn giản.
const MAX_SLOTS = 7;
// Số trang hiện ở mỗi bên trang đang đứng
const SIBLINGS = 1;

// Trả về danh sách ô cần render: số trang, hoặc chuỗi đánh dấu vị trí dấu "..."
const getPageItems = (page, totalPages) => {
  if (totalPages <= MAX_SLOTS) {
    return Array.from({ length: totalPages }, (_, i) => i + 1);
  }

  // Cửa sổ quanh trang hiện tại, luôn nằm giữa trang đầu và trang cuối
  let start = Math.max(2, page - SIBLINGS);
  let end = Math.min(totalPages - 1, page + SIBLINGS);

  // Khi đứng sát hai đầu, cửa sổ bị cụt một bên.
  // Bù sang bên còn lại để thanh phân trang không co giãn khi chuyển trang.
  if (page - SIBLINGS < 2) {
    end = Math.min(totalPages - 1, end + (2 - (page - SIBLINGS)));
  }
  if (page + SIBLINGS > totalPages - 1) {
    start = Math.max(2, start - (page + SIBLINGS - (totalPages - 1)));
  }

  const items = [1];
  if (start > 2) items.push("ellipsis-start");
  for (let p = start; p <= end; p++) items.push(p);
  if (end < totalPages - 1) items.push("ellipsis-end");
  items.push(totalPages);

  return items;
};

const TaskListPagination = ({ page, totalPages, setPage }) => {
  // Luôn hiện thanh phân trang, kể cả khi chỉ có đúng trang 1.
  // Vừa giữ layout ổn định (DateTimeFilter không bị kéo sang trái),
  // vừa cho người dùng thấy trang 2, 3... xuất hiện dần khi task nhiều lên.

  // Kẹp trong khoảng hợp lệ để không bao giờ gọi API với trang không tồn tại
  const goTo = (target) => {
    setPage(Math.min(Math.max(target, 1), totalPages));
  };

  const pageItems = getPageItems(page, totalPages);

  return (
    <Pagination className="mx-0 w-auto justify-start">
      <PaginationContent>
        <PaginationItem>
          <PaginationPrevious
            text="Trước"
            disabled={page === 1}
            onClick={() => goTo(page - 1)}
            className="cursor-pointer"
          />
        </PaginationItem>

        {pageItems.map((item) =>
          typeof item === "number" ? (
            <PaginationItem key={item}>
              <PaginationLink
                isActive={item === page}
                onClick={() => goTo(item)}
                className="cursor-pointer"
              >
                {item}
              </PaginationLink>
            </PaginationItem>
          ) : (
            <PaginationItem key={item}>
              <PaginationEllipsis />
            </PaginationItem>
          )
        )}

        <PaginationItem>
          <PaginationNext
            text="Sau"
            disabled={page === totalPages}
            onClick={() => goTo(page + 1)}
            className="cursor-pointer"
          />
        </PaginationItem>
      </PaginationContent>
    </Pagination>
  );
};

export default TaskListPagination;
