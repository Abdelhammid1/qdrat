using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class RTK_Addendum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RemedialTrackExamAttempts_AxisProgressId_ExamNumber",
                table: "RemedialTrackExamAttempts");

            migrationBuilder.AddColumn<int>(
                name: "AddendumId",
                table: "RemedialTrackExamAttempts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RemedialTrackAddenda",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PublicationId = table.Column<int>(type: "int", nullable: false),
                    AxisId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DurationSeconds = table.Column<int>(type: "int", nullable: true),
                    ExamModelId = table.Column<int>(type: "int", nullable: true),
                    ExamDurationMinutes = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemedialTrackAddenda", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemedialTrackAddenda_ProfessionalModels_ExamModelId",
                        column: x => x.ExamModelId,
                        principalTable: "ProfessionalModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RemedialTrackAddenda_RemedialTrackAxes_AxisId",
                        column: x => x.AxisId,
                        principalTable: "RemedialTrackAxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RemedialTrackAddenda_RemedialTrackPublications_PublicationId",
                        column: x => x.PublicationId,
                        principalTable: "RemedialTrackPublications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RemedialTrackAddendumProgresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AddendumId = table.Column<int>(type: "int", nullable: false),
                    EnrollmentId = table.Column<int>(type: "int", nullable: false),
                    WatchedSeconds = table.Column<double>(type: "float", nullable: false),
                    DurationSeconds = table.Column<int>(type: "int", nullable: true),
                    EndedSeen = table.Column<bool>(type: "bit", nullable: false),
                    VideoCompleted = table.Column<bool>(type: "bit", nullable: false),
                    LastPingAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastPingState = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    VideoCompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExamPassed = table.Column<bool>(type: "bit", nullable: false),
                    BestScorePercent = table.Column<double>(type: "float", nullable: true),
                    AttemptsCount = table.Column<int>(type: "int", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemedialTrackAddendumProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemedialTrackAddendumProgresses_RemedialTrackAddenda_AddendumId",
                        column: x => x.AddendumId,
                        principalTable: "RemedialTrackAddenda",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RemedialTrackAddendumProgresses_RemedialTrackEnrollments_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalTable: "RemedialTrackEnrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RemedialTrackExamAttempts_AxisProgressId_ExamNumber",
                table: "RemedialTrackExamAttempts",
                columns: new[] { "AxisProgressId", "ExamNumber" },
                unique: true,
                filter: "[AddendumId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_RemedialTrackExamAttempts_Addendum_OpenAttempt",
                table: "RemedialTrackExamAttempts",
                columns: new[] { "AddendumId", "AxisProgressId" },
                unique: true,
                filter: "[AddendumId] IS NOT NULL AND [Status] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RemedialTrackAddenda_AxisId",
                table: "RemedialTrackAddenda",
                column: "AxisId");

            migrationBuilder.CreateIndex(
                name: "IX_RemedialTrackAddenda_ExamModelId",
                table: "RemedialTrackAddenda",
                column: "ExamModelId");

            migrationBuilder.CreateIndex(
                name: "IX_RemedialTrackAddenda_Publication_IsActive",
                table: "RemedialTrackAddenda",
                columns: new[] { "PublicationId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_RemedialTrackAddendumProgresses_Enrollment_CompletedAt",
                table: "RemedialTrackAddendumProgresses",
                columns: new[] { "EnrollmentId", "CompletedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_RemedialTrackAddendumProgresses_Addendum_Enrollment",
                table: "RemedialTrackAddendumProgresses",
                columns: new[] { "AddendumId", "EnrollmentId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RemedialTrackExamAttempts_RemedialTrackAddenda_AddendumId",
                table: "RemedialTrackExamAttempts",
                column: "AddendumId",
                principalTable: "RemedialTrackAddenda",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RemedialTrackExamAttempts_RemedialTrackAddenda_AddendumId",
                table: "RemedialTrackExamAttempts");

            migrationBuilder.DropTable(
                name: "RemedialTrackAddendumProgresses");

            migrationBuilder.DropTable(
                name: "RemedialTrackAddenda");

            migrationBuilder.DropIndex(
                name: "IX_RemedialTrackExamAttempts_AxisProgressId_ExamNumber",
                table: "RemedialTrackExamAttempts");

            migrationBuilder.DropIndex(
                name: "UX_RemedialTrackExamAttempts_Addendum_OpenAttempt",
                table: "RemedialTrackExamAttempts");

            migrationBuilder.DropColumn(
                name: "AddendumId",
                table: "RemedialTrackExamAttempts");

            migrationBuilder.CreateIndex(
                name: "IX_RemedialTrackExamAttempts_AxisProgressId_ExamNumber",
                table: "RemedialTrackExamAttempts",
                columns: new[] { "AxisProgressId", "ExamNumber" },
                unique: true);
        }
    }
}
