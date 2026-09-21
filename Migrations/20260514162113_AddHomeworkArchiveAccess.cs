using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddHomeworkArchiveAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HomeworkArchiveAccesses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HomeworkSetId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    GrantedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeworkArchiveAccesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HomeworkArchiveAccesses_AspNetUsers_GrantedByUserId",
                        column: x => x.GrantedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HomeworkArchiveAccesses_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HomeworkArchiveAccesses_HomeworkSets_HomeworkSetId",
                        column: x => x.HomeworkSetId,
                        principalTable: "HomeworkSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkArchiveAccesses_GrantedByUserId",
                table: "HomeworkArchiveAccesses",
                column: "GrantedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkArchiveAccesses_HomeworkSetId_UserId",
                table: "HomeworkArchiveAccesses",
                columns: new[] { "HomeworkSetId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkArchiveAccesses_UserId",
                table: "HomeworkArchiveAccesses",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HomeworkArchiveAccesses");
        }
    }
}
