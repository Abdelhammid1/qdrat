using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.Helpers
{
    public static class DayNameHelper
    {
        public static string GetArabicDay(DayOfWeek day)
        {
            return day switch
            {
                DayOfWeek.Saturday => "السبت",
                DayOfWeek.Sunday => "الأحد",
                DayOfWeek.Monday => "الإثنين",
                DayOfWeek.Tuesday => "الثلاثاء",
                DayOfWeek.Wednesday => "الأربعاء",
                DayOfWeek.Thursday => "الخميس",
                DayOfWeek.Friday => "الجمعة",
                _ => "غير معروف"
            };
        }

        // ✅ هذه الدالة ترجع قائمة SelectListItem مباشرة لاستخدامها في DropDownList
        public static List<SelectListItem> GetArabicDays()
        {
            return Enum.GetValues(typeof(DayOfWeek))
                .Cast<DayOfWeek>()
                .Select(d => new SelectListItem
                {
                    Value = ((int)d).ToString(),
                    Text = GetArabicDay(d)
                }).ToList();
        }
    }
}
