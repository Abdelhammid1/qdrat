using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class RTK_RemedialTracks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RemedialTracks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CurriculumId = table.Column<int>(type: "int", nullable: false),
                    PassPercent = table.Column<int>(type: "int", nullable: false),
                    MinWatchPercent = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsStructureLocked = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemedialTracks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemedialTracks_Curriculums_CurriculumId",
                        column: x => x.CurriculumId,
                        principalTable: "Curriculums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RemedialTrackAxes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TrackId = table.Column<int>(type: "int", nullable: false),
                    SectionId = table.Column<int>(type: "int", nullable: false),
                    TitleOverride = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Exam101ModelId = table.Column<int>(type: "int", nullable: false),
                    Exam102ModelId = table.Column<int>(type: "int", nullable: false),
                    ExamDurationMinutes = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemedialTrackAxes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemedialTrackAxes_ProfessionalModels_Exam101ModelId",
                        column: x => x.Exam101ModelId,
                        principalTable: "ProfessionalModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RemedialTrackAxes_ProfessionalModels_Exam102ModelId",
                        column: x => x.Exam102ModelId,
                        principalTable: "ProfessionalModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RemedialTrackAxes_RemedialTracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "RemedialTracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RemedialTrackAxes_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RemedialTrackPublications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TrackId = table.Column<int>(type: "int", nullable: false),
                    BatchId = table.Column<int>(type: "int", nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    PublishAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AccessCode = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: true),
                    CodeVersion = table.Column<int>(type: "int", nullable: false),
                    CodeGeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AdminNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TotalStudents = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemedialTrackPublications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemedialTrackPublications_Batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "Batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RemedialTrackPublications_RemedialTracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "RemedialTracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RemedialTrackVideos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AxisId = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DurationSeconds = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemedialTrackVideos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemedialTrackVideos_RemedialTrackAxes_AxisId",
                        column: x => x.AxisId,
                        principalTable: "RemedialTrackAxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RemedialTrackEnrollments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PublicationId = table.Column<int>(type: "int", nullable: false),
                    TrackId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CurrentAxisId = table.Column<int>(type: "int", nullable: true),
                    VerifiedCodeVersion = table.Column<int>(type: "int", nullable: true),
                    FailedCodeAttempts = table.Column<int>(type: "int", nullable: false),
                    CodeLockedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AdminReportNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemedialTrackEnrollments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemedialTrackEnrollments_RemedialTrackPublications_PublicationId",
                        column: x => x.PublicationId,
                        principalTable: "RemedialTrackPublications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RemedialTrackEnrollments_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "StudentID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RemedialTrackAxisProgresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnrollmentId = table.Column<int>(type: "int", nullable: false),
                    AxisId = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Round = table.Column<int>(type: "int", nullable: false),
                    Exam101Percent = table.Column<double>(type: "float", nullable: true),
                    Exam102Percent = table.Column<double>(type: "float", nullable: true),
                    OpenedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PassedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AdminOpenedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AdminOpenedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AdminOpenReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AdminOpenedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemedialTrackAxisProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemedialTrackAxisProgresses_RemedialTrackAxes_AxisId",
                        column: x => x.AxisId,
                        principalTable: "RemedialTrackAxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RemedialTrackAxisProgresses_RemedialTrackEnrollments_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalTable: "RemedialTrackEnrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RemedialTrackEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnrollmentId = table.Column<int>(type: "int", nullable: false),
                    AxisId = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ActorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemedialTrackEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemedialTrackEvents_RemedialTrackEnrollments_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalTable: "RemedialTrackEnrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RemedialTrackExamAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AxisProgressId = table.Column<int>(type: "int", nullable: false),
                    ExamNumber = table.Column<int>(type: "int", nullable: false),
                    ModelId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalQuestions = table.Column<int>(type: "int", nullable: false),
                    CorrectCount = table.Column<int>(type: "int", nullable: false),
                    ScorePercent = table.Column<double>(type: "float", nullable: false),
                    IsPassed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemedialTrackExamAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemedialTrackExamAttempts_RemedialTrackAxisProgresses_AxisProgressId",
                        column: x => x.AxisProgressId,
                        principalTable: "RemedialTrackAxisProgresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RemedialTrackVideoProgresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AxisProgressId = table.Column<int>(type: "int", nullable: false),
                    VideoId = table.Column<int>(type: "int", nullable: false),
                    VideoOrder = table.Column<int>(type: "int", nullable: false),
                    Round = table.Column<int>(type: "int", nullable: false),
                    WatchedSeconds = table.Column<double>(type: "float", nullable: false),
                    DurationSeconds = table.Column<int>(type: "int", nullable: true),
                    EndedSeen = table.Column<bool>(type: "bit", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    FirstPingAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastPingAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastPingState = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemedialTrackVideoProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemedialTrackVideoProgresses_RemedialTrackAxisProgresses_AxisProgressId",
                        column: x => x.AxisProgressId,
                        principalTable: "RemedialTrackAxisProgresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RemedialTrackVideoProgresses_RemedialTrackVideos_VideoId",
                        column: x => x.VideoId,
                        principalTable: "RemedialTrackVideos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RemedialTrackExamAttemptQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AttemptId = table.Column<int>(type: "int", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    SelectedAnswer = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsCorrect = table.Column<bool>(type: "bit", nullable: true),
                    AnsweredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemedialTrackExamAttemptQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemedialTrackExamAttemptQuestions_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RemedialTrackExamAttemptQuestions_RemedialTrackExamAttempts_AttemptId",
                        column: x => x.AttemptId,
                        principalTable: "RemedialTrackExamAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RemedialTrackAxes_Exam101ModelId",
                table: "RemedialTrackAxes",
                column: "Exam101ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_RemedialTrackAxes_Exam102ModelId",
                table: "RemedialTrackAxes",
                column: "Exam102ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_RemedialTrackAxes_SectionId",
                table: "RemedialTrackAxes",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_RemedialTrackAxes_TrackId_Order",
                table: "RemedialTrackAxes",
                columns: new[] { "TrackId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RemedialTrackAxes_TrackId_SectionId",
                table: "RemedialTrackAxes",
                columns: new[] { "TrackId", "SectionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RemedialTrackAxisProgresses_AxisId",
                table: "RemedialTrackAxisProgresses",
                column: "AxisId");

            migrationBuilder.CreateIndex(
                name: "IX_RemedialTrackAxisProgresses_EnrollmentId_AxisId",
                table: "RemedialTrackAxisProgresses",
                columns: new[] { "EnrollmentId", "AxisId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RemedialTrackAxisProgresses_EnrollmentId_Order",
                table: "RemedialTrackAxisProgresses",
                columns: new[] { "EnrollmentId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_RemedialTrackEnrollments_PublicationId_StudentId",
                table: "RemedialTrackEnrollments",
                columns: new[] { "PublicationId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RemedialTrackEnrollments_StudentId_Status",
                table: "RemedialTrackEnrollments",
                columns: new[] { "StudentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RemedialTrackEnrollments_TrackId_StudentId",
                table: "RemedialTrackEnrollments",
                columns: new[] { "TrackId", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_RemedialTrackEvents_EnrollmentId_CreatedAtUtc",
                table: "RemedialTrackEvents",
                columns: new[] { "EnrollmentId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RemedialTrackExamAttemptQuestions_AttemptId_Order",
                table: "RemedialTrackExamAttemptQuestions",
                columns: new[] { "AttemptId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_RemedialTrackExamAttemptQuestions_AttemptId_QuestionId",
                table: "RemedialTrackExamAttemptQuestions",
                columns: new[] { "AttemptId", "QuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RemedialTrackExamAttemptQuestions_QuestionId",
                table: "RemedialTrackExamAttemptQuestions",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_RemedialTrackExamAttempts_AxisProgressId_ExamNumber",
                table: "RemedialTrackExamAttempts",
                columns: new[] { "AxisProgressId", "ExamNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RemedialTrackPublications_BatchId_Status",
                table: "RemedialTrackPublications",
                columns: new[] { "BatchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RemedialTrackPublications_TrackId_Status",
                table: "RemedialTrackPublications",
                columns: new[] { "TrackId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_RemedialTrackPublications_ActiveCode",
                table: "RemedialTrackPublications",
                column: "AccessCode",
                unique: true,
                filter: "[AccessCode] IS NOT NULL AND [Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_RemedialTracks_Code",
                table: "RemedialTracks",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RemedialTracks_CurriculumId_Status",
                table: "RemedialTracks",
                columns: new[] { "CurriculumId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RemedialTrackVideoProgresses_AxisProgressId_VideoId_Round",
                table: "RemedialTrackVideoProgresses",
                columns: new[] { "AxisProgressId", "VideoId", "Round" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RemedialTrackVideoProgresses_VideoId",
                table: "RemedialTrackVideoProgresses",
                column: "VideoId");

            migrationBuilder.CreateIndex(
                name: "IX_RemedialTrackVideos_AxisId_Order",
                table: "RemedialTrackVideos",
                columns: new[] { "AxisId", "Order" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RemedialTrackEvents");

            migrationBuilder.DropTable(
                name: "RemedialTrackExamAttemptQuestions");

            migrationBuilder.DropTable(
                name: "RemedialTrackVideoProgresses");

            migrationBuilder.DropTable(
                name: "RemedialTrackExamAttempts");

            migrationBuilder.DropTable(
                name: "RemedialTrackVideos");

            migrationBuilder.DropTable(
                name: "RemedialTrackAxisProgresses");

            migrationBuilder.DropTable(
                name: "RemedialTrackAxes");

            migrationBuilder.DropTable(
                name: "RemedialTrackEnrollments");

            migrationBuilder.DropTable(
                name: "RemedialTrackPublications");

            migrationBuilder.DropTable(
                name: "RemedialTracks");
        }
    }
}
