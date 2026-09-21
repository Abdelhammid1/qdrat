using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceExamFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RemedialPlanId",
                table: "StudentIndicatorResults",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndAt",
                table: "PerformanceIndicatorExams",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsOnline",
                table: "PerformanceIndicatorExams",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceCode",
                table: "PerformanceIndicatorExams",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "StartAt",
                table: "PerformanceIndicatorExams",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "PerformanceIndicatorExamQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PerformanceIndicatorExamId = table.Column<int>(type: "int", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SectionId = table.Column<int>(type: "int", nullable: false),
                    OrderNumber = table.Column<int>(type: "int", nullable: false),
                    PerformanceIndicatorExamSectionId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceIndicatorExamQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerformanceIndicatorExamQuestions_PerformanceIndicatorExamSections_PerformanceIndicatorExamSectionId",
                        column: x => x.PerformanceIndicatorExamSectionId,
                        principalTable: "PerformanceIndicatorExamSections",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PerformanceIndicatorExamQuestions_PerformanceIndicatorExams_PerformanceIndicatorExamId",
                        column: x => x.PerformanceIndicatorExamId,
                        principalTable: "PerformanceIndicatorExams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PerformanceIndicatorExamQuestions_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PerformanceIndicatorExamQuestions_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PerformanceIndicatorExamToBatch",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PerformanceIndicatorExamId = table.Column<int>(type: "int", nullable: false),
                    BatchId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceIndicatorExamToBatch", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerformanceIndicatorExamToBatch_Batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "Batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PerformanceIndicatorExamToBatch_PerformanceIndicatorExams_PerformanceIndicatorExamId",
                        column: x => x.PerformanceIndicatorExamId,
                        principalTable: "PerformanceIndicatorExams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentIndicatorResults_RemedialPlanId",
                table: "StudentIndicatorResults",
                column: "RemedialPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceIndicatorExamQuestions_PerformanceIndicatorExamId",
                table: "PerformanceIndicatorExamQuestions",
                column: "PerformanceIndicatorExamId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceIndicatorExamQuestions_PerformanceIndicatorExamSectionId",
                table: "PerformanceIndicatorExamQuestions",
                column: "PerformanceIndicatorExamSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceIndicatorExamQuestions_QuestionId",
                table: "PerformanceIndicatorExamQuestions",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceIndicatorExamQuestions_SectionId",
                table: "PerformanceIndicatorExamQuestions",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceIndicatorExamToBatch_BatchId",
                table: "PerformanceIndicatorExamToBatch",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceIndicatorExamToBatch_PerformanceIndicatorExamId",
                table: "PerformanceIndicatorExamToBatch",
                column: "PerformanceIndicatorExamId");

            migrationBuilder.AddForeignKey(
                name: "FK_StudentIndicatorResults_RemedialPlans_RemedialPlanId",
                table: "StudentIndicatorResults",
                column: "RemedialPlanId",
                principalTable: "RemedialPlans",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StudentIndicatorResults_RemedialPlans_RemedialPlanId",
                table: "StudentIndicatorResults");

            migrationBuilder.DropTable(
                name: "PerformanceIndicatorExamQuestions");

            migrationBuilder.DropTable(
                name: "PerformanceIndicatorExamToBatch");

            migrationBuilder.DropIndex(
                name: "IX_StudentIndicatorResults_RemedialPlanId",
                table: "StudentIndicatorResults");

            migrationBuilder.DropColumn(
                name: "RemedialPlanId",
                table: "StudentIndicatorResults");

            migrationBuilder.DropColumn(
                name: "EndAt",
                table: "PerformanceIndicatorExams");

            migrationBuilder.DropColumn(
                name: "IsOnline",
                table: "PerformanceIndicatorExams");

            migrationBuilder.DropColumn(
                name: "ReferenceCode",
                table: "PerformanceIndicatorExams");

            migrationBuilder.DropColumn(
                name: "StartAt",
                table: "PerformanceIndicatorExams");
        }
    }
}
