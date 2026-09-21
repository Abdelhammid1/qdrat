using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddLectureActualStartTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ActualStartTime",
                table: "Lecture",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StartedByRole",
                table: "Lecture",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StartedByUserId",
                table: "Lecture",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lecture_StartedByUserId",
                table: "Lecture",
                column: "StartedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Lecture_AspNetUsers_StartedByUserId",
                table: "Lecture",
                column: "StartedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Lecture_AspNetUsers_StartedByUserId",
                table: "Lecture");

            migrationBuilder.DropIndex(
                name: "IX_Lecture_StartedByUserId",
                table: "Lecture");

            migrationBuilder.DropColumn(
                name: "ActualStartTime",
                table: "Lecture");

            migrationBuilder.DropColumn(
                name: "StartedByRole",
                table: "Lecture");

            migrationBuilder.DropColumn(
                name: "StartedByUserId",
                table: "Lecture");
        }
    }
}
