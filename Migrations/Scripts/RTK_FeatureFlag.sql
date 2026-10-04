-- RTK-S7.4 — مفتاح تعطيل الخطة العلاجية العاجلة (Idempotent، إضافة صف واحد فقط)
-- التشغيل: يدويًا على قاعدة الإنتاج بعد RTK_RemedialTracks.sql. إعادة التشغيل آمنة.
-- التعطيل:  UPDATE SystemSettings SET [Value] = N'false' WHERE [Key] = N'RemedialTrack.Enabled';
-- التفعيل:  UPDATE SystemSettings SET [Value] = N'true'  WHERE [Key] = N'RemedialTrack.Enabled';
-- يسري التغيير خلال 30 ثانية (كاش الميزة) دون إعادة تشغيل التطبيق.
IF NOT EXISTS (SELECT 1 FROM [SystemSettings] WHERE [Key] = N'RemedialTrack.Enabled')
BEGIN
    INSERT INTO [SystemSettings] ([Key], [Value], [Description], [CreatedAt])
    VALUES (N'RemedialTrack.Enabled', N'true', N'تفعيل الخطة العلاجية العاجلة للطلاب (false = صيانة)', SYSUTCDATETIME());
END;
GO
