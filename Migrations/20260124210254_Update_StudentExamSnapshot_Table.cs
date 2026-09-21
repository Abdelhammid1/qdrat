using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class Update_StudentExamSnapshot_Table : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BuiltAt",
                table: "StudentExamSnapshots",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsComplete",
                table: "StudentExamSnapshots",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdatedAt",
                table: "StudentExamSnapshots",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "SkippedAnswers",
                table: "StudentExamSnapshots",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "WrongQuestionsTrendJson",
                table: "StudentExamSnapshots",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuiltAt",
                table: "StudentExamSnapshots");

            migrationBuilder.DropColumn(
                name: "IsComplete",
                table: "StudentExamSnapshots");

            migrationBuilder.DropColumn(
                name: "LastUpdatedAt",
                table: "StudentExamSnapshots");

            migrationBuilder.DropColumn(
                name: "SkippedAnswers",
                table: "StudentExamSnapshots");

            migrationBuilder.DropColumn(
                name: "WrongQuestionsTrendJson",
                table: "StudentExamSnapshots");
        }
    }
}
