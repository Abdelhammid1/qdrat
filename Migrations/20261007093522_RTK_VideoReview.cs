using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class RTK_VideoReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "VideoReviewChangedAtUtc",
                table: "RemedialTrackEnrollments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoReviewChangedByName",
                table: "RemedialTrackEnrollments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoReviewChangedByUserId",
                table: "RemedialTrackEnrollments",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VideoReviewEnabled",
                table: "RemedialTrackEnrollments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "VideoReviewUntilUtc",
                table: "RemedialTrackEnrollments",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VideoReviewChangedAtUtc",
                table: "RemedialTrackEnrollments");

            migrationBuilder.DropColumn(
                name: "VideoReviewChangedByName",
                table: "RemedialTrackEnrollments");

            migrationBuilder.DropColumn(
                name: "VideoReviewChangedByUserId",
                table: "RemedialTrackEnrollments");

            migrationBuilder.DropColumn(
                name: "VideoReviewEnabled",
                table: "RemedialTrackEnrollments");

            migrationBuilder.DropColumn(
                name: "VideoReviewUntilUtc",
                table: "RemedialTrackEnrollments");
        }
    }
}
