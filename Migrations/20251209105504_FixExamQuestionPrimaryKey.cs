using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class FixExamQuestionPrimaryKey : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ❶ حذف المفتاح الأساسي القديم
            migrationBuilder.DropPrimaryKey(
                name: "PK_ExamQuestions",
                table: "ExamQuestions");

            // ❷ لا تضف العمود Id لأنه موجود بالفعل
            // migrationBuilder.AddColumn<int>(...) ← نحذفها بالكامل

            // ❸ إضافة المفتاح الأساسي الجديد على Id
            migrationBuilder.AddPrimaryKey(
                name: "PK_ExamQuestions",
                table: "ExamQuestions",
                column: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ExamQuestions",
                table: "ExamQuestions");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ExamQuestions",
                table: "ExamQuestions",
                columns: new[] { "ExamId", "QuestionId" });
        }
    }
}
