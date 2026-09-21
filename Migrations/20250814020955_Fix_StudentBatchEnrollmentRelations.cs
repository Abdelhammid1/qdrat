using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class Fix_StudentBatchEnrollmentRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // هنا تحط الأكواد اللي تعدل العلاقات الفعلية لو فيه حاجة ناقصة
            // لكن ما تحاولش تحذف مفاتيح أو أعمدة مش موجودة
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CourseId1",
                table: "StudentCourses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BatchId1",
                table: "StudentBatchEnrollments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StudentID1",
                table: "StudentBatchEnrollments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentCourses_CourseId1",
                table: "StudentCourses",
                column: "CourseId1");

            migrationBuilder.CreateIndex(
                name: "IX_StudentBatchEnrollments_BatchId1",
                table: "StudentBatchEnrollments",
                column: "BatchId1");

            migrationBuilder.CreateIndex(
                name: "IX_StudentBatchEnrollments_StudentID1",
                table: "StudentBatchEnrollments",
                column: "StudentID1");

            migrationBuilder.AddForeignKey(
                name: "FK_StudentBatchEnrollments_Batches_BatchId1",
                table: "StudentBatchEnrollments",
                column: "BatchId1",
                principalTable: "Batches",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_StudentBatchEnrollments_Students_StudentID1",
                table: "StudentBatchEnrollments",
                column: "StudentID1",
                principalTable: "Students",
                principalColumn: "StudentID");

            migrationBuilder.AddForeignKey(
                name: "FK_StudentCourses_Courses_CourseId1",
                table: "StudentCourses",
                column: "CourseId1",
                principalTable: "Courses",
                principalColumn: "Id");
        }
    }
}
