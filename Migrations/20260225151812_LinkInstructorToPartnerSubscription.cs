using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class LinkInstructorToPartnerSubscription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PartnerSubscriptionId",
                table: "Instructors",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Instructors_PartnerSubscriptionId",
                table: "Instructors",
                column: "PartnerSubscriptionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Instructors_PartnerSubscriptions_PartnerSubscriptionId",
                table: "Instructors",
                column: "PartnerSubscriptionId",
                principalTable: "PartnerSubscriptions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Instructors_PartnerSubscriptions_PartnerSubscriptionId",
                table: "Instructors");

            migrationBuilder.DropIndex(
                name: "IX_Instructors_PartnerSubscriptionId",
                table: "Instructors");

            migrationBuilder.DropColumn(
                name: "PartnerSubscriptionId",
                table: "Instructors");
        }
    }
}
