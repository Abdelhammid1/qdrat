namespace QdratNew.Helpers
{
    public static class RoleNamesHelper
    {
        public static readonly Dictionary<string, string> RoleDisplay = new()
        {
            // 🔹 أدوار الإدارة العليا
            ["Owner"] = "المالك",
            ["SuperAdmin"] = "الأدمن العام",
            ["Admin"] = "الأدمن",
            ["Developer"] = "المبرمج",
            ["Employee"] = "الموظف",
            ["DataEntry"] = "مدخل البيانات",

            // 🔹 أدوار الشريك
            ["Partner"] = "الشريك",
            ["PartnerAdmin"] = "مدير الشريك",
            ["PartnerInstructor"] = "مدرب الشريك",

            // 🔹 أدوار التشغيل
            ["Instructor"] = "المدرب",
            ["Student"] = "الطالب",
            ["Parent"] = "ولي الأمر"
        };
    }
}
