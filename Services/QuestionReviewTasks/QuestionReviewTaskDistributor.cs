namespace QdratNew.Services.QuestionReviewTasks
{
    /// <summary>
    /// QRT-S7.2: خوارزمية التوزيع بالتساوي مع مراعاة العبء الحالي — دالة نقية قابلة للاختبار بلا قاعدة بيانات.
    /// كل سؤال يذهب للمدرب صاحب أقل (عبء حالي + ما أُسند له في هذا التوزيع)، فيأخذ الأقل حجزًا الباقي.
    /// </summary>
    public static class QuestionReviewTaskDistributor
    {
        /// <summary>
        /// يرجع عدد الأسئلة لكل مدرب بنفس ترتيب <paramref name="currentLoads"/>.
        /// عند التعادل يفوز الأقدم ترتيبًا (المُرتَّبون مسبقًا حسب العبء ثم الاسم).
        /// </summary>
        public static int[] Allocate(int total, IReadOnlyList<int> currentLoads)
        {
            var counts = new int[currentLoads.Count];
            if (total <= 0 || counts.Length == 0)
                return counts;

            for (var n = 0; n < total; n++)
            {
                var best = 0;
                var bestLoad = currentLoads[0] + counts[0];
                for (var i = 1; i < counts.Length; i++)
                {
                    var load = currentLoads[i] + counts[i];
                    if (load < bestLoad)
                    {
                        best = i;
                        bestLoad = load;
                    }
                }
                counts[best]++;
            }

            return counts;
        }
    }
}
