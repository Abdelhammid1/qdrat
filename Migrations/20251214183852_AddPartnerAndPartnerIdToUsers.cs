using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerAndPartnerIdToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PartnerId",
                table: "Branches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PartnerId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Partner",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LogoPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PartnershipStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PartnershipEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Partner", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Branches_PartnerId",
                table: "Branches",
                column: "PartnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Branches_Partner_PartnerId",
                table: "Branches",
                column: "PartnerId",
                principalTable: "Partner",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Branches_Partner_PartnerId",
                table: "Branches");

            migrationBuilder.DropTable(
                name: "Partner");

            migrationBuilder.DropIndex(
                name: "IX_Branches_PartnerId",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "PartnerId",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "PartnerId",
                table: "AspNetUsers");
        }
    }
}
