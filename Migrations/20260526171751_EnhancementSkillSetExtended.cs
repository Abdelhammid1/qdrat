using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class EnhancementSkillSetExtended : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "EnhancementSkillSets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "EnhancementSkillSets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndAt",
                table: "EnhancementSkillSets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "EnhancementSkillSets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSent",
                table: "EnhancementSkillSets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "QuestionsPerStudent",
                table: "EnhancementSkillSets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SentAt",
                table: "EnhancementSkillSets",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "EnhancementSkillSets");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "EnhancementSkillSets");

            migrationBuilder.DropColumn(
                name: "EndAt",
                table: "EnhancementSkillSets");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "EnhancementSkillSets");

            migrationBuilder.DropColumn(
                name: "IsSent",
                table: "EnhancementSkillSets");

            migrationBuilder.DropColumn(
                name: "QuestionsPerStudent",
                table: "EnhancementSkillSets");

            migrationBuilder.DropColumn(
                name: "SentAt",
                table: "EnhancementSkillSets");
        }
    }
}
