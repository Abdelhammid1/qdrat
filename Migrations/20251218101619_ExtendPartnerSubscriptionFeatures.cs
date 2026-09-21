using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class ExtendPartnerSubscriptionFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AccessUntilDate",
                table: "PartnerSubscriptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CanUseAIAnalytics",
                table: "PartnerSubscriptions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanUseExams",
                table: "PartnerSubscriptions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanUseHomework",
                table: "PartnerSubscriptions",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccessUntilDate",
                table: "PartnerSubscriptions");

            migrationBuilder.DropColumn(
                name: "CanUseAIAnalytics",
                table: "PartnerSubscriptions");

            migrationBuilder.DropColumn(
                name: "CanUseExams",
                table: "PartnerSubscriptions");

            migrationBuilder.DropColumn(
                name: "CanUseHomework",
                table: "PartnerSubscriptions");
        }
    }
}
