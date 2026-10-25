using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddCoursePackages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CoursePackages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    GradeLabel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Subtitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IntroText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AccentColor = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    DiscountPercent = table.Column<byte>(type: "tinyint", nullable: true),
                    DiscountCaption = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ButtonText = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    PosterImagePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ShowOnHomePage = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoursePackages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CoursePackageItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CoursePackageId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IconKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ColorHex = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                    ScheduleTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ScheduleText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ScheduleNote = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoursePackageItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoursePackageItems_CoursePackages_CoursePackageId",
                        column: x => x.CoursePackageId,
                        principalTable: "CoursePackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CoursePackageRegistrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CoursePackageId = table.Column<int>(type: "int", nullable: false),
                    PackageNameSnapshot = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AdminNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ContactedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HandledByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoursePackageRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoursePackageRegistrations_CoursePackages_CoursePackageId",
                        column: x => x.CoursePackageId,
                        principalTable: "CoursePackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CoursePackageTags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CoursePackageId = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IconKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoursePackageTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoursePackageTags_CoursePackages_CoursePackageId",
                        column: x => x.CoursePackageId,
                        principalTable: "CoursePackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoursePackageItems_CoursePackageId",
                table: "CoursePackageItems",
                column: "CoursePackageId");

            migrationBuilder.CreateIndex(
                name: "IX_CoursePackageRegistrations_CoursePackageId_PhoneNumber_SubmittedAt",
                table: "CoursePackageRegistrations",
                columns: new[] { "CoursePackageId", "PhoneNumber", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CoursePackageRegistrations_Status_SubmittedAt",
                table: "CoursePackageRegistrations",
                columns: new[] { "Status", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CoursePackages_IsActive_ShowOnHomePage_DisplayOrder",
                table: "CoursePackages",
                columns: new[] { "IsActive", "ShowOnHomePage", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_CoursePackages_Slug",
                table: "CoursePackages",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CoursePackageTags_CoursePackageId",
                table: "CoursePackageTags",
                column: "CoursePackageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoursePackageItems");

            migrationBuilder.DropTable(
                name: "CoursePackageRegistrations");

            migrationBuilder.DropTable(
                name: "CoursePackageTags");

            migrationBuilder.DropTable(
                name: "CoursePackages");
        }
    }
}
