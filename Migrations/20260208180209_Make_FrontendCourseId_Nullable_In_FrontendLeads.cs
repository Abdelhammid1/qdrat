using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class Make_FrontendCourseId_Nullable_In_FrontendLeads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FrontendLeads_FrontendCourses_FrontendCourseId",
                table: "FrontendLeads");

            migrationBuilder.AlterColumn<int>(
                name: "FrontendCourseId",
                table: "FrontendLeads",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_FrontendLeads_FrontendCourses_FrontendCourseId",
                table: "FrontendLeads",
                column: "FrontendCourseId",
                principalTable: "FrontendCourses",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FrontendLeads_FrontendCourses_FrontendCourseId",
                table: "FrontendLeads");

            migrationBuilder.AlterColumn<int>(
                name: "FrontendCourseId",
                table: "FrontendLeads",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FrontendLeads_FrontendCourses_FrontendCourseId",
                table: "FrontendLeads",
                column: "FrontendCourseId",
                principalTable: "FrontendCourses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
