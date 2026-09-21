using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddInstructorCriticalQuestionTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InstructorCriticalQuestionTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InstructorId = table.Column<int>(type: "int", nullable: false),
                    BatchId = table.Column<int>(type: "int", nullable: false),
                    LessonId = table.Column<int>(type: "int", nullable: false),
                    CurriculumId = table.Column<int>(type: "int", nullable: true),
                    SectionId = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    AdminNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    InstructorNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CriticalQuestionsCount = table.Column<int>(type: "int", nullable: false),
                    AffectedStudentsCount = table.Column<int>(type: "int", nullable: false),
                    AverageErrorPercentage = table.Column<double>(type: "float", nullable: false),
                    IsSent = table.Column<bool>(type: "bit", nullable: false),
                    IsReviewedByInstructor = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstructorCriticalQuestionTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstructorCriticalQuestionTasks_Batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "Batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InstructorCriticalQuestionTasks_Curriculums_CurriculumId",
                        column: x => x.CurriculumId,
                        principalTable: "Curriculums",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InstructorCriticalQuestionTasks_Instructors_InstructorId",
                        column: x => x.InstructorId,
                        principalTable: "Instructors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InstructorCriticalQuestionTasks_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InstructorCriticalQuestionTasks_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InstructorCriticalQuestionTaskItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InstructorCriticalQuestionTaskId = table.Column<int>(type: "int", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LessonId = table.Column<int>(type: "int", nullable: false),
                    SectionId = table.Column<int>(type: "int", nullable: true),
                    HomeworkSetId = table.Column<int>(type: "int", nullable: true),
                    ExamAssignmentId = table.Column<int>(type: "int", nullable: true),
                    ExamId = table.Column<int>(type: "int", nullable: true),
                    PerformanceIndicatorExamId = table.Column<int>(type: "int", nullable: true),
                    TotalAttempts = table.Column<int>(type: "int", nullable: false),
                    WrongAttempts = table.Column<int>(type: "int", nullable: false),
                    CorrectAttempts = table.Column<int>(type: "int", nullable: false),
                    AffectedStudentsCount = table.Column<int>(type: "int", nullable: false),
                    ErrorPercentage = table.Column<double>(type: "float", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    QuestionTitleSnapshot = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ReferenceNumberSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstructorCriticalQuestionTaskItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstructorCriticalQuestionTaskItems_InstructorCriticalQuestionTasks_InstructorCriticalQuestionTaskId",
                        column: x => x.InstructorCriticalQuestionTaskId,
                        principalTable: "InstructorCriticalQuestionTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InstructorCriticalQuestionTaskItems_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InstructorCriticalQuestionTaskItems_InstructorCriticalQuestionTaskId",
                table: "InstructorCriticalQuestionTaskItems",
                column: "InstructorCriticalQuestionTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_InstructorCriticalQuestionTaskItems_QuestionId",
                table: "InstructorCriticalQuestionTaskItems",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_InstructorCriticalQuestionTasks_BatchId",
                table: "InstructorCriticalQuestionTasks",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_InstructorCriticalQuestionTasks_CurriculumId",
                table: "InstructorCriticalQuestionTasks",
                column: "CurriculumId");

            migrationBuilder.CreateIndex(
                name: "IX_InstructorCriticalQuestionTasks_InstructorId",
                table: "InstructorCriticalQuestionTasks",
                column: "InstructorId");

            migrationBuilder.CreateIndex(
                name: "IX_InstructorCriticalQuestionTasks_LessonId",
                table: "InstructorCriticalQuestionTasks",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_InstructorCriticalQuestionTasks_SectionId",
                table: "InstructorCriticalQuestionTasks",
                column: "SectionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InstructorCriticalQuestionTaskItems");

            migrationBuilder.DropTable(
                name: "InstructorCriticalQuestionTasks");
        }
    }
}
