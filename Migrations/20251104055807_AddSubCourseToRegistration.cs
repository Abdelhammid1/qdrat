using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddSubCourseToRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SubCourseId",
                table: "FrontendCourseRegistrations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_FrontendCourseRegistrations_SubCourseId",
                table: "FrontendCourseRegistrations",
                column: "SubCourseId");

            migrationBuilder.AddForeignKey(
                 name: "FK_FrontendCourseRegistrations_SubCourses_SubCourseId",
                 table: "FrontendCourseRegistrations",
                 column: "SubCourseId",
                 principalTable: "SubCourses",
                 principalColumn: "Id",
                 onDelete: ReferentialAction.NoAction);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FrontendCourseRegistrations_SubCourses_SubCourseId",
                table: "FrontendCourseRegistrations");

            migrationBuilder.DropIndex(
                name: "IX_FrontendCourseRegistrations_SubCourseId",
                table: "FrontendCourseRegistrations");

            migrationBuilder.DropColumn(
                name: "SubCourseId",
                table: "FrontendCourseRegistrations");
        }
    }
}
