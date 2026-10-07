using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class RTK_PublicationSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_RemedialTrackPublications_ActiveCode",
                table: "RemedialTrackPublications");

            migrationBuilder.AddColumn<string>(
                name: "DeleteReason",
                table: "RemedialTrackPublications",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "RemedialTrackPublications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedByName",
                table: "RemedialTrackPublications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedByUserId",
                table: "RemedialTrackPublications",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "RemedialTrackPublications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_RemedialTrackPublications_IsDeleted_CreatedAt",
                table: "RemedialTrackPublications",
                columns: new[] { "IsDeleted", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_RemedialTrackPublications_ActiveCode",
                table: "RemedialTrackPublications",
                column: "AccessCode",
                unique: true,
                filter: "[AccessCode] IS NOT NULL AND [Status] = 1 AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RemedialTrackPublications_IsDeleted_CreatedAt",
                table: "RemedialTrackPublications");

            migrationBuilder.DropIndex(
                name: "UX_RemedialTrackPublications_ActiveCode",
                table: "RemedialTrackPublications");

            migrationBuilder.DropColumn(
                name: "DeleteReason",
                table: "RemedialTrackPublications");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "RemedialTrackPublications");

            migrationBuilder.DropColumn(
                name: "DeletedByName",
                table: "RemedialTrackPublications");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "RemedialTrackPublications");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "RemedialTrackPublications");

            migrationBuilder.CreateIndex(
                name: "UX_RemedialTrackPublications_ActiveCode",
                table: "RemedialTrackPublications",
                column: "AccessCode",
                unique: true,
                filter: "[AccessCode] IS NOT NULL AND [Status] = 1");
        }
    }
}
