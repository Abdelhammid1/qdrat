using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddHomeworkFollowUpFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ParentContacted",
                table: "HomeworkSetStudents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ParentContactedAt",
                table: "HomeworkSetStudents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "StudentReminderContacted",
                table: "HomeworkSetStudents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "StudentReminderContactedAt",
                table: "HomeworkSetStudents",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParentContacted",
                table: "HomeworkSetStudents");

            migrationBuilder.DropColumn(
                name: "ParentContactedAt",
                table: "HomeworkSetStudents");

            migrationBuilder.DropColumn(
                name: "StudentReminderContacted",
                table: "HomeworkSetStudents");

            migrationBuilder.DropColumn(
                name: "StudentReminderContactedAt",
                table: "HomeworkSetStudents");
        }
    }
}
