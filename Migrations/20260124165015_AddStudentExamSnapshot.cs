using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentExamSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TotalSolveMinutes",
                table: "StudentExamSnapshots",
                newName: "AverageSolveMinutes");

            migrationBuilder.AddColumn<int>(
                name: "CompletedExams",
                table: "StudentExamSnapshots",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LateExams",
                table: "StudentExamSnapshots",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MistakesCount",
                table: "StudentExamSnapshots",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PendingExams",
                table: "StudentExamSnapshots",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RecommendationsJson",
                table: "StudentExamSnapshots",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotType",
                table: "StudentExamSnapshots",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SnapshotVersion",
                table: "StudentExamSnapshots",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalExams",
                table: "StudentExamSnapshots",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WeakSectionsCount",
                table: "StudentExamSnapshots",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedExams",
                table: "StudentExamSnapshots");

            migrationBuilder.DropColumn(
                name: "LateExams",
                table: "StudentExamSnapshots");

            migrationBuilder.DropColumn(
                name: "MistakesCount",
                table: "StudentExamSnapshots");

            migrationBuilder.DropColumn(
                name: "PendingExams",
                table: "StudentExamSnapshots");

            migrationBuilder.DropColumn(
                name: "RecommendationsJson",
                table: "StudentExamSnapshots");

            migrationBuilder.DropColumn(
                name: "SnapshotType",
                table: "StudentExamSnapshots");

            migrationBuilder.DropColumn(
                name: "SnapshotVersion",
                table: "StudentExamSnapshots");

            migrationBuilder.DropColumn(
                name: "TotalExams",
                table: "StudentExamSnapshots");

            migrationBuilder.DropColumn(
                name: "WeakSectionsCount",
                table: "StudentExamSnapshots");

            migrationBuilder.RenameColumn(
                name: "AverageSolveMinutes",
                table: "StudentExamSnapshots",
                newName: "TotalSolveMinutes");
        }
    }
}
