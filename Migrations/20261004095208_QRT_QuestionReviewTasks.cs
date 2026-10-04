using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class QRT_QuestionReviewTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuestionReviewTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AdminNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    InstructorId = table.Column<int>(type: "int", nullable: false),
                    CurriculumId = table.Column<int>(type: "int", nullable: true),
                    ParentTaskId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LastReminderAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalItems = table.Column<int>(type: "int", nullable: false),
                    PendingItems = table.Column<int>(type: "int", nullable: false),
                    ApprovedItems = table.Column<int>(type: "int", nullable: false),
                    ReturnedItems = table.Column<int>(type: "int", nullable: false),
                    RemovedItems = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionReviewTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionReviewTasks_Curriculums_CurriculumId",
                        column: x => x.CurriculumId,
                        principalTable: "Curriculums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuestionReviewTasks_Instructors_InstructorId",
                        column: x => x.InstructorId,
                        principalTable: "Instructors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuestionReviewTasks_QuestionReviewTasks_ParentTaskId",
                        column: x => x.ParentTaskId,
                        principalTable: "QuestionReviewTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuestionReviewTaskItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaskId = table.Column<int>(type: "int", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsLockActive = table.Column<bool>(type: "bit", nullable: false),
                    ActionAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActionByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ActionByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReturnNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AdminResolutionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReferenceNumberSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionReviewTaskItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionReviewTaskItems_QuestionReviewTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "QuestionReviewTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuestionReviewTaskItems_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuestionReviewTaskItems_TaskId_QuestionId",
                table: "QuestionReviewTaskItems",
                columns: new[] { "TaskId", "QuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuestionReviewTaskItems_TaskId_Status",
                table: "QuestionReviewTaskItems",
                columns: new[] { "TaskId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_QuestionReviewTaskItems_ActiveLock",
                table: "QuestionReviewTaskItems",
                column: "QuestionId",
                unique: true,
                filter: "[IsLockActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionReviewTasks_Code",
                table: "QuestionReviewTasks",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuestionReviewTasks_CurriculumId",
                table: "QuestionReviewTasks",
                column: "CurriculumId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionReviewTasks_InstructorId_Status",
                table: "QuestionReviewTasks",
                columns: new[] { "InstructorId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_QuestionReviewTasks_ParentTaskId",
                table: "QuestionReviewTasks",
                column: "ParentTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionReviewTasks_Status_DueAtUtc",
                table: "QuestionReviewTasks",
                columns: new[] { "Status", "DueAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuestionReviewTaskItems");

            migrationBuilder.DropTable(
                name: "QuestionReviewTasks");
        }
    }
}
