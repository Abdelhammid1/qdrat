using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class Add_MinistrySimExamGuestStudent_Entities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MinistrySimExamGuestStudents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MinistrySimExamGuestStudents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MinistrySimExamAssignmentsToGuests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MinistrySimExamId = table.Column<int>(type: "int", nullable: false),
                    GuestStudentId = table.Column<int>(type: "int", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MinistrySimExamAssignmentsToGuests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MinistrySimExamAssignmentsToGuests_MinistrySimExamGuestStudents_GuestStudentId",
                        column: x => x.GuestStudentId,
                        principalTable: "MinistrySimExamGuestStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MinistrySimExamAssignmentsToGuests_MinistrySimExams_MinistrySimExamId",
                        column: x => x.MinistrySimExamId,
                        principalTable: "MinistrySimExams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MinistrySimExamAssignmentsToGuests_GuestStudentId",
                table: "MinistrySimExamAssignmentsToGuests",
                column: "GuestStudentId");

            migrationBuilder.CreateIndex(
                name: "IX_MinistrySimExamAssignmentsToGuests_MinistrySimExamId_GuestStudentId",
                table: "MinistrySimExamAssignmentsToGuests",
                columns: new[] { "MinistrySimExamId", "GuestStudentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MinistrySimExamAssignmentsToGuests");

            migrationBuilder.DropTable(
                name: "MinistrySimExamGuestStudents");
        }
    }
}
