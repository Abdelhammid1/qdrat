using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class Add_IntegrityViolationLog_UniqueFilteredIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_IntegrityViolationLogs_ActiveViolation",
                table: "IntegrityViolationLogs",
                columns: new[] { "StudentId", "AttemptType", "AttemptEntityId" },
                unique: true,
                filter: "[IsResolved] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IntegrityViolationLogs_ActiveViolation",
                table: "IntegrityViolationLogs");
        }
    }
}
