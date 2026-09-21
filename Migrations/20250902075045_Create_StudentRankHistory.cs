using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    public partial class Create_StudentRankHistory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentRankHistories",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(nullable: false),
                    BatchId = table.Column<int>(nullable: false),
                    Rank = table.Column<int>(nullable: false),
                    TotalStudents = table.Column<int>(nullable: false),
                    RecordedAt = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentRankHistories", x => x.Id);

                    table.ForeignKey(
                        name: "FK_StudentRankHistories_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "StudentID",   // عمود المفتاح في Students
                        onDelete: ReferentialAction.Restrict);

                    table.ForeignKey(
                        name: "FK_StudentRankHistories_Batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "Batches",
                        principalColumn: "Id",          // ✅ استبدل حسب العمود الصحيح
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentRankHistories_StudentId",
                table: "StudentRankHistories",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentRankHistories_BatchId",
                table: "StudentRankHistories",
                column: "BatchId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentRankHistories");
        }
    }
}
