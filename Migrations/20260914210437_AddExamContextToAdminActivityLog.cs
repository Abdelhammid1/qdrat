using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddExamContextToAdminActivityLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExamAssignmentId",
                table: "AdminActivityLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExamAssignmentToStudentId",
                table: "AdminActivityLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImpactJson",
                table: "AdminActivityLogs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "AdminActivityLogs",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExamAssignmentId",
                table: "AdminActivityLogs");

            migrationBuilder.DropColumn(
                name: "ExamAssignmentToStudentId",
                table: "AdminActivityLogs");

            migrationBuilder.DropColumn(
                name: "ImpactJson",
                table: "AdminActivityLogs");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "AdminActivityLogs");
        }
    }
}
