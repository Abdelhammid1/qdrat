using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace QdratNew.Migrations
{
    public partial class AddHomeworkSetAttemptsAndRelations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // إنشاء جدول HomeworkSetAttempts
            migrationBuilder.CreateTable(
                name: "HomeworkSetAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HomeworkSetId = table.Column<int>(nullable: false),
                    StudentId = table.Column<int>(nullable: false),
                    AttemptNumber = table.Column<int>(nullable: false),
                    StartedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "GETDATE()"),
                    SubmittedAt = table.Column<DateTime>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeworkSetAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HomeworkSetAttempts_HomeworkSets_HomeworkSetId",
                        column: x => x.HomeworkSetId,
                        principalTable: "HomeworkSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HomeworkSetAttempts_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "StudentID",
                        onDelete: ReferentialAction.Restrict); // ✅
                });

            // إنشاء جدول HomeworkSetSections
            migrationBuilder.CreateTable(
                name: "HomeworkSetSections",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HomeworkSetId = table.Column<int>(nullable: false),
                    SectionId = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeworkSetSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HomeworkSetSections_HomeworkSets_HomeworkSetId",
                        column: x => x.HomeworkSetId,
                        principalTable: "HomeworkSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HomeworkSetSections_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // إنشاء جدول HomeworkSetStudents
            migrationBuilder.CreateTable(
                name: "HomeworkSetStudents",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HomeworkSetId = table.Column<int>(nullable: false),
                    StudentId = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeworkSetStudents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HomeworkSetStudents_HomeworkSets_HomeworkSetId",
                        column: x => x.HomeworkSetId,
                        principalTable: "HomeworkSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HomeworkSetStudents_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "StudentID",
                        onDelete: ReferentialAction.Restrict); // ✅
                });

            // الفهارس
            migrationBuilder.CreateIndex(
                name: "IX_HomeworkSetAttempts_HomeworkSetId",
                table: "HomeworkSetAttempts",
                column: "HomeworkSetId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkSetAttempts_StudentId",
                table: "HomeworkSetAttempts",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkSetSections_HomeworkSetId",
                table: "HomeworkSetSections",
                column: "HomeworkSetId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkSetSections_SectionId",
                table: "HomeworkSetSections",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkSetStudents_HomeworkSetId",
                table: "HomeworkSetStudents",
                column: "HomeworkSetId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkSetStudents_StudentId",
                table: "HomeworkSetStudents",
                column: "StudentId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("HomeworkSetAttempts");
            migrationBuilder.DropTable("HomeworkSetSections");
            migrationBuilder.DropTable("HomeworkSetStudents");
        }
    }
}
