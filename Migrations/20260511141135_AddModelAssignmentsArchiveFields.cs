using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddModelAssignmentsArchiveFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "HomeworkSets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedByUserId",
                table: "HomeworkSets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "HomeworkSets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "ExamAssignmentsToBatches",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedByUserId",
                table: "ExamAssignmentsToBatches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "ExamAssignmentsToBatches",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "HomeworkSets");

            migrationBuilder.DropColumn(
                name: "ArchivedByUserId",
                table: "HomeworkSets");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "HomeworkSets");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "ExamAssignmentsToBatches");

            migrationBuilder.DropColumn(
                name: "ArchivedByUserId",
                table: "ExamAssignmentsToBatches");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "ExamAssignmentsToBatches");
        }
    }
}
