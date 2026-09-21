namespace QdratNew.Entities
{
    public class StudentViewModel
    {
        public int StudentID { get; set; }
        public required string FullName { get; set; }
        public required string NationalID { get; set; }
        public required string Email { get; set; }
        public required string PhoneNumber { get; set; }
    }
}
