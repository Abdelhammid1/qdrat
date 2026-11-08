using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class APD_MinistrySimPartnerHidden : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "HiddenByOwnerAt",
                table: "MinistrySimExamAssignmentsToStudents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "HiddenByOwnerAt",
                table: "MinistrySimExamAssignmentsToBatches",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HiddenByOwnerAt",
                table: "MinistrySimExamAssignmentsToStudents");

            migrationBuilder.DropColumn(
                name: "HiddenByOwnerAt",
                table: "MinistrySimExamAssignmentsToBatches");
        }
    }
}
