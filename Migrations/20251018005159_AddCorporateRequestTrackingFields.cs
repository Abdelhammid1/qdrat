using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddCorporateRequestTrackingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdminNotes",
                table: "CorporateRegistrationRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HandledBy",
                table: "CorporateRegistrationRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdated",
                table: "CorporateRegistrationRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "CorporateRegistrationRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdminNotes",
                table: "CorporateRegistrationRequests");

            migrationBuilder.DropColumn(
                name: "HandledBy",
                table: "CorporateRegistrationRequests");

            migrationBuilder.DropColumn(
                name: "LastUpdated",
                table: "CorporateRegistrationRequests");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "CorporateRegistrationRequests");
        }
    }
}
