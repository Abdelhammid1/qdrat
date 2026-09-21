using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerSubscriptionCourses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PartnerSubscriptionCourses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerSubscriptionId = table.Column<int>(type: "int", nullable: false),
                    CourseId = table.Column<int>(type: "int", nullable: false),
                    CanUsePlatformQuestionBank = table.Column<bool>(type: "bit", nullable: false),
                    CanCreatePrivateQuestions = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerSubscriptionCourses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartnerSubscriptionCourses_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PartnerSubscriptionCourses_PartnerSubscriptions_PartnerSubscriptionId",
                        column: x => x.PartnerSubscriptionId,
                        principalTable: "PartnerSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerSubscriptionCourses_CourseId",
                table: "PartnerSubscriptionCourses",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerSubscriptionCourses_PartnerSubscriptionId",
                table: "PartnerSubscriptionCourses",
                column: "PartnerSubscriptionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartnerSubscriptionCourses");
        }
    }
}
