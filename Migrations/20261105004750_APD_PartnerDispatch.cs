using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class APD_PartnerDispatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OwnerPartnerId",
                table: "PerformanceIndicatorExams",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CanUseMinistrySimExams",
                table: "PartnerSubscriptions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "OwnerPartnerId",
                table: "MinistrySimExamAssignmentsToStudents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OwnerPartnerId",
                table: "MinistrySimExamAssignmentsToBatches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OwnerPartnerId",
                table: "HomeworkSets",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PartnerDispatchLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DispatchGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SourceProfessionalModelId = table.Column<int>(type: "int", nullable: true),
                    SourceMinistrySimExamId = table.Column<int>(type: "int", nullable: true),
                    SourcePerformanceIndicatorExamId = table.Column<int>(type: "int", nullable: true),
                    PartnerId = table.Column<int>(type: "int", nullable: false),
                    BatchId = table.Column<int>(type: "int", nullable: false),
                    TargetEntityId = table.Column<int>(type: "int", nullable: false),
                    StudentsCount = table.Column<int>(type: "int", nullable: false),
                    ContractOverridden = table.Column<bool>(type: "bit", nullable: false),
                    OverrideReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SentByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerDispatchLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartnerDispatchLogs_Partners_PartnerId",
                        column: x => x.PartnerId,
                        principalTable: "Partners",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceIndicatorExams_OwnerPartnerId",
                table: "PerformanceIndicatorExams",
                column: "OwnerPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_MinistrySimExamAssignmentsToStudents_OwnerPartnerId",
                table: "MinistrySimExamAssignmentsToStudents",
                column: "OwnerPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_MinistrySimExamAssignmentsToBatches_OwnerPartnerId_BatchId",
                table: "MinistrySimExamAssignmentsToBatches",
                columns: new[] { "OwnerPartnerId", "BatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkSets_OwnerPartnerId_BatchId",
                table: "HomeworkSets",
                columns: new[] { "OwnerPartnerId", "BatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerDispatchLogs_DispatchGroupId",
                table: "PartnerDispatchLogs",
                column: "DispatchGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerDispatchLogs_PartnerId_Kind",
                table: "PartnerDispatchLogs",
                columns: new[] { "PartnerId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerDispatchLogs_SentAtUtc",
                table: "PartnerDispatchLogs",
                column: "SentAtUtc");

            migrationBuilder.AddForeignKey(
                name: "FK_HomeworkSets_Partners_OwnerPartnerId",
                table: "HomeworkSets",
                column: "OwnerPartnerId",
                principalTable: "Partners",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MinistrySimExamAssignmentsToBatches_Partners_OwnerPartnerId",
                table: "MinistrySimExamAssignmentsToBatches",
                column: "OwnerPartnerId",
                principalTable: "Partners",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MinistrySimExamAssignmentsToStudents_Partners_OwnerPartnerId",
                table: "MinistrySimExamAssignmentsToStudents",
                column: "OwnerPartnerId",
                principalTable: "Partners",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PerformanceIndicatorExams_Partners_OwnerPartnerId",
                table: "PerformanceIndicatorExams",
                column: "OwnerPartnerId",
                principalTable: "Partners",
                principalColumn: "Id");

            // APD-S1.3: منح مبدئي لـ PartnerDispatch.Send لكل بروفايل يملك AdminLessonCompletions.ManageModelHomework
            // (صراحة، أو ضمنيًا بمستوى قراءة وكتابة بلا صلاحيات مخصصة). التجاوز وقراءة السجل لا يُمنحان تلقائيًا.
            migrationBuilder.Sql(GrantSendSql);
        }

        public const string GrantSendSql = @"
INSERT INTO AdminProfileControllerPermissions (AdminProfileId, ControllerName, AccessLevel, HasCustomPermissions)
SELECT DISTINCT cp.AdminProfileId, N'PartnerDispatch', 1, 1
FROM AdminProfileControllerPermissions cp
WHERE cp.ControllerName = N'AdminLessonCompletions'
  AND ((cp.HasCustomPermissions = 0 AND cp.AccessLevel >= 2)
       OR (cp.HasCustomPermissions = 1 AND EXISTS (
            SELECT 1 FROM AdminProfileCustomPermissions c
            WHERE c.AdminProfileControllerPermissionId = cp.Id
              AND c.ActionName = N'ManageModelHomework'
              AND c.IsAllowed = 1)))
  AND NOT EXISTS (
        SELECT 1 FROM AdminProfileControllerPermissions x
        WHERE x.AdminProfileId = cp.AdminProfileId AND x.ControllerName = N'PartnerDispatch');

INSERT INTO AdminProfileCustomPermissions (AdminProfileControllerPermissionId, ActionName, IsAllowed)
SELECT p.Id, N'Send', 1
FROM AdminProfileControllerPermissions p
WHERE p.ControllerName = N'PartnerDispatch'
  AND NOT EXISTS (
        SELECT 1 FROM AdminProfileCustomPermissions c
        WHERE c.AdminProfileControllerPermissionId = p.Id);
";

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE c FROM AdminProfileCustomPermissions c
INNER JOIN AdminProfileControllerPermissions p ON p.Id = c.AdminProfileControllerPermissionId
WHERE p.ControllerName = N'PartnerDispatch';

DELETE FROM AdminProfileControllerPermissions WHERE ControllerName = N'PartnerDispatch';
");

            migrationBuilder.DropForeignKey(
                name: "FK_HomeworkSets_Partners_OwnerPartnerId",
                table: "HomeworkSets");

            migrationBuilder.DropForeignKey(
                name: "FK_MinistrySimExamAssignmentsToBatches_Partners_OwnerPartnerId",
                table: "MinistrySimExamAssignmentsToBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_MinistrySimExamAssignmentsToStudents_Partners_OwnerPartnerId",
                table: "MinistrySimExamAssignmentsToStudents");

            migrationBuilder.DropForeignKey(
                name: "FK_PerformanceIndicatorExams_Partners_OwnerPartnerId",
                table: "PerformanceIndicatorExams");

            migrationBuilder.DropTable(
                name: "PartnerDispatchLogs");

            migrationBuilder.DropIndex(
                name: "IX_PerformanceIndicatorExams_OwnerPartnerId",
                table: "PerformanceIndicatorExams");

            migrationBuilder.DropIndex(
                name: "IX_MinistrySimExamAssignmentsToStudents_OwnerPartnerId",
                table: "MinistrySimExamAssignmentsToStudents");

            migrationBuilder.DropIndex(
                name: "IX_MinistrySimExamAssignmentsToBatches_OwnerPartnerId_BatchId",
                table: "MinistrySimExamAssignmentsToBatches");

            migrationBuilder.DropIndex(
                name: "IX_HomeworkSets_OwnerPartnerId_BatchId",
                table: "HomeworkSets");

            migrationBuilder.DropColumn(
                name: "OwnerPartnerId",
                table: "PerformanceIndicatorExams");

            migrationBuilder.DropColumn(
                name: "CanUseMinistrySimExams",
                table: "PartnerSubscriptions");

            migrationBuilder.DropColumn(
                name: "OwnerPartnerId",
                table: "MinistrySimExamAssignmentsToStudents");

            migrationBuilder.DropColumn(
                name: "OwnerPartnerId",
                table: "MinistrySimExamAssignmentsToBatches");

            migrationBuilder.DropColumn(
                name: "OwnerPartnerId",
                table: "HomeworkSets");
        }
    }
}
