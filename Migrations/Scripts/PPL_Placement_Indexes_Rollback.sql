-- PPL-S7.2 (تراجع): يحذف فهارس PPL_Placement_Indexes.sql. لا يمس أي بيانات، والكود يعمل بدونها لكن أبطأ.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ExamStudentStatuses_ExamId' AND object_id = OBJECT_ID(N'[ExamStudentStatuses]'))
    DROP INDEX [IX_ExamStudentStatuses_ExamId] ON [ExamStudentStatuses];
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ExamAssignmentsToBatches_ExamId' AND object_id = OBJECT_ID(N'[ExamAssignmentsToBatches]'))
    DROP INDEX [IX_ExamAssignmentsToBatches_ExamId] ON [ExamAssignmentsToBatches];
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ExamAssignmentsToBatches_BatchId' AND object_id = OBJECT_ID(N'[ExamAssignmentsToBatches]'))
    DROP INDEX [IX_ExamAssignmentsToBatches_BatchId] ON [ExamAssignmentsToBatches];
GO
