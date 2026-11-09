-- OLC-S8.4 — تصدير Schema جداول عقد المحتوى التعليمي أونلاين (v1.2) من قاعدة الإنتاج بعد النشر
-- قراءة فقط: لا يعدّل أي شيء. الناتج يُسلَّم لخطة OLN (OLN-S8.1) لمقارنته بـ Migration الـ PostgreSQL قبل الـ Cutover.
--
-- التشغيل (على السيرفر، الناتج CSV بفاصل |):
--   sqlcmd -S . -E -d QdratNewDB -i OLC_ExportContractSchema.sql -o olc_schema_<التاريخ>.txt -s "|" -W
-- لا يحتوي الناتج على أي بيانات طلاب أو أسرار — أسماء أعمدة وأنواع وفهارس فقط.

SET NOCOUNT ON;

-- 1) الأعمدة: الاسم، النوع، الطول، القابلية لـ NULL، الافتراضي، الهوية
SELECT
    t.name                                   AS TableName,
    c.column_id                              AS Ordinal,
    c.name                                   AS ColumnName,
    ty.name                                  AS DataType,
    CASE WHEN ty.name IN (N'nvarchar', N'nchar') AND c.max_length > 0 THEN c.max_length / 2
         WHEN c.max_length = -1 THEN -1
         ELSE c.max_length END               AS MaxLength,
    c.is_nullable                            AS IsNullable,
    c.is_identity                            AS IsIdentity,
    dc.definition                            AS DefaultValue
FROM sys.tables t
JOIN sys.columns c       ON c.object_id = t.object_id
JOIN sys.types ty        ON ty.user_type_id = c.user_type_id
LEFT JOIN sys.default_constraints dc ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
WHERE t.name IN (N'LectureContents', N'LectureContentItems', N'LectureContentProgress', N'LectureContentItemProgress')
   OR (t.name = N'Batches' AND c.name IN (N'IsOnlineLearning', N'OnlineLearningEnabledAt'))
ORDER BY t.name, c.column_id;

-- 2) الفهارس: الاسم، فريد؟، الأعمدة بالترتيب، الفلتر
SELECT
    t.name        AS TableName,
    i.name        AS IndexName,
    i.is_unique   AS IsUnique,
    i.is_primary_key AS IsPrimaryKey,
    STUFF((SELECT N',' + col.name
           FROM sys.index_columns ic
           JOIN sys.columns col ON col.object_id = ic.object_id AND col.column_id = ic.column_id
           WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
           ORDER BY ic.key_ordinal
           FOR XML PATH(N'')), 1, 1, N'') AS KeyColumns,
    i.filter_definition AS FilterDefinition
FROM sys.indexes i
JOIN sys.tables t ON t.object_id = i.object_id
WHERE t.name IN (N'LectureContents', N'LectureContentItems', N'LectureContentProgress', N'LectureContentItemProgress')
  AND i.type > 0
ORDER BY t.name, i.name;

-- 3) المفاتيح الأجنبية وسلوك الحذف
SELECT
    tp.name  AS TableName,
    fk.name  AS ForeignKeyName,
    cp.name  AS ColumnName,
    tr.name  AS ReferencedTable,
    cr.name  AS ReferencedColumn,
    fk.delete_referential_action_desc AS OnDelete
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.tables tp  ON tp.object_id = fk.parent_object_id
JOIN sys.columns cp ON cp.object_id = fkc.parent_object_id AND cp.column_id = fkc.parent_column_id
JOIN sys.tables tr  ON tr.object_id = fk.referenced_object_id
JOIN sys.columns cr ON cr.object_id = fkc.referenced_object_id AND cr.column_id = fkc.referenced_column_id
WHERE tp.name IN (N'LectureContents', N'LectureContentItems', N'LectureContentProgress', N'LectureContentItemProgress')
ORDER BY tp.name, fk.name;

-- 4) أعداد الصفوف (لمطابقة النقل C.6-4 لاحقًا) + الـ Migration المطبّقة
SELECT N'LectureContents' AS TableName, COUNT_BIG(*) AS RowsCount FROM LectureContents
UNION ALL SELECT N'LectureContentItems', COUNT_BIG(*) FROM LectureContentItems
UNION ALL SELECT N'LectureContentProgress', COUNT_BIG(*) FROM LectureContentProgress
UNION ALL SELECT N'LectureContentItemProgress', COUNT_BIG(*) FROM LectureContentItemProgress
UNION ALL SELECT N'Batches(IsOnlineLearning=1)', COUNT_BIG(*) FROM Batches WHERE IsOnlineLearning = 1;

SELECT MigrationId, ProductVersion FROM __EFMigrationsHistory WHERE MigrationId LIKE N'%OLC%';
