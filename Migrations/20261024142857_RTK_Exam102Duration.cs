using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class RTK_Exam102Duration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Exam102DurationMinutes",
                table: "RemedialTrackAxes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // الخطط الحالية: اختبار 102 يحتفظ بنفس المدة المستخدمة سابقًا (لا تغيير في السلوك)
            migrationBuilder.Sql("UPDATE [RemedialTrackAxes] SET [Exam102DurationMinutes] = [ExamDurationMinutes]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Exam102DurationMinutes",
                table: "RemedialTrackAxes");
        }
    }
}
