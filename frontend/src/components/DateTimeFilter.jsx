import React from "react";
import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectLabel,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { options } from "@/lib/data";

const DateTimeFilter = ({ dateQuery, setDateQuery }) => {
  return (
    <Select items={options} value={dateQuery} onValueChange={setDateQuery}>
      {/* w-[160px] để nút không co giãn mỗi khi đổi lựa chọn */}
      <SelectTrigger size="default" className="w-[105px] bg-white/50">
        <SelectValue />
      </SelectTrigger>

      <SelectContent>
        <SelectGroup>
          <SelectLabel>Lọc theo thời gian</SelectLabel>
          {options.map((option) => (
            <SelectItem key={option.value} value={option.value}>
              {option.label}
            </SelectItem>
          ))}
        </SelectGroup>
      </SelectContent>
    </Select>
  );
};

export default DateTimeFilter;
