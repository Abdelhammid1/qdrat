using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddProfessionalCertificatesModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProfessionalCertificateCourses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TitleAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StandardCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ShortDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FullDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IconClass = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BadgeText = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_ProfessionalCertificateCourses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProfessionalCertificateSectionSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Subtitle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ButtonText = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AutoCloseWhenMaxReached = table.Column<bool>(type: "bit", nullable: false),
                    GlobalMaxRequests = table.Column<int>(type: "int", nullable: true),
                    ClosedMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfessionalCertificateSectionSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProfessionalCertificateRegistrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfessionalCertificateCourseId = table.Column<int>(type: "int", nullable: false),
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
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfessionalCertificateRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfessionalCertificateRegistrations_ProfessionalCertificateCourses_ProfessionalCertificateCourseId",
                        column: x => x.ProfessionalCertificateCourseId,
                        principalTable: "ProfessionalCertificateCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "ProfessionalCertificateSectionSettings",
                columns: new[] { "Id", "AutoCloseWhenMaxReached", "ButtonText", "ClosedMessage", "Description", "GlobalMaxRequests", "IsEnabled", "Subtitle", "Title", "UpdatedAt", "UpdatedByUserId" },
                values: new object[] { 1, false, "سجل اهتمامك", null, "اختر البرنامج المناسب لك وسجّل بياناتك، وسيقوم فريق الدعم الفني بالتواصل معك لاستكمال تفاصيل الاشتراك.", null, true, "برامج متخصصة في السلامة والصحة المهنية بمعايير عالمية", "الشهادات الدولية الاحترافية", null, null });

            migrationBuilder.CreateIndex(
                name: "IX_ProfessionalCertificateCourses_Slug",
                table: "ProfessionalCertificateCourses",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProfessionalCertificateRegistrations_NationalId",
                table: "ProfessionalCertificateRegistrations",
                column: "NationalId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfessionalCertificateRegistrations_PhoneNumber",
                table: "ProfessionalCertificateRegistrations",
                column: "PhoneNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ProfessionalCertificateRegistrations_ProfessionalCertificateCourseId",
                table: "ProfessionalCertificateRegistrations",
                column: "ProfessionalCertificateCourseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfessionalCertificateRegistrations_Status",
                table: "ProfessionalCertificateRegistrations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProfessionalCertificateRegistrations_SubmittedAt",
                table: "ProfessionalCertificateRegistrations",
                column: "SubmittedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProfessionalCertificateRegistrations");

            migrationBuilder.DropTable(
                name: "ProfessionalCertificateSectionSettings");

            migrationBuilder.DropTable(
                name: "ProfessionalCertificateCourses");
        }
    }
}
