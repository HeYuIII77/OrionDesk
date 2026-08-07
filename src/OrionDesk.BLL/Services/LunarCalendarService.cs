using System;
using System.Collections.Generic;

namespace OrionDesk.BLL.Services
{
    /// <summary>
    /// 农历服务 - 提供农历日期计算
    /// </summary>
    public class LunarCalendarService
    {
        // 农历数据表（1900-2100年）
        private static readonly int[] LunarInfo = {
            0x04bd8, 0x04ae0, 0x0a570, 0x054d5, 0x0d260, 0x0d950, 0x16554, 0x056a0, 0x09ad0, 0x055d2,
            0x04ae0, 0x0a5b6, 0x0a4d0, 0x0d250, 0x1d255, 0x0b540, 0x0d6a0, 0x0ada2, 0x095b0, 0x14977,
            0x04970, 0x0a4b0, 0x0b4b5, 0x06a50, 0x06d40, 0x1ab54, 0x02b60, 0x09570, 0x052f2, 0x04970,
            0x06566, 0x0d4a0, 0x0ea50, 0x06e95, 0x05ad0, 0x02b60, 0x186e3, 0x092e0, 0x1c8d7, 0x0c950,
            0x0d4a0, 0x1d8a6, 0x0b550, 0x056a0, 0x1a5b4, 0x025d0, 0x092d0, 0x0d2b2, 0x0a950, 0x0b557,
            0x06ca0, 0x0b550, 0x15355, 0x04da0, 0x0a5b0, 0x14573, 0x052b0, 0x0a9a8, 0x0e950, 0x06aa0,
            0x0aea6, 0x0ab50, 0x04b60, 0x0aae4, 0x0a570, 0x05260, 0x0f263, 0x0d950, 0x05b57, 0x056a0,
            0x096d0, 0x04dd5, 0x04ad0, 0x0a4d0, 0x0d4d4, 0x0d250, 0x0d558, 0x0b540, 0x0b6a0, 0x195a6,
            0x095b0, 0x049b0, 0x0a974, 0x0a4b0, 0x0b27a, 0x06a50, 0x06d40, 0x0af46, 0x0ab60, 0x09570,
            0x04af5, 0x04970, 0x064b0, 0x074a3, 0x0ea50, 0x06b58, 0x055c0, 0x0ab60, 0x096d5, 0x092e0,
            0x0c960, 0x0d954, 0x0d4a0, 0x0da50, 0x07552, 0x056a0, 0x0abb7, 0x025d0, 0x092d0, 0x0cab5,
            0x0a950, 0x0b4a0, 0x0baa4, 0x0ad50, 0x055d9, 0x04ba0, 0x0a5b0, 0x15176, 0x052b0, 0x0a930,
            0x07954, 0x06aa0, 0x0ad50, 0x05b52, 0x04b60, 0x0a6e6, 0x0a4e0, 0x0d260, 0x0ea65, 0x0d530,
            0x05aa0, 0x076a3, 0x096d0, 0x04afb, 0x04ad0, 0x0a4d0, 0x1d0b6, 0x0d250, 0x0d520, 0x0dd45,
            0x0b5a0, 0x056d0, 0x055b2, 0x049b0, 0x0a577, 0x0a4b0, 0x0aa50, 0x1b255, 0x06d20, 0x0ada0,
            0x14b63, 0x09370, 0x049f8, 0x04970, 0x064b0, 0x168a6, 0x0ea50, 0x06b20, 0x1a6c4, 0x0aae0,
            0x092e0, 0x0d2e3, 0x0c960, 0x0d557, 0x0d4a0, 0x0da50, 0x05d55, 0x056a0, 0x0a6d0, 0x055d4,
            0x052d0, 0x0a9b8, 0x0a950, 0x0b4a0, 0x0b6a6, 0x0ad50, 0x055a0, 0x0aba4, 0x0a5b0, 0x052b0,
            0x0b273, 0x06930, 0x07337, 0x06aa0, 0x0ad50, 0x14b55, 0x04b60, 0x0a570, 0x054e4, 0x0d160,
            0x0e968, 0x0d520, 0x0daa0, 0x16aa6, 0x056d0, 0x04ae0, 0x0a9d4, 0x0a2d0, 0x0d150, 0x0f252,
            0x0d520
        };

        // 天干地支
        private static readonly string[] HeavenlyStems = { "甲", "乙", "丙", "丁", "戊", "己", "庚", "辛", "壬", "癸" };
        private static readonly string[] EarthlyBranches = { "子", "丑", "寅", "卯", "辰", "巳", "午", "未", "申", "酉", "戌", "亥" };
        private static readonly string[] ChineseZodiac = { "鼠", "牛", "虎", "兔", "龙", "蛇", "马", "羊", "猴", "鸡", "狗", "猪" };

        // 农历月份名称
        private static readonly string[] LunarMonthNames = { "正", "二", "三", "四", "五", "六", "七", "八", "九", "十", "冬", "腊" };
        private static readonly string[] LunarDayNames = {
            "初一", "初二", "初三", "初四", "初五", "初六", "初七", "初八", "初九", "初十",
            "十一", "十二", "十三", "十四", "十五", "十六", "十七", "十八", "十九", "二十",
            "廿一", "廿二", "廿三", "廿四", "廿五", "廿六", "廿七", "廿八", "廿九", "三十"
        };

        /// <summary>
        /// 获取农历日期
        /// </summary>
        public LunarDate GetLunarDate(DateTime solarDate)
        {
            int year = solarDate.Year;
            int month = solarDate.Month;
            int day = solarDate.Day;

            // 计算距离1900年1月31日的天数
            int offset = (int)(new DateTime(year, month, day) - new DateTime(1900, 1, 31)).TotalDays;

            // 计算农历年份
            int lunarYear = 1900;
            int daysInYear = 0;
            for (lunarYear = 1900; lunarYear < 2101 && offset > 0; lunarYear++)
            {
                daysInYear = DaysInLunarYear(lunarYear);
                offset -= daysInYear;
            }
            if (offset < 0)
            {
                offset += daysInYear;
                lunarYear--;
            }

            // 计算闰月
            int leapMonth = LeapMonth(lunarYear);
            bool isLeap = false;

            // 计算农历月份
            int lunarMonth = 1;
            int daysInMonth = 0;
            for (lunarMonth = 1; lunarMonth < 13 && offset > 0; lunarMonth++)
            {
                // 闰月
                if (leapMonth > 0 && lunarMonth == leapMonth + 1 && !isLeap)
                {
                    --lunarMonth;
                    isLeap = true;
                    daysInMonth = LeapDays(lunarYear);
                }
                else
                {
                    daysInMonth = MonthDays(lunarYear, lunarMonth);
                }

                if (isLeap && lunarMonth == leapMonth + 1)
                {
                    isLeap = false;
                }

                offset -= daysInMonth;
            }

            if (offset == 0 && leapMonth > 0 && lunarMonth == leapMonth + 1)
            {
                if (isLeap)
                {
                    isLeap = false;
                }
                else
                {
                    isLeap = true;
                    --lunarMonth;
                }
            }

            if (offset < 0)
            {
                offset += daysInMonth;
                --lunarMonth;
            }

            int lunarDay = offset + 1;

            // 获取干支年
            int stemIndex = (lunarYear - 4) % 10;
            int branchIndex = (lunarYear - 4) % 12;
            string stemBranch = HeavenlyStems[stemIndex] + EarthlyBranches[branchIndex];
            string zodiac = ChineseZodiac[branchIndex];

            return new LunarDate
            {
                Year = lunarYear,
                Month = lunarMonth,
                Day = lunarDay,
                IsLeapMonth = isLeap,
                MonthName = (isLeap ? "闰" : "") + LunarMonthNames[lunarMonth - 1] + "月",
                DayName = LunarDayNames[lunarDay - 1],
                StemBranchYear = stemBranch,
                Zodiac = zodiac
            };
        }

        #region 私有方法

        private int DaysInLunarYear(int year)
        {
            int sum = 348;
            int i = 0x8000;
            int info = LunarInfo[year - 1900];
            while (i > 0x8)
            {
                if ((info & i) != 0) sum += 1;
                i >>= 1;
            }
            return sum + LeapDays(year);
        }

        private int MonthDays(int year, int month)
        {
            if (month > 12 || month < 1) return -1;
            int bit = 1 << (16 - month);
            if ((LunarInfo[year - 1900] & bit) == 0) return 29;
            return 30;
        }

        private int LeapDays(int year)
        {
            if (LeapMonth(year) != 0)
            {
                if ((LunarInfo[year - 1900] & 0x10000) != 0) return 30;
                return 29;
            }
            return 0;
        }

        private int LeapMonth(int year)
        {
            return LunarInfo[year - 1900] & 0xf;
        }

        #endregion
    }

    /// <summary>
    /// 农历日期
    /// </summary>
    public class LunarDate
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int Day { get; set; }
        public bool IsLeapMonth { get; set; }
        public string MonthName { get; set; } = "";
        public string DayName { get; set; } = "";
        public string StemBranchYear { get; set; } = "";
        public string Zodiac { get; set; } = "";
    }
}
