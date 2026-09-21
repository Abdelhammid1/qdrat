using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminPermissionProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AdminUserProfiles_AdminPermissionProfiles_ProfileId",
                table: "AdminUserProfiles");

            migrationBuilder.RenameColumn(
                name: "ProfileId",
                table: "AdminUserProfiles",
                newName: "AdminProfileId");

            migrationBuilder.RenameIndex(
                name: "IX_AdminUserProfiles_ProfileId",
                table: "AdminUserProfiles",
                newName: "IX_AdminUserProfiles_AdminProfileId");

            migrationBuilder.CreateTable(
                name: "AdminProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsSystemProfile = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdminProfileControllerPermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdminProfileId = table.Column<int>(type: "int", nullable: false),
                    ControllerName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    AccessLevel = table.Column<int>(type: "int", nullable: false),
                    HasCustomPermissions = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminProfileControllerPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminProfileControllerPermissions_AdminProfiles_AdminProfileId",
                        column: x => x.AdminProfileId,
                        principalTable: "AdminProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AdminProfileCustomPermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdminProfileControllerPermissionId = table.Column<int>(type: "int", nullable: false),
                    ActionName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsAllowed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminProfileCustomPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminProfileCustomPermissions_AdminProfileControllerPermissions_AdminProfileControllerPermissionId",
                        column: x => x.AdminProfileControllerPermissionId,
                        principalTable: "AdminProfileControllerPermissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminProfileControllerPermissions_AdminProfileId_ControllerName",
                table: "AdminProfileControllerPermissions",
                columns: new[] { "AdminProfileId", "ControllerName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminProfileCustomPermissions_AdminProfileControllerPermissionId_ActionName",
                table: "AdminProfileCustomPermissions",
                columns: new[] { "AdminProfileControllerPermissionId", "ActionName" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AdminUserProfiles_AdminProfiles_AdminProfileId",
                table: "AdminUserProfiles",
                column: "AdminProfileId",
                principalTable: "AdminProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AdminUserProfiles_AdminProfiles_AdminProfileId",
                table: "AdminUserProfiles");

            migrationBuilder.DropTable(
                name: "AdminProfileCustomPermissions");

            migrationBuilder.DropTable(
                name: "AdminProfileControllerPermissions");

            migrationBuilder.DropTable(
                name: "AdminProfiles");

            migrationBuilder.RenameColumn(
                name: "AdminProfileId",
                table: "AdminUserProfiles",
                newName: "ProfileId");

            migrationBuilder.RenameIndex(
                name: "IX_AdminUserProfiles_AdminProfileId",
                table: "AdminUserProfiles",
                newName: "IX_AdminUserProfiles_ProfileId");

            migrationBuilder.AddForeignKey(
                name: "FK_AdminUserProfiles_AdminPermissionProfiles_ProfileId",
                table: "AdminUserProfiles",
                column: "ProfileId",
                principalTable: "AdminPermissionProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
