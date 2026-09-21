using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class Fix_StudentBatchEnrollment_FK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.AddForeignKey(
            //    name: "FK_StudentBatchEnrollments_Batches_BatchId",
            //    table: "StudentBatchEnrollments",
            //    column: "BatchId",
            //    principalTable: "Batches",
            //    principalColumn: "Id",
            //    onDelete: ReferentialAction.Restrict);

            //migrationBuilder.AddForeignKey(
            //    name: "FK_StudentBatchEnrollments_Students_StudentID",
            //    table: "StudentBatchEnrollments",
            //    column: "StudentID",
            //    principalTable: "Students",
            //    principalColumn: "StudentID",
            //    onDelete: ReferentialAction.Restrict);
        }


        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
