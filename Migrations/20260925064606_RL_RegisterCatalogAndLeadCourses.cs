using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class RL_RegisterCatalogAndLeadCourses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // الفهرس قد يكون غير موجود فعليًا في بعض القواعد (انحراف عن الـ Snapshot) — الحذف مشروط.
            // يحل محله الفهرس المركّب IX_Courses_ProjectId_ShowOnRegisterPage_IsActive الذي يغطي ProjectId.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Courses_ProjectId' AND object_id = OBJECT_ID(N'[Courses]'))
    DROP INDEX [IX_Courses_ProjectId] ON [Courses];");

            migrationBuilder.AddColumn<string>(
                name: "PublicDescription",
                table: "Projects",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegisterAccentColor",
                table: "Projects",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RegisterDisplayOrder",
                table: "Projects",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RegisterIcon",
                table: "Projects",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowOnRegisterPage",
                table: "Projects",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AdminNotes",
                table: "FrontendLeads",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApplicantType",
                table: "FrontendLeads",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "FrontendLeads",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ContactedAt",
                table: "FrontendLeads",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactedByUserId",
                table: "FrontendLeads",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Gender",
                table: "FrontendLeads",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdatedAt",
                table: "FrontendLeads",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "FrontendLeads",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParentName",
                table: "FrontendLeads",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParentPhone",
                table: "FrontendLeads",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SchoolStage",
                table: "FrontendLeads",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceIp",
                table: "FrontendLeads",
                type: "nvarchar(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "FrontendLeads",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PublicDescription",
                table: "Courses",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RegisterDisplayOrder",
                table: "Courses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "ShowOnRegisterPage",
                table: "Courses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "FrontendLeadCourses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FrontendLeadId = table.Column<int>(type: "int", nullable: false),
                    CourseId = table.Column<int>(type: "int", nullable: true),
                    ProjectId = table.Column<int>(type: "int", nullable: true),
                    CourseNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProjectNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FrontendLeadCourses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FrontendLeadCourses_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FrontendLeadCourses_FrontendLeads_FrontendLeadId",
                        column: x => x.FrontendLeadId,
                        principalTable: "FrontendLeads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ShowOnRegisterPage_IsActive",
                table: "Projects",
                columns: new[] { "ShowOnRegisterPage", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_FrontendLeads_Status_CreatedAt",
                table: "FrontendLeads",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Courses_ProjectId_ShowOnRegisterPage_IsActive",
                table: "Courses",
                columns: new[] { "ProjectId", "ShowOnRegisterPage", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_FrontendLeadCourses_CourseId",
                table: "FrontendLeadCourses",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_FrontendLeadCourses_FrontendLeadId",
                table: "FrontendLeadCourses",
                column: "FrontendLeadId");

            // RL-S2.4: ترحيل IsContacted → Status للطلبات الحالية (الأعمدة الجديدة تُضاف بقيمة 0)
            migrationBuilder.Sql(@"
UPDATE FrontendLeads
SET Status = CASE WHEN IsContacted = 1 THEN 2 ELSE 1 END
WHERE Status = 0 OR Status IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FrontendLeadCourses");

            migrationBuilder.DropIndex(
                name: "IX_Projects_ShowOnRegisterPage_IsActive",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_FrontendLeads_Status_CreatedAt",
                table: "FrontendLeads");

            migrationBuilder.DropIndex(
                name: "IX_Courses_ProjectId_ShowOnRegisterPage_IsActive",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "PublicDescription",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "RegisterAccentColor",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "RegisterDisplayOrder",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "RegisterIcon",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ShowOnRegisterPage",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "AdminNotes",
                table: "FrontendLeads");

            migrationBuilder.DropColumn(
                name: "ApplicantType",
                table: "FrontendLeads");

            migrationBuilder.DropColumn(
                name: "City",
                table: "FrontendLeads");

            migrationBuilder.DropColumn(
                name: "ContactedAt",
                table: "FrontendLeads");

            migrationBuilder.DropColumn(
                name: "ContactedByUserId",
                table: "FrontendLeads");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "FrontendLeads");

            migrationBuilder.DropColumn(
                name: "LastUpdatedAt",
                table: "FrontendLeads");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "FrontendLeads");

            migrationBuilder.DropColumn(
                name: "ParentName",
                table: "FrontendLeads");

            migrationBuilder.DropColumn(
                name: "ParentPhone",
                table: "FrontendLeads");

            migrationBuilder.DropColumn(
                name: "SchoolStage",
                table: "FrontendLeads");

            migrationBuilder.DropColumn(
                name: "SourceIp",
                table: "FrontendLeads");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "FrontendLeads");

            migrationBuilder.DropColumn(
                name: "PublicDescription",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "RegisterDisplayOrder",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "ShowOnRegisterPage",
                table: "Courses");

            migrationBuilder.CreateIndex(
                name: "IX_Courses_ProjectId",
                table: "Courses",
                column: "ProjectId");
        }
    }
}
