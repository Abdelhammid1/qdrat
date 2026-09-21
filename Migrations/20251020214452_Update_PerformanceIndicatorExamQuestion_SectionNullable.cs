using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class Update_PerformanceIndicatorExamQuestion_SectionNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PerformanceIndicatorExamQuestions_Sections_SectionId",
                table: "PerformanceIndicatorExamQuestions");

            migrationBuilder.AlterColumn<int>(
                name: "SectionId",
                table: "PerformanceIndicatorExamQuestions",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_PerformanceIndicatorExamQuestions_Sections_SectionId",
                table: "PerformanceIndicatorExamQuestions",
                column: "SectionId",
                principalTable: "Sections",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PerformanceIndicatorExamQuestions_Sections_SectionId",
                table: "PerformanceIndicatorExamQuestions");

            migrationBuilder.AlterColumn<int>(
                name: "SectionId",
                table: "PerformanceIndicatorExamQuestions",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PerformanceIndicatorExamQuestions_Sections_SectionId",
                table: "PerformanceIndicatorExamQuestions",
                column: "SectionId",
                principalTable: "Sections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
