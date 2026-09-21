using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseCollectionsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CourseCollections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Subtitle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ButtonText = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LogoPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisplayStyle = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ShowOnHomePage = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    AutoCloseWhenMaxReached = table.Column<bool>(type: "bit", nullable: false),
                    GlobalMaxRequests = table.Column<int>(type: "int", nullable: true),
                    ClosedMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseCollections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CourseCollectionCourses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CourseCollectionId = table.Column<int>(type: "int", nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StandardCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Slug = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ShortDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FullDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IconClass = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BadgeText = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LogoPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ShowOnHomePage = table.Column<bool>(type: "bit", nullable: false),
                    IsRegistrationOpen = table.Column<bool>(type: "bit", nullable: false),
                    MaxRequests = table.Column<int>(type: "int", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseCollectionCourses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseCollectionCourses_CourseCollections_CourseCollectionId",
                        column: x => x.CourseCollectionId,
                        principalTable: "CourseCollections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CourseCollectionSponsors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CourseCollectionId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LogoPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    LinkUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ShowOnHomePage = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseCollectionSponsors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseCollectionSponsors_CourseCollections_CourseCollectionId",
                        column: x => x.CourseCollectionId,
                        principalTable: "CourseCollections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CourseCollectionRegistrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CourseCollectionCourseId = table.Column<int>(type: "int", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NationalId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AdminNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsContacted = table.Column<bool>(type: "bit", nullable: false),
                    ContactedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FollowUpAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HandledByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    HandledByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RegistrationBatchId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseCollectionRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseCollectionRegistrations_CourseCollectionCourses_CourseCollectionCourseId",
                        column: x => x.CourseCollectionCourseId,
                        principalTable: "CourseCollectionCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourseCollectionCourses_CourseCollectionId",
                table: "CourseCollectionCourses",
                column: "CourseCollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseCollectionCourses_CourseCollectionId_Slug",
                table: "CourseCollectionCourses",
                columns: new[] { "CourseCollectionId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseCollectionRegistrations_CourseCollectionCourseId",
                table: "CourseCollectionRegistrations",
                column: "CourseCollectionCourseId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseCollectionRegistrations_NationalId",
                table: "CourseCollectionRegistrations",
                column: "NationalId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseCollectionRegistrations_PhoneNumber",
                table: "CourseCollectionRegistrations",
                column: "PhoneNumber");

            migrationBuilder.CreateIndex(
                name: "IX_CourseCollectionRegistrations_Status",
                table: "CourseCollectionRegistrations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CourseCollectionRegistrations_SubmittedAt",
                table: "CourseCollectionRegistrations",
                column: "SubmittedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CourseCollections_Slug",
                table: "CourseCollections",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseCollectionSponsors_CourseCollectionId",
                table: "CourseCollectionSponsors",
                column: "CourseCollectionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourseCollectionRegistrations");

            migrationBuilder.DropTable(
                name: "CourseCollectionSponsors");

            migrationBuilder.DropTable(
                name: "CourseCollectionCourses");

            migrationBuilder.DropTable(
                name: "CourseCollections");
        }
    }
}
