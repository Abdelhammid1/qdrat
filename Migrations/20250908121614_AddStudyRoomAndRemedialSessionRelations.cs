using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddStudyRoomAndRemedialSessionRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StudyRoomId",
                table: "RemedialSessions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RemedialSessionId",
                table: "AttendanceRecords",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RemedialSessions_StudyRoomId",
                table: "RemedialSessions",
                column: "StudyRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_RemedialSessionId",
                table: "AttendanceRecords",
                column: "RemedialSessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceRecords_RemedialSessions_RemedialSessionId",
                table: "AttendanceRecords",
                column: "RemedialSessionId",
                principalTable: "RemedialSessions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RemedialSessions_StudyRooms_StudyRoomId",
                table: "RemedialSessions",
                column: "StudyRoomId",
                principalTable: "StudyRooms",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceRecords_RemedialSessions_RemedialSessionId",
                table: "AttendanceRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_RemedialSessions_StudyRooms_StudyRoomId",
                table: "RemedialSessions");

            migrationBuilder.DropIndex(
                name: "IX_RemedialSessions_StudyRoomId",
                table: "RemedialSessions");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecords_RemedialSessionId",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "StudyRoomId",
                table: "RemedialSessions");

            migrationBuilder.DropColumn(
                name: "RemedialSessionId",
                table: "AttendanceRecords");
        }
    }
}
