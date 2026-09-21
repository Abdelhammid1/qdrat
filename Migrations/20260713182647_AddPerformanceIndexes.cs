using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ⚠️ الفهارس القديمة (IX_QuestionOptions_QuestionId, IX_Notifications_StudentID,
            // IX_Homeworks_StudentId) قد تكون موجودة أو غير موجودة حسب حالة كل قاعدة بيانات
            // (انحراف عن الـ Model Snapshot). نستخدم إسقاط شرطي حتى تعمل المايجريشن على أي بيئة.
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_QuestionOptions_QuestionId' AND object_id = OBJECT_ID(N'dbo.QuestionOptions'))
                    DROP INDEX IX_QuestionOptions_QuestionId ON dbo.QuestionOptions;

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Notifications_StudentID' AND object_id = OBJECT_ID(N'dbo.Notifications'))
                    DROP INDEX IX_Notifications_StudentID ON dbo.Notifications;

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Homeworks_StudentId' AND object_id = OBJECT_ID(N'dbo.Homeworks'))
                    DROP INDEX IX_Homeworks_StudentId ON dbo.Homeworks;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionOptions_QuestionId",
                table: "QuestionOptions",
                column: "QuestionId")
                .Annotation("SqlServer:Include", new[] { "ImageUrl", "Text" });

            migrationBuilder.CreateIndex(
                name: "IX_QuestionAttemptNew_StudentId_HomeworkSetId",
                table: "QuestionAttemptNew",
                columns: new[] { "StudentId", "HomeworkSetId" })
                .Annotation("SqlServer:Include", new[] { "QuestionId", "IsCorrect", "AttemptedAt", "SelectedAnswer", "TimeTakenSeconds" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_StudentId_IsRead",
                table: "Notifications",
                columns: new[] { "StudentID", "IsRead" })
                .Annotation("SqlServer:Include", new[] { "Category", "SentAt", "TargetUrl" });

            migrationBuilder.CreateIndex(
                name: "IX_Homeworks_StudentId_HomeworkSetId",
                table: "Homeworks",
                columns: new[] { "StudentId", "HomeworkSetId" })
                .Annotation("SqlServer:Include", new[] { "QuestionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QuestionOptions_QuestionId",
                table: "QuestionOptions");

            migrationBuilder.DropIndex(
                name: "IX_QuestionAttemptNew_StudentId_HomeworkSetId",
                table: "QuestionAttemptNew");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_StudentId_IsRead",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Homeworks_StudentId_HomeworkSetId",
                table: "Homeworks");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionOptions_QuestionId",
                table: "QuestionOptions",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_StudentID",
                table: "Notifications",
                column: "StudentID");

            migrationBuilder.CreateIndex(
                name: "IX_Homeworks_StudentId",
                table: "Homeworks",
                column: "StudentId");
        }
    }
}
