using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    public partial class AddReviewSecondsToExamStudentStatus : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // فقط إضافة العمود الجديد لجدول ExamStudentStatuses
            migrationBuilder.AddColumn<int>(
                name: "ReviewSeconds",
                table: "ExamStudentStatuses",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // حذف العمود عند العودة للوراء
            migrationBuilder.DropColumn(
                name: "ReviewSeconds",
                table: "ExamStudentStatuses");
        }
    }
}
