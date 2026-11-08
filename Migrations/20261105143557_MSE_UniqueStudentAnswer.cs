using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class MSE_UniqueStudentAnswer : Migration
    {
        // يبقي أحدث صف لكل (محاولة، سؤال قسم)، وينسخ الأقدم إلى جدول احتياطي قبل حذفه.
        // لا يغيّر StagePercentScore ولا TotalScorePercent: إعادة حساب الدرجات المتأثرة قرار منفصل مؤجل.
        public const string DedupeSql = @"
IF OBJECT_ID(N'MinistrySimExamStudentAnswers_DuplicatesBackup', N'U') IS NULL
    CREATE TABLE [MinistrySimExamStudentAnswers_DuplicatesBackup] (
        [Id] int NOT NULL PRIMARY KEY,
        [MinistrySimExamStudentAttemptId] int NOT NULL,
        [MinistrySimExamStageQuestionId] int NOT NULL,
        [SelectedOptionId] int NULL,
        [IsCorrect] bit NULL,
        [AnsweredAt] datetime2 NOT NULL,
        [IsFlaggedForReview] bit NOT NULL,
        [BackedUpAt] datetime2 NOT NULL DEFAULT (GETDATE())
    );

INSERT INTO [MinistrySimExamStudentAnswers_DuplicatesBackup]
    ([Id], [MinistrySimExamStudentAttemptId], [MinistrySimExamStageQuestionId], [SelectedOptionId], [IsCorrect], [AnsweredAt], [IsFlaggedForReview])
SELECT a.[Id], a.[MinistrySimExamStudentAttemptId], a.[MinistrySimExamStageQuestionId], a.[SelectedOptionId], a.[IsCorrect], a.[AnsweredAt], a.[IsFlaggedForReview]
FROM [MinistrySimExamStudentAnswers] a
WHERE EXISTS (
    SELECT 1 FROM [MinistrySimExamStudentAnswers] newer
    WHERE newer.[MinistrySimExamStudentAttemptId] = a.[MinistrySimExamStudentAttemptId]
      AND newer.[MinistrySimExamStageQuestionId] = a.[MinistrySimExamStageQuestionId]
      AND newer.[Id] > a.[Id]);

DELETE a
FROM [MinistrySimExamStudentAnswers] a
WHERE EXISTS (
    SELECT 1 FROM [MinistrySimExamStudentAnswers] newer
    WHERE newer.[MinistrySimExamStudentAttemptId] = a.[MinistrySimExamStudentAttemptId]
      AND newer.[MinistrySimExamStageQuestionId] = a.[MinistrySimExamStageQuestionId]
      AND newer.[Id] > a.[Id]);
";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DedupeSql);

            migrationBuilder.DropIndex(
                name: "IX_MinistrySimExamStudentAnswers_MinistrySimExamStudentAttemptId",
                table: "MinistrySimExamStudentAnswers");

            migrationBuilder.CreateIndex(
                name: "IX_MinistrySimExamStudentAnswers_MinistrySimExamStudentAttemptId_MinistrySimExamStageQuestionId",
                table: "MinistrySimExamStudentAnswers",
                columns: new[] { "MinistrySimExamStudentAttemptId", "MinistrySimExamStageQuestionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MinistrySimExamStudentAnswers_MinistrySimExamStudentAttemptId_MinistrySimExamStageQuestionId",
                table: "MinistrySimExamStudentAnswers");

            migrationBuilder.CreateIndex(
                name: "IX_MinistrySimExamStudentAnswers_MinistrySimExamStudentAttemptId",
                table: "MinistrySimExamStudentAnswers",
                column: "MinistrySimExamStudentAttemptId");
        }
    }
}
