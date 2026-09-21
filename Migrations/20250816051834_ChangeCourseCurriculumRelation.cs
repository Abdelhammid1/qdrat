using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class ChangeCourseCurriculumRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. إنشاء جدول CourseCurriculums
            migrationBuilder.CreateTable(
                name: "CourseCurriculums",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CourseId = table.Column<int>(type: "int", nullable: false),
                    CurriculumId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseCurriculums", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseCurriculums_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CourseCurriculums_Curriculums_CurriculumId",
                        column: x => x.CurriculumId,
                        principalTable: "Curriculums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourseCurriculums_CourseId",
                table: "CourseCurriculums",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseCurriculums_CurriculumId",
                table: "CourseCurriculums",
                column: "CurriculumId");

            // 2. ترحيل البيانات من العمود CourseId القديم إلى الجدول الجديد
            migrationBuilder.Sql(@"
                INSERT INTO CourseCurriculums (CourseId, CurriculumId)
                SELECT CourseId, Id FROM Curriculums WHERE CourseId IS NOT NULL
            ");

            // 3. حذف العلاقة القديمة (FK + العمود)
            migrationBuilder.DropForeignKey(
                name: "FK_Curriculums_Courses_CourseId",
                table: "Curriculums");

            // أحيانًا الـ Index ده مش موجود → نخليه معلق
            // migrationBuilder.DropIndex(
            //     name: "IX_Curriculums_CourseId",
            //     table: "Curriculums");

            migrationBuilder.DropColumn(
                name: "CourseId",
                table: "Curriculums");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 1. رجوع العمود CourseId في Curriculums
            migrationBuilder.AddColumn<int>(
                name: "CourseId",
                table: "Curriculums",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Curriculums_CourseId",
                table: "Curriculums",
                column: "CourseId");

            migrationBuilder.AddForeignKey(
                name: "FK_Curriculums_Courses_CourseId",
                table: "Curriculums",
                column: "CourseId",
                principalTable: "Courses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // 2. إعادة ترحيل البيانات من CourseCurriculums إلى العمود القديم
            migrationBuilder.Sql(@"
                UPDATE C
                SET C.CourseId = CC.CourseId
                FROM Curriculums C
                INNER JOIN CourseCurriculums CC ON CC.CurriculumId = C.Id
            ");

            // 3. حذف جدول CourseCurriculums
            migrationBuilder.DropTable(
                name: "CourseCurriculums");
        }
    }
}
