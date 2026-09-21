using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace QdratNew.Helpers
{
    public static class EnumExtensions
    {
        public static string GetDisplayName(this Enum enumValue)
        {
            return enumValue.GetType()
                .GetMember(enumValue.ToString())
                .First()
                .GetCustomAttributes(false)
                .OfType<DisplayAttribute>()
                .FirstOrDefault()?.Name ?? enumValue.ToString();
        }

        public static List<string> GetDisplayNamesFromFlags<TEnum>(this TEnum flags)
            where TEnum : Enum
        {
            return Enum.GetValues(typeof(TEnum))
                .Cast<TEnum>()
                .Where(v => flags.HasFlag(v))
                .Where(v => Convert.ToInt32(v) != 0)
                .Select(v => (v as Enum).GetDisplayName())
                .ToList();
        }



    }
}
