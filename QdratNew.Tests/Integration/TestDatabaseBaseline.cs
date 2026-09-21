using Microsoft.Data.SqlClient;

namespace QdratNew.Tests.Integration
{
    // Sprint 9 (QG-G / G2-G3) — منطق مشترك بين اختبارات التكامل (WebApplicationFactory) واختبار
    // المتصفح (Playwright عبر عملية فرعية حقيقية): يبني قاعدة اختبار معزولة (نسخة حقيقية من
    // QdratNewDB عبر BACKUP/RESTORE) مرة واحدة فقط لكل جلسة اختبار — راجع الشرح المعماري
    // الكامل في CustomWebApplicationFactory حول سبب عدم استخدام Migrate/EnsureCreated هنا.
    internal static class TestDatabaseBaseline
    {
        public const string SourceDatabaseName = "QdratNewDB";
        public const string TestDatabaseName = "QdratNewDB_IntegrationTests";
        public const string ServerConnectionString = "Integrated Security=SSPI;Data Source=.;TrustServerCertificate=True;Initial Catalog=master;";
        public const string TestDatabaseConnectionString =
            "Integrated Security=SSPI;Persist Security Info=False;Initial Catalog=" + TestDatabaseName + ";Data Source=.;TrustServerCertificate=True;";

        private static readonly SemaphoreSlim BaselineLock = new(1, 1);
        private static bool _baselineReady;

        public static async Task EnsureAsync()
        {
            if (_baselineReady) return;

            await BaselineLock.WaitAsync();
            try
            {
                if (_baselineReady) return;

                await using var master = new SqlConnection(ServerConnectionString);
                await master.OpenAsync();

                var exists = await ExecuteScalarAsync(master, $"SELECT DB_ID('{TestDatabaseName}')");
                if (exists != null && exists != DBNull.Value)
                {
                    _baselineReady = true;
                    return;
                }

                // مسار النسخة الاحتياطية يجب أن يكون داخل مجلد بيانات SQL Server نفسه — حساب
                // خدمة SQL Server (وليس مستخدم عملية الاختبار) هو من يكتب هذا الملف فعليًا،
                // ولا يملك عادة صلاحية الكتابة على مجلد Temp الخاص بمستخدم النظام.
                var dataDir = await GetDefaultDataPathAsync(master);
                var backupPath = Path.Combine(dataDir, $"{TestDatabaseName}_baseline.bak");

                await ExecuteNonQueryAsync(master,
                    $"BACKUP DATABASE [{SourceDatabaseName}] TO DISK = @path WITH INIT, COMPRESSION",
                    ("@path", backupPath));

                var fileList = new List<(string LogicalName, string Type)>();
                await using (var cmd = new SqlCommand("RESTORE FILELISTONLY FROM DISK = @path", master))
                {
                    cmd.Parameters.AddWithValue("@path", backupPath);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        fileList.Add((reader.GetString(reader.GetOrdinal("LogicalName")), reader.GetString(reader.GetOrdinal("Type"))));
                    }
                }

                var moveClauses = fileList.Select(f =>
                {
                    var extension = f.Type == "L" ? "ldf" : "mdf";
                    var suffix = f.Type == "L" ? "_log" : "";
                    var physicalPath = Path.Combine(dataDir, $"{TestDatabaseName}{suffix}.{extension}");
                    return $"MOVE '{f.LogicalName}' TO '{physicalPath}'";
                });

                await ExecuteNonQueryAsync(master,
                    $"RESTORE DATABASE [{TestDatabaseName}] FROM DISK = @path WITH {string.Join(", ", moveClauses)}, REPLACE",
                    ("@path", backupPath));

                // حذف ملف النسخة الاحتياطية عبر SQL Server نفسه (وليس File.Delete من عملية
                // الاختبار) لأن حساب خدمة SQL Server هو مالك الملف الفعلي داخل مجلد بياناته.
                try
                {
                    await ExecuteNonQueryAsync(master, "EXEC master.dbo.xp_delete_file 0, @path", ("@path", backupPath));
                }
                catch
                {
                    // تنظيف اختياري فقط — فشله لا يمنع نجاح الاختبارات.
                }

                _baselineReady = true;
            }
            finally
            {
                BaselineLock.Release();
            }
        }

        private static async Task<string> GetDefaultDataPathAsync(SqlConnection connection)
        {
            var path = await ExecuteScalarAsync(connection, "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS NVARCHAR(4000))");
            return path?.ToString() ?? @"C:\";
        }

        private static async Task<object?> ExecuteScalarAsync(SqlConnection connection, string sql)
        {
            await using var cmd = new SqlCommand(sql, connection);
            return await cmd.ExecuteScalarAsync();
        }

        private static async Task ExecuteNonQueryAsync(SqlConnection connection, string sql, params (string Name, object Value)[] parameters)
        {
            await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = 180 };
            foreach (var (name, value) in parameters)
                cmd.Parameters.AddWithValue(name, value);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
