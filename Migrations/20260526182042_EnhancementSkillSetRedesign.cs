using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class EnhancementSkillSetRedesign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. إسقاط الـ FK أولاً (إذا كان موجوداً)
            migrationBuilder.Sql(@"
                DECLARE @fk NVARCHAR(256) = (
                    SELECT fk.name FROM sys.foreign_keys fk
                    WHERE fk.parent_object_id = OBJECT_ID('EnhancementSkillSets')
                      AND fk.name LIKE '%Lecture%'
                )
                IF @fk IS NOT NULL
                    EXEC('ALTER TABLE EnhancementSkillSets DROP CONSTRAINT [' + @fk + ']')
            ");

            // 2. تعديل العمود ليصبح nullable
            migrationBuilder.AlterColumn<int>(
                name: "LectureId",
                table: "EnhancementSkillSets",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            // 3. تصفير LectureId للسجلات التي لا تشير لمحاضرة موجودة (بعد أن أصبح nullable)
            migrationBuilder.Sql(@"
                UPDATE EnhancementSkillSets
                SET LectureId = NULL
                WHERE LectureId IS NOT NULL
                  AND LectureId NOT IN (SELECT Id FROM Lecture)
            ");

            migrationBuilder.AddColumn<int>(
                name: "GenerationMethod",
                table: "EnhancementSkillSets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProfessionalModelId",
                table: "EnhancementSkillSets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledSendAt",
                table: "EnhancementSkillSets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EnhancementSetIndicators",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnhancementSkillSetId = table.Column<int>(type: "int", nullable: false),
                    LessonId = table.Column<int>(type: "int", nullable: false),
                    QuestionCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnhancementSetIndicators", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnhancementSetIndicators_EnhancementSkillSets_EnhancementSkillSetId",
                        column: x => x.EnhancementSkillSetId,
                        principalTable: "EnhancementSkillSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EnhancementSetIndicators_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EnhancementSkillSets_ProfessionalModelId",
                table: "EnhancementSkillSets",
                column: "ProfessionalModelId");

            migrationBuilder.CreateIndex(
                name: "IX_EnhancementSetIndicators_EnhancementSkillSetId",
                table: "EnhancementSetIndicators",
                column: "EnhancementSkillSetId");

            migrationBuilder.CreateIndex(
                name: "IX_EnhancementSetIndicators_LessonId",
                table: "EnhancementSetIndicators",
                column: "LessonId");

            // LectureId FK مُهمَل — الحقل أصبح اختيارياً ولا داعي لـ FK صارم
            migrationBuilder.AddForeignKey(
                name: "FK_EnhancementSkillSets_ProfessionalModels_ProfessionalModelId",
                table: "EnhancementSkillSets",
                column: "ProfessionalModelId",
                principalTable: "ProfessionalModels",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EnhancementSkillSets_Lecture_LectureId",
                table: "EnhancementSkillSets");

            migrationBuilder.DropForeignKey(
                name: "FK_EnhancementSkillSets_ProfessionalModels_ProfessionalModelId",
                table: "EnhancementSkillSets");

            migrationBuilder.DropTable(
                name: "EnhancementSetIndicators");

            migrationBuilder.DropIndex(
                name: "IX_EnhancementSkillSets_ProfessionalModelId",
                table: "EnhancementSkillSets");

            migrationBuilder.DropColumn(
                name: "GenerationMethod",
                table: "EnhancementSkillSets");

            migrationBuilder.DropColumn(
                name: "ProfessionalModelId",
                table: "EnhancementSkillSets");

            migrationBuilder.DropColumn(
                name: "ScheduledSendAt",
                table: "EnhancementSkillSets");

            migrationBuilder.AlterColumn<int>(
                name: "LectureId",
                table: "EnhancementSkillSets",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_EnhancementSkillSets_Lecture_LectureId",
                table: "EnhancementSkillSets",
                column: "LectureId",
                principalTable: "Lecture",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
