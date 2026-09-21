using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class Add_ParentsArea : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[dbo].[ExamAssignmentsToStudents]', N'SourceType') IS NULL
BEGIN
    ALTER TABLE [dbo].[ExamAssignmentsToStudents]
        ADD [SourceType] nvarchar(50) NULL;
END");

            migrationBuilder.CreateTable(
                name: "ParentActionLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: true),
                    ActionType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentActionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParentActionLogs_Parents_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Parents",
                        principalColumn: "ParentID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParentActionLogs_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "StudentID");
                });

            migrationBuilder.CreateTable(
                name: "ParentMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MessageBody = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RepliedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RepliedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ReplyBody = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParentMessages_AspNetUsers_RepliedByUserId",
                        column: x => x.RepliedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ParentMessages_Parents_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Parents",
                        principalColumn: "ParentID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParentMessages_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "StudentID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ParentSmartPracticeRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    CurriculumId = table.Column<int>(type: "int", nullable: true),
                    SectionId = table.Column<int>(type: "int", nullable: true),
                    RequestedQuestionCount = table.Column<int>(type: "int", nullable: false),
                    RequestedDurationMinutes = table.Column<int>(type: "int", nullable: false),
                    PracticeMode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GeneratedExamAssignmentToStudentId = table.Column<int>(type: "int", nullable: true),
                    SafeSummary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RecommendationSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentSmartPracticeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParentSmartPracticeRequests_Curriculums_CurriculumId",
                        column: x => x.CurriculumId,
                        principalTable: "Curriculums",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ParentSmartPracticeRequests_ExamAssignmentsToStudents_GeneratedExamAssignmentToStudentId",
                        column: x => x.GeneratedExamAssignmentToStudentId,
                        principalTable: "ExamAssignmentsToStudents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ParentSmartPracticeRequests_Parents_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Parents",
                        principalColumn: "ParentID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParentSmartPracticeRequests_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ParentSmartPracticeRequests_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "StudentID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ParentStudentInsights",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OverallStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CommitmentStatus = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LearningTrend = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SafeWeaknessSummary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RecommendedAction = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CanRequestSmartPractice = table.Column<bool>(type: "bit", nullable: false),
                    BlockingReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentStudentInsights", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParentStudentInsights_Parents_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Parents",
                        principalColumn: "ParentID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParentStudentInsights_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "StudentID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParentActionLogs_ParentId",
                table: "ParentActionLogs",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentActionLogs_StudentId",
                table: "ParentActionLogs",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentMessages_ParentId",
                table: "ParentMessages",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentMessages_RepliedByUserId",
                table: "ParentMessages",
                column: "RepliedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentMessages_StudentId",
                table: "ParentMessages",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentSmartPracticeRequests_CurriculumId",
                table: "ParentSmartPracticeRequests",
                column: "CurriculumId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentSmartPracticeRequests_GeneratedExamAssignmentToStudentId",
                table: "ParentSmartPracticeRequests",
                column: "GeneratedExamAssignmentToStudentId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentSmartPracticeRequests_ParentId",
                table: "ParentSmartPracticeRequests",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentSmartPracticeRequests_SectionId",
                table: "ParentSmartPracticeRequests",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentSmartPracticeRequests_StudentId",
                table: "ParentSmartPracticeRequests",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentStudentInsights_ParentId",
                table: "ParentStudentInsights",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentStudentInsights_StudentId",
                table: "ParentStudentInsights",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParentActionLogs");

            migrationBuilder.DropTable(
                name: "ParentMessages");

            migrationBuilder.DropTable(
                name: "ParentSmartPracticeRequests");

            migrationBuilder.DropTable(
                name: "ParentStudentInsights");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[dbo].[ExamAssignmentsToStudents]', N'SourceType') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[ExamAssignmentsToStudents]
        DROP COLUMN [SourceType];
END");
        }
    }
}
