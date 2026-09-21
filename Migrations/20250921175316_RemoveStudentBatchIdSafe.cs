using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class RemoveStudentBatchIdSafe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ✅ حذف الـ Default Constraint لو موجود باستخدام Dynamic SQL
            migrationBuilder.Sql(@"
        DECLARE @ConstraintName NVARCHAR(200);
        DECLARE @sql NVARCHAR(MAX);

        SELECT @ConstraintName = df.name
        FROM sys.default_constraints df
        INNER JOIN sys.columns c ON df.parent_object_id = c.object_id AND df.parent_column_id = c.column_id
        WHERE c.object_id = OBJECT_ID(N'dbo.Students') AND c.name = N'BatchId';

        IF @ConstraintName IS NOT NULL
        BEGIN
            SET @sql = N'ALTER TABLE [dbo].[Students] DROP CONSTRAINT [' + @ConstraintName + ']';
            EXEC sp_executesql @sql;
        END
    ");

            // ✅ Drop العمود نفسه لو موجود
            migrationBuilder.Sql(@"
        IF EXISTS (SELECT 1 FROM sys.columns 
                   WHERE Name = N'BatchId' AND Object_ID = Object_ID(N'dbo.Students'))
        BEGIN
            ALTER TABLE [dbo].[Students] DROP COLUMN [BatchId];
        END
    ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ✅ رجع العمود لو رجعت الـ Migration
            migrationBuilder.Sql(@"
        IF NOT EXISTS (SELECT 1 FROM sys.columns 
                       WHERE Name = N'BatchId' AND Object_ID = Object_ID(N'dbo.Students'))
        BEGIN
            ALTER TABLE [dbo].[Students] ADD [BatchId] int NULL;
        END
    ");
        }


    }
}
