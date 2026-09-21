using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class Add_MinistrySimExam_OnlineOnsite_ReferenceCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // القيمة الافتراضية true إلزامية للصفوف الموجودة فعليًا قبل هذا الـMigration — كانت هذه الإسنادات "أونلاين"
            // ضمنيًا (لا بوابة رمز كانت موجودة أصلاً)، فتصبح false هنا ستقفل كل طالب/دفعة مُسنَد له سابقًا خلف رمز غير موجود.
            migrationBuilder.AddColumn<bool>(
                name: "IsOnline",
                table: "MinistrySimExamAssignmentsToStudents",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceCode",
                table: "MinistrySimExamAssignmentsToStudents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOnline",
                table: "MinistrySimExamAssignmentsToBatches",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceCode",
                table: "MinistrySimExamAssignmentsToBatches",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsOnline",
                table: "MinistrySimExamAssignmentsToStudents");

            migrationBuilder.DropColumn(
                name: "ReferenceCode",
                table: "MinistrySimExamAssignmentsToStudents");

            migrationBuilder.DropColumn(
                name: "IsOnline",
                table: "MinistrySimExamAssignmentsToBatches");

            migrationBuilder.DropColumn(
                name: "ReferenceCode",
                table: "MinistrySimExamAssignmentsToBatches");
        }
    }
}
