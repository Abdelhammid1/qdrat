using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class Add_MinistrySimulationExam_Entities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MinistrySimExams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CourseId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalStages = table.Column<int>(type: "int", nullable: false),
                    DefaultQuantQuestionsPerStage = table.Column<int>(type: "int", nullable: false),
                    DefaultVerbalQuestionsPerStage = table.Column<int>(type: "int", nullable: false),
                    DefaultDurationMinutesPerStage = table.Column<int>(type: "int", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByInstructorId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MinistrySimExams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MinistrySimExams_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MinistrySimExams_Instructors_CreatedByInstructorId",
                        column: x => x.CreatedByInstructorId,
                        principalTable: "Instructors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MinistrySimExamAssignmentsToBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MinistrySimExamId = table.Column<int>(type: "int", nullable: false),
                    BatchId = table.Column<int>(type: "int", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsSentToStudents = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByInstructorId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MinistrySimExamAssignmentsToBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MinistrySimExamAssignmentsToBatches_Batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "Batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MinistrySimExamAssignmentsToBatches_Instructors_CreatedByInstructorId",
                        column: x => x.CreatedByInstructorId,
                        principalTable: "Instructors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MinistrySimExamAssignmentsToBatches_MinistrySimExams_MinistrySimExamId",
                        column: x => x.MinistrySimExamId,
                        principalTable: "MinistrySimExams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MinistrySimExamAssignmentsToStudents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MinistrySimExamId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MinistrySimExamAssignmentsToStudents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MinistrySimExamAssignmentsToStudents_MinistrySimExams_MinistrySimExamId",
                        column: x => x.MinistrySimExamId,
                        principalTable: "MinistrySimExams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MinistrySimExamAssignmentsToStudents_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "StudentID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MinistrySimExamStages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MinistrySimExamId = table.Column<int>(type: "int", nullable: false),
                    StageNumber = table.Column<int>(type: "int", nullable: false),
                    QuantSectionId = table.Column<int>(type: "int", nullable: false),
                    QuantQuestionCount = table.Column<int>(type: "int", nullable: false),
                    VerbalSectionId = table.Column<int>(type: "int", nullable: false),
                    VerbalQuestionCount = table.Column<int>(type: "int", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MinistrySimExamStages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MinistrySimExamStages_MinistrySimExams_MinistrySimExamId",
                        column: x => x.MinistrySimExamId,
                        principalTable: "MinistrySimExams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MinistrySimExamStages_Sections_QuantSectionId",
                        column: x => x.QuantSectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MinistrySimExamStages_Sections_VerbalSectionId",
                        column: x => x.VerbalSectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MinistrySimExamStudentAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MinistrySimExamId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    TotalScorePercent = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MinistrySimExamStudentAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MinistrySimExamStudentAttempts_MinistrySimExams_MinistrySimExamId",
                        column: x => x.MinistrySimExamId,
                        principalTable: "MinistrySimExams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MinistrySimExamStudentAttempts_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "StudentID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MinistrySimExamStageIndicatorSelections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MinistrySimExamStageId = table.Column<int>(type: "int", nullable: false),
                    SectionId = table.Column<int>(type: "int", nullable: false),
                    LessonId = table.Column<int>(type: "int", nullable: false),
                    Difficulty = table.Column<int>(type: "int", nullable: false),
                    RequestedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MinistrySimExamStageIndicatorSelections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MinistrySimExamStageIndicatorSelections_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MinistrySimExamStageIndicatorSelections_MinistrySimExamStages_MinistrySimExamStageId",
                        column: x => x.MinistrySimExamStageId,
                        principalTable: "MinistrySimExamStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MinistrySimExamStageIndicatorSelections_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MinistrySimExamStageQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MinistrySimExamStageId = table.Column<int>(type: "int", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StageOrder = table.Column<int>(type: "int", nullable: false),
                    GlobalOrder = table.Column<int>(type: "int", nullable: false),
                    IsQuant = table.Column<bool>(type: "bit", nullable: false),
                    IsManuallySelected = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MinistrySimExamStageQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MinistrySimExamStageQuestions_MinistrySimExamStages_MinistrySimExamStageId",
                        column: x => x.MinistrySimExamStageId,
                        principalTable: "MinistrySimExamStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MinistrySimExamStageQuestions_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MinistrySimExamStudentStageProgresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MinistrySimExamStudentAttemptId = table.Column<int>(type: "int", nullable: false),
                    StageNumber = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    TimeExpired = table.Column<bool>(type: "bit", nullable: false),
                    StagePercentScore = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MinistrySimExamStudentStageProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MinistrySimExamStudentStageProgresses_MinistrySimExamStudentAttempts_MinistrySimExamStudentAttemptId",
                        column: x => x.MinistrySimExamStudentAttemptId,
                        principalTable: "MinistrySimExamStudentAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MinistrySimExamStudentAnswers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MinistrySimExamStudentAttemptId = table.Column<int>(type: "int", nullable: false),
                    MinistrySimExamStageQuestionId = table.Column<int>(type: "int", nullable: false),
                    SelectedOptionId = table.Column<int>(type: "int", nullable: true),
                    IsCorrect = table.Column<bool>(type: "bit", nullable: true),
                    AnsweredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsFlaggedForReview = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MinistrySimExamStudentAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MinistrySimExamStudentAnswers_MinistrySimExamStageQuestions_MinistrySimExamStageQuestionId",
                        column: x => x.MinistrySimExamStageQuestionId,
                        principalTable: "MinistrySimExamStageQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MinistrySimExamStudentAnswers_MinistrySimExamStudentAttempts_MinistrySimExamStudentAttemptId",
                        column: x => x.MinistrySimExamStudentAttemptId,
                        principalTable: "MinistrySimExamStudentAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MinistrySimExamAssignmentsToBatches_BatchId",
                table: "MinistrySimExamAssignmentsToBatches",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_MinistrySimExamAssignmentsToBatches_CreatedByInstructorId",
                table: "MinistrySimExamAssignmentsToBatches",
                column: "CreatedByInstructorId");

            migrationBuilder.CreateIndex(
                name: "IX_MinistrySimExamAssignmentsToBatches_MinistrySimExamId",
                table: "MinistrySimExamAssignmentsToBatches",
                column: "MinistrySimExamId");

            migrationBuilder.CreateIndex(
                name: "IX_MinistrySimExamAssignmentsToStudents_MinistrySimExamId",
                table: "MinistrySimExamAssignmentsToStudents",
                column: "MinistrySimExamId");

            migrationBuilder.CreateIndex(
                name: "IX_MinistrySimExamAssignmentsToStudents_StudentId",
                table: "MinistrySimExamAssignmentsToStudents",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_MinistrySimExams_CourseId",
                table: "MinistrySimExams",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_MinistrySimExams_CreatedByInstructorId",
                table: "MinistrySimExams",
                column: "CreatedByInstructorId");

            migrationBuilder.CreateIndex(
                name: "IX_MinistrySimExamStageIndicatorSelections_LessonId",
                table: "MinistrySimExamStageIndicatorSelections",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_MinistrySimExamStageIndicatorSelections_MinistrySimExamStageId",
                table: "MinistrySimExamStageIndicatorSelections",
                column: "MinistrySimExamStageId");

            migrationBuilder.CreateIndex(
                name: "IX_MinistrySimExamStageIndicatorSelections_SectionId",
                table: "MinistrySimExamStageIndicatorSelections",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_MinistrySimExamStageQuestions_MinistrySimExamStageId",
                table: "MinistrySimExamStageQuestions",
                column: "MinistrySimExamStageId");

            migrationBuilder.CreateIndex(
                name: "IX_MinistrySimExamStageQuestions_QuestionId",
                table: "MinistrySimExamStageQuestions",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_MinistrySimExamStages_MinistrySimExamId_StageNumber",
                table: "MinistrySimExamStages",
                columns: new[] { "MinistrySimExamId", "StageNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MinistrySimExamStages_QuantSectionId",
                table: "MinistrySimExamStages",
                column: "QuantSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_MinistrySimExamStages_VerbalSectionId",
                table: "MinistrySimExamStages",
                column: "VerbalSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_MinistrySimExamStudentAnswers_MinistrySimExamStageQuestionId",
                table: "MinistrySimExamStudentAnswers",
                column: "MinistrySimExamStageQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_MinistrySimExamStudentAnswers_MinistrySimExamStudentAttemptId",
                table: "MinistrySimExamStudentAnswers",
                column: "MinistrySimExamStudentAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_MinistrySimExamStudentAttempts_MinistrySimExamId_StudentId",
                table: "MinistrySimExamStudentAttempts",
                columns: new[] { "MinistrySimExamId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MinistrySimExamStudentAttempts_StudentId",
                table: "MinistrySimExamStudentAttempts",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_MinistrySimExamStudentStageProgresses_MinistrySimExamStudentAttemptId_StageNumber",
                table: "MinistrySimExamStudentStageProgresses",
                columns: new[] { "MinistrySimExamStudentAttemptId", "StageNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MinistrySimExamAssignmentsToBatches");

            migrationBuilder.DropTable(
                name: "MinistrySimExamAssignmentsToStudents");

            migrationBuilder.DropTable(
                name: "MinistrySimExamStageIndicatorSelections");

            migrationBuilder.DropTable(
                name: "MinistrySimExamStudentAnswers");

            migrationBuilder.DropTable(
                name: "MinistrySimExamStudentStageProgresses");

            migrationBuilder.DropTable(
                name: "MinistrySimExamStageQuestions");

            migrationBuilder.DropTable(
                name: "MinistrySimExamStudentAttempts");

            migrationBuilder.DropTable(
                name: "MinistrySimExamStages");

            migrationBuilder.DropTable(
                name: "MinistrySimExams");
        }
    }
}
