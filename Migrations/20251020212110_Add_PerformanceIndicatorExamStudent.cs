using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class Add_PerformanceIndicatorExamStudent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PerformanceIndicatorExamStudents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PerformanceIndicatorExamId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    ScorePercent = table.Column<double>(type: "float", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceIndicatorExamStudents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerformanceIndicatorExamStudents_PerformanceIndicatorExams_PerformanceIndicatorExamId",
                        column: x => x.PerformanceIndicatorExamId,
                        principalTable: "PerformanceIndicatorExams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PerformanceIndicatorExamStudents_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "StudentID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceIndicatorExamStudents_PerformanceIndicatorExamId",
                table: "PerformanceIndicatorExamStudents",
                column: "PerformanceIndicatorExamId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceIndicatorExamStudents_StudentId",
                table: "PerformanceIndicatorExamStudents",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PerformanceIndicatorExamStudents");
        }
    }
}
