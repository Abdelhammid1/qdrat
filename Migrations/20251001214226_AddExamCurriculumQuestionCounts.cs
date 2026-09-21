using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddExamCurriculumQuestionCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "SectionId",
                table: "ExamAssignmentsToBatches",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateTable(
                name: "ExamCurriculumQuestionCounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExamAssignmentId = table.Column<int>(type: "int", nullable: false),
                    CurriculumId = table.Column<int>(type: "int", nullable: false),
                    QuestionCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamCurriculumQuestionCounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamCurriculumQuestionCounts_Curriculums_CurriculumId",
                        column: x => x.CurriculumId,
                        principalTable: "Curriculums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExamCurriculumQuestionCounts_ExamAssignmentsToBatches_ExamAssignmentId",
                        column: x => x.ExamAssignmentId,
                        principalTable: "ExamAssignmentsToBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExamCurriculumQuestionCounts_CurriculumId",
                table: "ExamCurriculumQuestionCounts",
                column: "CurriculumId");

            migrationBuilder.CreateIndex(
                name: "IX_ExamCurriculumQuestionCounts_ExamAssignmentId",
                table: "ExamCurriculumQuestionCounts",
                column: "ExamAssignmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExamCurriculumQuestionCounts");

            migrationBuilder.AlterColumn<int>(
                name: "SectionId",
                table: "ExamAssignmentsToBatches",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
