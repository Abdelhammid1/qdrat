using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddLectureEndTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ActualEndTime",
                table: "Lecture",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "Lecture",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EndedByRole",
                table: "Lecture",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EndedByUserId",
                table: "Lecture",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lecture_EndedByUserId",
                table: "Lecture",
                column: "EndedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Lecture_AspNetUsers_EndedByUserId",
                table: "Lecture",
                column: "EndedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Lecture_AspNetUsers_EndedByUserId",
                table: "Lecture");

            migrationBuilder.DropIndex(
                name: "IX_Lecture_EndedByUserId",
                table: "Lecture");

            migrationBuilder.DropColumn(
                name: "ActualEndTime",
                table: "Lecture");

            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "Lecture");

            migrationBuilder.DropColumn(
                name: "EndedByRole",
                table: "Lecture");

            migrationBuilder.DropColumn(
                name: "EndedByUserId",
                table: "Lecture");
        }
    }
}
