using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddExamAssignmentToStudentToExamStudentStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Guidance",
                table: "StudentIndicatorResults",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "ExamAssignmentToStudentId",
                table: "ExamStudentStatuses",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExamStudentStatuses_ExamAssignmentToStudentId",
                table: "ExamStudentStatuses",
                column: "ExamAssignmentToStudentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ExamStudentStatuses_ExamAssignmentsToStudents_ExamAssignmentToStudentId",
                table: "ExamStudentStatuses",
                column: "ExamAssignmentToStudentId",
                principalTable: "ExamAssignmentsToStudents",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExamStudentStatuses_ExamAssignmentsToStudents_ExamAssignmentToStudentId",
                table: "ExamStudentStatuses");

            migrationBuilder.DropIndex(
                name: "IX_ExamStudentStatuses_ExamAssignmentToStudentId",
                table: "ExamStudentStatuses");

            migrationBuilder.DropColumn(
                name: "ExamAssignmentToStudentId",
                table: "ExamStudentStatuses");

            migrationBuilder.AlterColumn<string>(
                name: "Guidance",
                table: "StudentIndicatorResults",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
