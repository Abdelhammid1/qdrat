using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddExamAssignmentToStudentToExamQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExamAssignmentToStudentId",
                table: "ExamQuestions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExamQuestions_ExamAssignmentToStudentId",
                table: "ExamQuestions",
                column: "ExamAssignmentToStudentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ExamQuestions_ExamAssignmentsToStudents_ExamAssignmentToStudentId",
                table: "ExamQuestions",
                column: "ExamAssignmentToStudentId",
                principalTable: "ExamAssignmentsToStudents",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExamQuestions_ExamAssignmentsToStudents_ExamAssignmentToStudentId",
                table: "ExamQuestions");

            migrationBuilder.DropIndex(
                name: "IX_ExamQuestions_ExamAssignmentToStudentId",
                table: "ExamQuestions");

            migrationBuilder.DropColumn(
                name: "ExamAssignmentToStudentId",
                table: "ExamQuestions");
        }
    }
}
