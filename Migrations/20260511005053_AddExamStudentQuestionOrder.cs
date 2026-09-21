using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddExamStudentQuestionOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExamStudentQuestionOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    ExamAssignmentId = table.Column<int>(type: "int", nullable: true),
                    ExamAssignmentToStudentId = table.Column<int>(type: "int", nullable: true),
                    ExamId = table.Column<int>(type: "int", nullable: true),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamStudentQuestionOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamStudentQuestionOrders_ExamAssignmentsToBatches_ExamAssignmentId",
                        column: x => x.ExamAssignmentId,
                        principalTable: "ExamAssignmentsToBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamStudentQuestionOrders_ExamAssignmentsToStudents_ExamAssignmentToStudentId",
                        column: x => x.ExamAssignmentToStudentId,
                        principalTable: "ExamAssignmentsToStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamStudentQuestionOrders_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamStudentQuestionOrders_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamStudentQuestionOrders_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "StudentID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExamStudentQuestionOrders_ExamAssignmentId",
                table: "ExamStudentQuestionOrders",
                column: "ExamAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ExamStudentQuestionOrders_ExamAssignmentToStudentId",
                table: "ExamStudentQuestionOrders",
                column: "ExamAssignmentToStudentId");

            migrationBuilder.CreateIndex(
                name: "IX_ExamStudentQuestionOrders_ExamId",
                table: "ExamStudentQuestionOrders",
                column: "ExamId");

            migrationBuilder.CreateIndex(
                name: "IX_ExamStudentQuestionOrders_QuestionId",
                table: "ExamStudentQuestionOrders",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_ExamStudentQuestionOrders_StudentId_ExamAssignmentId_OrderNumber",
                table: "ExamStudentQuestionOrders",
                columns: new[] { "StudentId", "ExamAssignmentId", "OrderNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamStudentQuestionOrders_StudentId_ExamAssignmentId_QuestionId",
                table: "ExamStudentQuestionOrders",
                columns: new[] { "StudentId", "ExamAssignmentId", "QuestionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamStudentQuestionOrders_StudentId_ExamAssignmentToStudentId_OrderNumber",
                table: "ExamStudentQuestionOrders",
                columns: new[] { "StudentId", "ExamAssignmentToStudentId", "OrderNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamStudentQuestionOrders_StudentId_ExamAssignmentToStudentId_QuestionId",
                table: "ExamStudentQuestionOrders",
                columns: new[] { "StudentId", "ExamAssignmentToStudentId", "QuestionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExamStudentQuestionOrders");
        }
    }
}
