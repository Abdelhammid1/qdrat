namespace QdratNew.Security.Permissions
{
    public class AdminActionDescriptor
    {
        public string Key { get; set; } = null!;
        public string DisplayNameAr { get; set; } = null!;
    }

    public static class AdminControllerActionCatalog
    {
        public static IReadOnlyList<AdminActionDescriptor> GetActions(string controllerName)
        {
            return controllerName switch
            {
                "Questions" => new[]
                {
                    new AdminActionDescriptor { Key = "Read", DisplayNameAr = "عرض بنك الأسئلة" },
                    new AdminActionDescriptor { Key = "Add", DisplayNameAr = "إضافة سؤال" },
                    new AdminActionDescriptor { Key = "Edit", DisplayNameAr = "تعديل سؤال" },
                    new AdminActionDescriptor { Key = "Delete", DisplayNameAr = "حذف سؤال" },
                    new AdminActionDescriptor { Key = "Approve", DisplayNameAr = "اعتماد سؤال" },
                    new AdminActionDescriptor { Key = "Reject", DisplayNameAr = "رفض سؤال" },
                    new AdminActionDescriptor { Key = "Preview", DisplayNameAr = "معاينة سؤال" },
                    new AdminActionDescriptor { Key = "ViewAudit", DisplayNameAr = "عرض سجل التعديلات" }
                },

                "Curriculums" => new[]
                {
                    new AdminActionDescriptor { Key = "Read", DisplayNameAr = "عرض المناهج" },
                    new AdminActionDescriptor { Key = "FilterList", DisplayNameAr = "تحميل قائمة المناهج داخل الفلاتر" }
                },

                "Sections" => new[]
                {
                    new AdminActionDescriptor { Key = "Read", DisplayNameAr = "عرض المحاور" },
                    new AdminActionDescriptor { Key = "FilterList", DisplayNameAr = "تحميل قائمة المحاور داخل الفلاتر" }
                },

                "Lessons" => new[]
                {
                    new AdminActionDescriptor { Key = "Read", DisplayNameAr = "عرض المؤشرات" },
                    new AdminActionDescriptor { Key = "FilterList", DisplayNameAr = "تحميل قائمة المؤشرات داخل الفلاتر" }
                },

                "ExamAssignments" => new[]
                {
                    new AdminActionDescriptor { Key = "Read",    DisplayNameAr = "عرض الاختبارات" },
                    new AdminActionDescriptor { Key = "Details",  DisplayNameAr = "تفاصيل الاختبار وأسئلته" },
                    new AdminActionDescriptor { Key = "Generate", DisplayNameAr = "توليد اختبار" },
                    new AdminActionDescriptor { Key = "Edit",     DisplayNameAr = "تعديل اختبار" },
                    new AdminActionDescriptor { Key = "Publish",  DisplayNameAr = "إرسال / إعادة إرسال اختبار" },
                    new AdminActionDescriptor { Key = "Results",  DisplayNameAr = "عرض النتائج" },
                    new AdminActionDescriptor { Key = "Delete",   DisplayNameAr = "حذف اختبار" },
                    new AdminActionDescriptor { Key = "Archive",  DisplayNameAr = "أرشفة الاختبارات واسترجاعها" }
                },

                "ExamIndividualAssignments" => new[]
                {
                    new AdminActionDescriptor { Key = "Read",    DisplayNameAr = "عرض الاختبارات الفردية" },
                    new AdminActionDescriptor { Key = "Details", DisplayNameAr = "تفاصيل الاختبار الفردي وأسئلته" },
                    new AdminActionDescriptor { Key = "Create",  DisplayNameAr = "إنشاء اختبار فردي" },
                    new AdminActionDescriptor { Key = "Edit",    DisplayNameAr = "تعديل / استبدال أسئلة اختبار فردي" },
                    new AdminActionDescriptor { Key = "Delete",  DisplayNameAr = "حذف اختبار فردي" }
                },

                "ExamGenerator" => new[]
                {
                    new AdminActionDescriptor { Key = "Generate", DisplayNameAr = "توليد اختبار تلقائي" },
                    new AdminActionDescriptor { Key = "Preview", DisplayNameAr = "معاينة التوليد" },
                    new AdminActionDescriptor { Key = "Confirm", DisplayNameAr = "تأكيد التوليد" }
                },

                "AdminLessonCompletions" => new[]
                {
                    new AdminActionDescriptor { Key = "Read", DisplayNameAr = "عرض البيانات" },
                    new AdminActionDescriptor { Key = "GenerateHomework", DisplayNameAr = "تسجيل المؤشرات المنتهية للدفعة" },
                    new AdminActionDescriptor { Key = "ManageModelHomework", DisplayNameAr = "إدارة واجبات النماذج" },
                    new AdminActionDescriptor { Key = "ManageModelExam", DisplayNameAr = "إدارة اختبارات النماذج" },
                    new AdminActionDescriptor { Key = "Enhancement", DisplayNameAr = "إدارة المهارات التعزيزية" },
                    new AdminActionDescriptor { Key = "Delete", DisplayNameAr = "حذف نهائي" }
                },

                "PlacementExams" => new[]
                {
                    new AdminActionDescriptor { Key = "Read", DisplayNameAr = "عرض اختبارات تحديد المستوى" },
                    new AdminActionDescriptor { Key = "Results", DisplayNameAr = "عرض نتائج وتحليلات اختبار المستوى" },
                    new AdminActionDescriptor { Key = "Create", DisplayNameAr = "إنشاء اختبارات تحديد المستوى" },
                    new AdminActionDescriptor { Key = "Delete", DisplayNameAr = "إخفاء / استرجاع اختبارات المستوى" },
                    new AdminActionDescriptor { Key = "Archive", DisplayNameAr = "أرشفة اختبارات تحديد المستوى واسترجاعها" }
                },

                "Lectures" => new[]
                {
                    new AdminActionDescriptor { Key = "Read", DisplayNameAr = "عرض المحاضرات" },
                    new AdminActionDescriptor { Key = "Create", DisplayNameAr = "إضافة محاضرة" },
                    new AdminActionDescriptor { Key = "Edit", DisplayNameAr = "تعديل محاضرة" },
                    new AdminActionDescriptor { Key = "Delete", DisplayNameAr = "حذف محاضرة" }
                },

                "HomeworkManagement" => new[]
                {
                    new AdminActionDescriptor { Key = "Read", DisplayNameAr = "عرض الواجبات" },
                    new AdminActionDescriptor { Key = "Reports", DisplayNameAr = "عرض تقارير الواجبات وتحليل التلاعب" },
                    new AdminActionDescriptor { Key = "Details", DisplayNameAr = "تفاصيل الواجب" },
                    new AdminActionDescriptor { Key = "Review", DisplayNameAr = "مراجعة حل طالب" },
                    new AdminActionDescriptor { Key = "ManageQuestions", DisplayNameAr = "اختيار أسئلة الواجب" },
                    new AdminActionDescriptor { Key = "AddQuestion", DisplayNameAr = "إضافة سؤال للواجب" },
                    new AdminActionDescriptor { Key = "RemoveQuestion", DisplayNameAr = "حذف سؤال من الواجب" },
                    new AdminActionDescriptor { Key = "Resend", DisplayNameAr = "إعادة إرسال الواجب" },
                    new AdminActionDescriptor { Key = "Archive", DisplayNameAr = "أرشفة / استرجاع واجب" },
                    new AdminActionDescriptor { Key = "Delete", DisplayNameAr = "حذف واجب" }
                },

                "PerformanceDashboard" => new[]
                {
                    new AdminActionDescriptor { Key = "Read", DisplayNameAr = "عرض لوحة مؤشرات الأداء" },
                    new AdminActionDescriptor { Key = "BatchDetails", DisplayNameAr = "تفاصيل أداء دفعة" },
                    new AdminActionDescriptor { Key = "ExamReport", DisplayNameAr = "تقرير اختبار" },
                    new AdminActionDescriptor { Key = "ExamAnalytics", DisplayNameAr = "تحليل اختبار" }
                },

                "PerformanceExamReports" => new[]
                {
                    new AdminActionDescriptor { Key = "Read", DisplayNameAr = "عرض تقارير الأداء" },
                    new AdminActionDescriptor { Key = "StudentReport", DisplayNameAr = "تقرير طالب" },
                    new AdminActionDescriptor { Key = "BatchReport", DisplayNameAr = "تقرير دفعة" }
                },

                "PerformanceIndicatorExams" => new[]
                {
                    new AdminActionDescriptor { Key = "Read", DisplayNameAr = "عرض اختبارات مؤشر الأداء" },
                    new AdminActionDescriptor { Key = "Add", DisplayNameAr = "إنشاء اختبار مؤشر أداء" },
                    new AdminActionDescriptor { Key = "Edit", DisplayNameAr = "تعديل أسئلة الاختبار" },
                    new AdminActionDescriptor { Key = "CreateFromProfessionalModel", DisplayNameAr = "إنشاء من نموذج احترافي" },
                    new AdminActionDescriptor { Key = "ConfirmSend", DisplayNameAr = "إرسال الاختبار" },
                    new AdminActionDescriptor { Key = "Results", DisplayNameAr = "عرض نتائج الاختبار" },
                    new AdminActionDescriptor { Key = "Delete", DisplayNameAr = "حذف اختبار" },
                    new AdminActionDescriptor { Key = "Archive", DisplayNameAr = "أرشفة اختبارات مؤشر الأداء واسترجاعها" }
                },

                "VerbalPassages" => new[]
                {
                    new AdminActionDescriptor { Key = "Read", DisplayNameAr = "عرض القطع اللفظية" },
                    new AdminActionDescriptor { Key = "Create", DisplayNameAr = "إضافة قطعة لفظية" },
                    new AdminActionDescriptor { Key = "Edit", DisplayNameAr = "تعديل قطعة لفظية" },
                    new AdminActionDescriptor { Key = "Delete", DisplayNameAr = "حذف قطعة لفظية" }
                },

                "ProfessionalModels" => new[]
   {
    new AdminActionDescriptor { Key = "Read", DisplayNameAr = "عرض النماذج الاحترافية" },
    new AdminActionDescriptor { Key = "CreateModel", DisplayNameAr = "إنشاء نموذج احترافي" },
    new AdminActionDescriptor { Key = "EditModel", DisplayNameAr = "تعديل نموذج احترافي وإضافة الأسئلة" },
    new AdminActionDescriptor { Key = "DeleteModel", DisplayNameAr = "حذف نموذج احترافي" },
    new AdminActionDescriptor { Key = "ApproveModel", DisplayNameAr = "اعتماد نموذج احترافي" },
    new AdminActionDescriptor { Key = "Visibility", DisplayNameAr = "صلاحيات ظهور نموذج احترافي" },
    new AdminActionDescriptor { Key = "Duplicate", DisplayNameAr = "تكرار النموذج" },
    new AdminActionDescriptor { Key = "RejectModel", DisplayNameAr = "رفض نموذج احترافي" }
},

                "Attendance" => new[]
                {
                    new AdminActionDescriptor { Key = "Read", DisplayNameAr = "عرض الحضور" },
                    new AdminActionDescriptor { Key = "Mark", DisplayNameAr = "تسجيل الحضور" },
                    new AdminActionDescriptor { Key = "Students", DisplayNameAr = "عرض حضور الطلاب" },
                    new AdminActionDescriptor { Key = "Archive", DisplayNameAr = "أرشفة الحضور واسترجاعه" }
                },

                "EnhancementSkills" => new[]
                {
                    new AdminActionDescriptor { Key = "Read", DisplayNameAr = "عرض المهارات التعزيزية" },
                    new AdminActionDescriptor { Key = "Create", DisplayNameAr = "إنشاء مجموعة مهارات تعزيزية" },
                    new AdminActionDescriptor { Key = "Edit", DisplayNameAr = "تعديل المجموعة وإعادة التوليد" },
                    new AdminActionDescriptor { Key = "Send", DisplayNameAr = "إرسال المهارات التعزيزية للطلاب" },
                    new AdminActionDescriptor { Key = "Resend", DisplayNameAr = "إعادة إرسال مهارات لطالب محدد" },
                    new AdminActionDescriptor { Key = "Delete", DisplayNameAr = "حذف مجموعة مهارات تعزيزية" },
                    new AdminActionDescriptor { Key = "Archive", DisplayNameAr = "أرشفة المهارات التعزيزية واسترجاعها" }
                },

                "Settings" => new[]
                {
                    new AdminActionDescriptor { Key = "EditSettings", DisplayNameAr = "تعديل الإعدادات" }
                },

                "Batches" => new[]
                {
                    new AdminActionDescriptor { Key = "Read",    DisplayNameAr = "عرض الدفعات" },
                    new AdminActionDescriptor { Key = "Details", DisplayNameAr = "تفاصيل الدفعة والطلاب" },
                    new AdminActionDescriptor { Key = "Create",  DisplayNameAr = "إنشاء دفعة" },
                    new AdminActionDescriptor { Key = "Edit",    DisplayNameAr = "تعديل دفعة" },
                    new AdminActionDescriptor { Key = "Delete",  DisplayNameAr = "حذف دفعة" },
                    new AdminActionDescriptor { Key = "Archive", DisplayNameAr = "أرشفة الدفعات واسترجاعها" },
                    new AdminActionDescriptor { Key = "Students", DisplayNameAr = "إدارة طلاب الدفعة" },
                    new AdminActionDescriptor { Key = "Send",    DisplayNameAr = "إرسال رسائل للدفعة" }
                },

                "IntegrityViolations" => new[]
                {
                    new AdminActionDescriptor { Key = "Read", DisplayNameAr = "عرض مخالفات النزاهة" },
                    new AdminActionDescriptor { Key = "Resolve", DisplayNameAr = "إعادة فتح محاولة موقوفة" }
                },

                _ => Array.Empty<AdminActionDescriptor>()
            };
        }
    }
}
