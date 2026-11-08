-- MSE: فحص صفوف الإجابة المكررة في اختبار معمل القياس — قراءة فقط، لا يغيّر أي بيانات.
-- النتيجة الأولى: كل سؤال له أكثر من صف إجابة لنفس المحاولة (هذه الحالة كانت تُسقط صفحة الحل بخطأ 500).
-- النتيجة الثانية: محاولات انقفل لها قسم مع وجود تكرار، فقد تكون درجتها المحفوظة مضخّمة.

SET NOCOUNT ON;

SELECT
    a.MinistrySimExamId,
    a.Id          AS AttemptId,
    a.StudentId,
    st.StageNumber,
    ans.MinistrySimExamStageQuestionId,
    COUNT(*)      AS RowsCount,
    MIN(ans.AnsweredAt) AS FirstAnsweredAt,
    MAX(ans.AnsweredAt) AS LastAnsweredAt
FROM MinistrySimExamStudentAnswers ans
JOIN MinistrySimExamStudentAttempts a ON a.Id = ans.MinistrySimExamStudentAttemptId
JOIN MinistrySimExamStageQuestions sq ON sq.Id = ans.MinistrySimExamStageQuestionId
JOIN MinistrySimExamStages st ON st.Id = sq.MinistrySimExamStageId
GROUP BY a.MinistrySimExamId, a.Id, a.StudentId, st.StageNumber, ans.MinistrySimExamStageQuestionId
HAVING COUNT(*) > 1
ORDER BY a.MinistrySimExamId, a.Id, st.StageNumber;

SELECT DISTINCT
    a.MinistrySimExamId,
    a.Id AS AttemptId,
    a.StudentId,
    a.IsCompleted,
    a.TotalScorePercent,
    p.StageNumber,
    p.StagePercentScore
FROM MinistrySimExamStudentAttempts a
JOIN MinistrySimExamStudentStageProgresses p ON p.MinistrySimExamStudentAttemptId = a.Id AND p.IsLocked = 1
WHERE EXISTS (
    SELECT 1
    FROM MinistrySimExamStudentAnswers ans
    JOIN MinistrySimExamStageQuestions sq ON sq.Id = ans.MinistrySimExamStageQuestionId
    JOIN MinistrySimExamStages st ON st.Id = sq.MinistrySimExamStageId
    WHERE ans.MinistrySimExamStudentAttemptId = a.Id
      AND st.StageNumber = p.StageNumber
    GROUP BY ans.MinistrySimExamStageQuestionId
    HAVING COUNT(*) > 1
)
ORDER BY a.MinistrySimExamId, a.Id, p.StageNumber;
