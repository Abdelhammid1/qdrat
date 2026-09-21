using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePartnerSubscriptionStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CanUseHomework",
                table: "PartnerSubscriptions",
                newName: "CanUseRemedialSessions");

            migrationBuilder.RenameColumn(
                name: "CanUseExams",
                table: "PartnerSubscriptions",
                newName: "CanUseRemedialPlans");

            migrationBuilder.AddColumn<bool>(
                name: "CanAccessEducationalContent",
                table: "PartnerSubscriptions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanCreateExams",
                table: "PartnerSubscriptions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanCreateHomework",
                table: "PartnerSubscriptions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanUsePerformanceIndicatorExams",
                table: "PartnerSubscriptions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanUsePlacementExams",
                table: "PartnerSubscriptions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanUseReinforcementSkills",
                table: "PartnerSubscriptions",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanAccessEducationalContent",
                table: "PartnerSubscriptions");

            migrationBuilder.DropColumn(
                name: "CanCreateExams",
                table: "PartnerSubscriptions");

            migrationBuilder.DropColumn(
                name: "CanCreateHomework",
                table: "PartnerSubscriptions");

            migrationBuilder.DropColumn(
                name: "CanUsePerformanceIndicatorExams",
                table: "PartnerSubscriptions");

            migrationBuilder.DropColumn(
                name: "CanUsePlacementExams",
                table: "PartnerSubscriptions");

            migrationBuilder.DropColumn(
                name: "CanUseReinforcementSkills",
                table: "PartnerSubscriptions");

            migrationBuilder.RenameColumn(
                name: "CanUseRemedialSessions",
                table: "PartnerSubscriptions",
                newName: "CanUseHomework");

            migrationBuilder.RenameColumn(
                name: "CanUseRemedialPlans",
                table: "PartnerSubscriptions",
                newName: "CanUseExams");
        }
    }
}
