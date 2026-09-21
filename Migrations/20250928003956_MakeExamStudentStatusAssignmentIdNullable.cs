using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class MakeExamStudentStatusAssignmentIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExamStudentStatuses_ExamAssignmentsToBatches_ExamAssignmentId",
                table: "ExamStudentStatuses");

            migrationBuilder.AlterColumn<int>(
                name: "ExamAssignmentId",
                table: "ExamStudentStatuses",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_ExamStudentStatuses_ExamAssignmentsToBatches_ExamAssignmentId",
                table: "ExamStudentStatuses",
                column: "ExamAssignmentId",
                principalTable: "ExamAssignmentsToBatches",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExamStudentStatuses_ExamAssignmentsToBatches_ExamAssignmentId",
                table: "ExamStudentStatuses");

            migrationBuilder.AlterColumn<int>(
                name: "ExamAssignmentId",
                table: "ExamStudentStatuses",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamStudentStatuses_ExamAssignmentsToBatches_ExamAssignmentId",
                table: "ExamStudentStatuses",
                column: "ExamAssignmentId",
                principalTable: "ExamAssignmentsToBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
