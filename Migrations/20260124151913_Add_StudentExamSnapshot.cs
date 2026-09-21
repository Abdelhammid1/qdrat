using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class Add_StudentExamSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AdminUserProfiles_AdminProfiles_AdminProfileId",
                table: "AdminUserProfiles");

            migrationBuilder.AlterColumn<int>(
                name: "AdminProfileId",
                table: "AdminUserProfiles",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateTable(
                name: "StudentExamSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    CourseId = table.Column<int>(type: "int", nullable: false),
                    BatchId = table.Column<int>(type: "int", nullable: false),
                    ExamAssignmentId = table.Column<int>(type: "int", nullable: true),
                    TotalQuestions = table.Column<int>(type: "int", nullable: false),
                    CorrectAnswers = table.Column<int>(type: "int", nullable: false),
                    WrongAnswers = table.Column<int>(type: "int", nullable: false),
                    SkippedQuestions = table.Column<int>(type: "int", nullable: false),
                    AccuracyPercent = table.Column<double>(type: "float", nullable: false),
                    TotalSolveMinutes = table.Column<double>(type: "float", nullable: false),
                    SectionPerformanceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExamProgressJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SnapshotCreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SnapshotUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentExamSnapshots", x => x.Id);
                });

            migrationBuilder.AddForeignKey(
                name: "FK_AdminUserProfiles_AdminProfiles_AdminProfileId",
                table: "AdminUserProfiles",
                column: "AdminProfileId",
                principalTable: "AdminProfiles",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AdminUserProfiles_AdminProfiles_AdminProfileId",
                table: "AdminUserProfiles");

            migrationBuilder.DropTable(
                name: "StudentExamSnapshots");

            migrationBuilder.AlterColumn<int>(
                name: "AdminProfileId",
                table: "AdminUserProfiles",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AdminUserProfiles_AdminProfiles_AdminProfileId",
                table: "AdminUserProfiles",
                column: "AdminProfileId",
                principalTable: "AdminProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
