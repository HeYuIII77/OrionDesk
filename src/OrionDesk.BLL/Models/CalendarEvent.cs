namespace OrionDesk.BLL.Models
{
    /// <summary>
    /// 事项类型
    /// </summary>
    public enum EventType
    {
        /// <summary>工作（红色）</summary>
        Work,
        /// <summary>生活（绿色）</summary>
        Life,
        /// <summary>纪念日（蓝色）</summary>
        Anniversary,
        /// <summary>提醒（黄色）</summary>
        Reminder
    }

    /// <summary>
    /// 重复规则
    /// </summary>
    public enum EventRepeat
    {
        /// <summary>不重复</summary>
        None,
        /// <summary>每天</summary>
        Daily,
        /// <summary>每周</summary>
        Weekly,
        /// <summary>每月</summary>
        Monthly,
        /// <summary>每年</summary>
        Yearly
    }

    /// <summary>
    /// 日历事项
    /// </summary>
    public class CalendarEvent
    {
        /// <summary>唯一标识</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

        /// <summary>标题</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>开始时间</summary>
        public DateTime Start { get; set; } = DateTime.Today;

        /// <summary>结束时间（可选）</summary>
        public DateTime? End { get; set; }

        /// <summary>是否全天事项</summary>
        public bool IsAllDay { get; set; } = false;

        /// <summary>事项类型</summary>
        public EventType Type { get; set; } = EventType.Work;

        /// <summary>重复规则</summary>
        public EventRepeat Repeat { get; set; } = EventRepeat.None;

        /// <summary>备注</summary>
        public string Note { get; set; } = string.Empty;

        /// <summary>
        /// 获取事项类型名称
        /// </summary>
        public static string GetTypeName(EventType type)
        {
            return type switch
            {
                EventType.Work => "工作",
                EventType.Life => "生活",
                EventType.Anniversary => "纪念日",
                EventType.Reminder => "提醒",
                _ => "未知"
            };
        }

        /// <summary>
        /// 获取重复规则名称
        /// </summary>
        public static string GetRepeatName(EventRepeat repeat)
        {
            return repeat switch
            {
                EventRepeat.None => "不重复",
                EventRepeat.Daily => "每天",
                EventRepeat.Weekly => "每周",
                EventRepeat.Monthly => "每月",
                EventRepeat.Yearly => "每年",
                _ => "未知"
            };
        }

        /// <summary>
        /// 判断事项是否在指定日期发生（含重复规则展开）
        /// </summary>
        public bool IsOnDate(DateTime date)
        {
            var eventDate = Start.Date;
            var targetDate = date.Date;

            // 未来日期才检查重复；当日及过去只匹配原始日期或重复
            if (targetDate < eventDate) return false;

            return Repeat switch
            {
                EventRepeat.None => eventDate == targetDate,
                EventRepeat.Daily => true,
                EventRepeat.Weekly => targetDate.DayOfWeek == eventDate.DayOfWeek,
                EventRepeat.Monthly => targetDate.Day == eventDate.Day,
                EventRepeat.Yearly => targetDate.Month == eventDate.Month && targetDate.Day == eventDate.Day,
                _ => false
            };
        }
    }
}
