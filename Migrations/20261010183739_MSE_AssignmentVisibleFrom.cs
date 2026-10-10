using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class MSE_AssignmentVisibleFrom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "VisibleFrom",
                table: "MinistrySimExamAssignmentsToStudents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VisibleFrom",
                table: "MinistrySimExamAssignmentsToBatches",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VisibleFrom",
                table: "MinistrySimExamAssignmentsToStudents");

            migrationBuilder.DropColumn(
                name: "VisibleFrom",
                table: "MinistrySimExamAssignmentsToBatches");
        }
    }
}
