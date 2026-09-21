using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceLateAndEarlyLeaveFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "ActualArrivalTime",
                table: "AttendanceRecords",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "ActualDepartureTime",
                table: "AttendanceRecords",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EarlyLeavePermissionAt",
                table: "AttendanceRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasEarlyLeavePermission",
                table: "AttendanceRecords",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsLateArrival",
                table: "AttendanceRecords",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActualArrivalTime",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "ActualDepartureTime",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "EarlyLeavePermissionAt",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "HasEarlyLeavePermission",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "IsLateArrival",
                table: "AttendanceRecords");
        }
    }
}
