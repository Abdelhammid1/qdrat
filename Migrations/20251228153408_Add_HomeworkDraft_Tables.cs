using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class Add_HomeworkDraft_Tables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HomeworkDrafts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerId = table.Column<int>(type: "int", nullable: false),
                    SubscriptionPeriodId = table.Column<int>(type: "int", nullable: false),
                    CourseId = table.Column<int>(type: "int", nullable: false),
                    SectionId = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QuestionsPerLesson = table.Column<int>(type: "int", nullable: false),
                    UsePlatformQuestionBank = table.Column<bool>(type: "bit", nullable: false),
                    UsePrivateQuestionBank = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeworkDrafts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HomeworkDraftQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HomeworkDraftId = table.Column<int>(type: "int", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LessonId = table.Column<int>(type: "int", nullable: false),
                    SectionId = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeworkDraftQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HomeworkDraftQuestions_HomeworkDrafts_HomeworkDraftId",
                        column: x => x.HomeworkDraftId,
                        principalTable: "HomeworkDrafts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HomeworkDraftQuestions_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkDraftQuestions_HomeworkDraftId",
                table: "HomeworkDraftQuestions",
                column: "HomeworkDraftId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkDraftQuestions_QuestionId",
                table: "HomeworkDraftQuestions",
                column: "QuestionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HomeworkDraftQuestions");

            migrationBuilder.DropTable(
                name: "HomeworkDrafts");
        }
    }
}
