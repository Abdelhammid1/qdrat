using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminPermissionSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminPermissionModules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminPermissionModules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdminPermissionProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsSystemProfile = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminPermissionProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdminPermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfileId = table.Column<int>(type: "int", nullable: false),
                    ModuleId = table.Column<int>(type: "int", nullable: false),
                    AccessLevel = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminPermissions_AdminPermissionModules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "AdminPermissionModules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AdminPermissions_AdminPermissionProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "AdminPermissionProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AdminUserProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProfileId = table.Column<int>(type: "int", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminUserProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminUserProfiles_AdminPermissionProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "AdminPermissionProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AdminUserProfiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminPermissionModules_Key",
                table: "AdminPermissionModules",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminPermissions_ModuleId",
                table: "AdminPermissions",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminPermissions_ProfileId_ModuleId",
                table: "AdminPermissions",
                columns: new[] { "ProfileId", "ModuleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminUserProfiles_ProfileId",
                table: "AdminUserProfiles",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminUserProfiles_UserId",
                table: "AdminUserProfiles",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminPermissions");

            migrationBuilder.DropTable(
                name: "AdminUserProfiles");

            migrationBuilder.DropTable(
                name: "AdminPermissionModules");

            migrationBuilder.DropTable(
                name: "AdminPermissionProfiles");
        }
    }
}
