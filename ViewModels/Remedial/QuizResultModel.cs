namespace QdratNew.ViewModels.Remedial
{
    public class QuizResultModel
    {
        public int StudentId { get; set; }
        public int QuizId { get; set; }
        public int VideoId { get; set; }   // ✅ تمت الإضافة هنا

        public string SelectedAnswer { get; set; }
        public bool IsCorrect { get; set; }
    }
}
