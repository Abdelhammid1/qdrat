using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class RTK_ParentReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoSendParentReports",
                table: "RemedialTrackPublications",
                type: "bit",
                nullable: false,
                defaultValue: true);   // أوامر النشر القائمة: الإرسال التلقائي مفعّل (افتراض D23)

            migrationBuilder.CreateTable(
                name: "RemedialTrackParentReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnrollmentId = table.Column<int>(type: "int", nullable: false),
                    AxisProgressId = table.Column<int>(type: "int", nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemedialTrackParentReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemedialTrackParentReports_RemedialTrackEnrollments_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalTable: "RemedialTrackEnrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RemedialTrackParentReports_Parent_Status_CreatedAt",
                table: "RemedialTrackParentReports",
                columns: new[] { "ParentId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RemedialTrackParentReports_Status_CreatedAt",
                table: "RemedialTrackParentReports",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_RemedialTrackParentReports_Enrollment_Axis_Kind",
                table: "RemedialTrackParentReports",
                columns: new[] { "EnrollmentId", "AxisProgressId", "Kind" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RemedialTrackParentReports");

            migrationBuilder.DropColumn(
                name: "AutoSendParentReports",
                table: "RemedialTrackPublications");
        }
    }
}
