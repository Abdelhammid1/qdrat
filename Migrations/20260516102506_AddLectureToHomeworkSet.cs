using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddLectureToHomeworkSet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LectureId",
                table: "HomeworkSets",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkSets_LectureId",
                table: "HomeworkSets",
                column: "LectureId");

            migrationBuilder.AddForeignKey(
                name: "FK_HomeworkSets_Lecture_LectureId",
                table: "HomeworkSets",
                column: "LectureId",
                principalTable: "Lecture",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HomeworkSets_Lecture_LectureId",
                table: "HomeworkSets");

            migrationBuilder.DropIndex(
                name: "IX_HomeworkSets_LectureId",
                table: "HomeworkSets");

            migrationBuilder.DropColumn(
                name: "LectureId",
                table: "HomeworkSets");
        }
    }
}
