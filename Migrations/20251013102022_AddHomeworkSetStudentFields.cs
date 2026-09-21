using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddHomeworkSetStudentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AssignedAt",
                table: "HomeworkSetStudents",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsSubmitted",
                table: "HomeworkSetStudents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdated",
                table: "HomeworkSetStudents",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "NotificationSent",
                table: "HomeworkSetStudents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "Score",
                table: "HomeworkSetStudents",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "HomeworkSetStudents",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignedAt",
                table: "HomeworkSetStudents");

            migrationBuilder.DropColumn(
                name: "IsSubmitted",
                table: "HomeworkSetStudents");

            migrationBuilder.DropColumn(
                name: "LastUpdated",
                table: "HomeworkSetStudents");

            migrationBuilder.DropColumn(
                name: "NotificationSent",
                table: "HomeworkSetStudents");

            migrationBuilder.DropColumn(
                name: "Score",
                table: "HomeworkSetStudents");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "HomeworkSetStudents");
        }
    }
}
