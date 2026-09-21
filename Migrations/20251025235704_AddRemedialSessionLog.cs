using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddRemedialSessionLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExamAssignmentId",
                table: "StudentActivityLogs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AccessCode",
                table: "RemedialSessions",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "RemedialSessionLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    VideoId = table.Column<int>(type: "int", nullable: true),
                    WatchStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WatchEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationWatched = table.Column<int>(type: "int", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    DeviceInfo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemedialSessionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemedialSessionLogs_RemedialSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "RemedialSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RemedialSessionLogs_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "StudentID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RemedialSessionLogs_SessionId",
                table: "RemedialSessionLogs",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_RemedialSessionLogs_StudentId",
                table: "RemedialSessionLogs",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RemedialSessionLogs");

            migrationBuilder.DropColumn(
                name: "ExamAssignmentId",
                table: "StudentActivityLogs");

            migrationBuilder.DropColumn(
                name: "AccessCode",
                table: "RemedialSessions");
        }
    }
}
