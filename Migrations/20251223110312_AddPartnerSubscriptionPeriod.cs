using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerSubscriptionPeriod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PartnerSubscriptionPeriodId",
                table: "Students",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PartnerSubscriptionPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerSubscriptionId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MaxStudents = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerSubscriptionPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartnerSubscriptionPeriods_PartnerSubscriptions_PartnerSubscriptionId",
                        column: x => x.PartnerSubscriptionId,
                        principalTable: "PartnerSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Students_PartnerSubscriptionPeriodId",
                table: "Students",
                column: "PartnerSubscriptionPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerSubscriptionPeriods_PartnerSubscriptionId",
                table: "PartnerSubscriptionPeriods",
                column: "PartnerSubscriptionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Students_PartnerSubscriptionPeriods_PartnerSubscriptionPeriodId",
                table: "Students",
                column: "PartnerSubscriptionPeriodId",
                principalTable: "PartnerSubscriptionPeriods",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Students_PartnerSubscriptionPeriods_PartnerSubscriptionPeriodId",
                table: "Students");

            migrationBuilder.DropTable(
                name: "PartnerSubscriptionPeriods");

            migrationBuilder.DropIndex(
                name: "IX_Students_PartnerSubscriptionPeriodId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "PartnerSubscriptionPeriodId",
                table: "Students");
        }
    }
}
