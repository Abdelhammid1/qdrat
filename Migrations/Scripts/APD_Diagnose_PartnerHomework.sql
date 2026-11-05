-- APD-S1.1 (تشخيص، قراءة فقط): واجبات أرسلها الشريك من منطقته قبل APD وما زالت OwnerPartnerId = null،
-- فتظهر للأدمن كما قبل APD. لا يغيّر أي بيانات؛ قرار نقل ملكيتها لاحقًا يُتخذ بعد مراجعة الأرقام.
-- القاعدة: دفعة الواجب تخص فرعًا لشريك، ومن أرسله مستخدم مرتبط بنفس الشريك (UserPartners) أو بلا مرسل (مسار المسودات القديم).
SET NOCOUNT ON;

SELECT
    p.Id                                   AS PartnerId,
    p.Name                                 AS PartnerName,
    COUNT(*)                               AS HomeworkSets,
    SUM(CASE WHEN hs.AssignedByUserId IS NULL THEN 1 ELSE 0 END)             AS WithoutSender,
    SUM(CASE WHEN up.UserId IS NOT NULL THEN 1 ELSE 0 END)                   AS SentByPartnerUser,
    SUM(CASE WHEN hs.AssignedByUserId IS NOT NULL AND up.UserId IS NULL THEN 1 ELSE 0 END) AS SentByInstituteUser,
    MIN(hs.CreatedAt)                      AS FirstCreatedAt,
    MAX(hs.CreatedAt)                      AS LastCreatedAt
FROM HomeworkSets hs
INNER JOIN Batches b   ON b.Id = hs.BatchId
INNER JOIN Branches br ON br.Id = b.BranchId
INNER JOIN Partners p  ON p.Id = br.PartnerId
LEFT JOIN (
    SELECT DISTINCT UserId, PartnerId FROM UserPartners
) up ON up.UserId = hs.AssignedByUserId AND up.PartnerId = p.Id
WHERE hs.OwnerPartnerId IS NULL
GROUP BY p.Id, p.Name
ORDER BY HomeworkSets DESC;

-- الإجمالي
SELECT
    COUNT(*) AS PartnerBatchHomeworkSetsWithoutOwner
FROM HomeworkSets hs
INNER JOIN Batches b   ON b.Id = hs.BatchId
INNER JOIN Branches br ON br.Id = b.BranchId
WHERE br.PartnerId IS NOT NULL AND hs.OwnerPartnerId IS NULL;
