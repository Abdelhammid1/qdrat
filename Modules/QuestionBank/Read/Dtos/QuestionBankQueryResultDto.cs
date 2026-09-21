namespace QdratNew.Modules.QuestionBank.Read.Dtos
{
    public class QuestionBankQueryResultDto
    {
        public int TotalCount { get; set; }
        public List<QuestionBankItemDto> Items { get; set; } = new();
    }
}
