using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class MSE_MultiAxisSelectionIsQuant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsQuant",
                table: "MinistrySimExamStageIndicatorSelections",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // البيانات القديمة: كل اختيار كان إما للمحور الكمي الأساسي للقسم أو للفظي — نُعيّن الفرع من المحور الأساسي
            migrationBuilder.Sql(@"
UPDATE sel
SET sel.IsQuant = 1
FROM MinistrySimExamStageIndicatorSelections AS sel
INNER JOIN MinistrySimExamStages AS st ON st.Id = sel.MinistrySimExamStageId
WHERE sel.SectionId = st.QuantSectionId;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsQuant",
                table: "MinistrySimExamStageIndicatorSelections");
        }
    }
}
