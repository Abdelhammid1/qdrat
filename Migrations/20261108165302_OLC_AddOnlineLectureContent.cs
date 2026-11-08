using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class OLC_AddOnlineLectureContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOnlineLearning",
                table: "Batches",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "OnlineLearningEnabledAt",
                table: "Batches",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LectureContents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LectureId = table.Column<int>(type: "int", nullable: false),
                    ContentMode = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    VideoProvider = table.Column<int>(type: "int", nullable: true),
                    VideoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VideoExternalId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DurationSeconds = table.Column<int>(type: "int", nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LectureContents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LectureContents_Lecture_LectureId",
                        column: x => x.LectureId,
                        principalTable: "Lecture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LectureContentItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LectureContentId = table.Column<int>(type: "int", nullable: false),
                    LessonId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StartSeconds = table.Column<int>(type: "int", nullable: true),
                    EndSeconds = table.Column<int>(type: "int", nullable: true),
                    SourceVideoKind = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    SourceVideoId = table.Column<int>(type: "int", nullable: true),
                    VideoProvider = table.Column<int>(type: "int", nullable: true),
                    VideoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VideoExternalId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DurationSeconds = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LectureContentItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LectureContentItems_LectureContents_LectureContentId",
                        column: x => x.LectureContentId,
                        principalTable: "LectureContents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LectureContentItems_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LectureContentProgress",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LectureContentId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    WatchedSeconds = table.Column<double>(type: "float", nullable: false),
                    MaxPositionSeconds = table.Column<double>(type: "float", nullable: false),
                    DurationSeconds = table.Column<int>(type: "int", nullable: true),
                    LastPingState = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    FirstPingAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastPingAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndedSeen = table.Column<bool>(type: "bit", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LectureContentProgress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LectureContentProgress_LectureContents_LectureContentId",
                        column: x => x.LectureContentId,
                        principalTable: "LectureContents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LectureContentProgress_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "StudentID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LectureContentItemProgress",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LectureContentItemId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    WatchedSeconds = table.Column<double>(type: "float", nullable: false),
                    MaxPositionSeconds = table.Column<double>(type: "float", nullable: false),
                    DurationSeconds = table.Column<int>(type: "int", nullable: true),
                    LastPingState = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    FirstPingAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastPingAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndedSeen = table.Column<bool>(type: "bit", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LectureContentItemProgress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LectureContentItemProgress_LectureContentItems_LectureContentItemId",
                        column: x => x.LectureContentItemId,
                        principalTable: "LectureContentItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LectureContentItemProgress_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "StudentID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LectureContentItemProgress_LectureContentItemId_StudentId",
                table: "LectureContentItemProgress",
                columns: new[] { "LectureContentItemId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LectureContentItemProgress_StudentId",
                table: "LectureContentItemProgress",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_LectureContentItems_LectureContentId_LessonId",
                table: "LectureContentItems",
                columns: new[] { "LectureContentId", "LessonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LectureContentItems_LectureContentId_SortOrder",
                table: "LectureContentItems",
                columns: new[] { "LectureContentId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_LectureContentItems_LessonId",
                table: "LectureContentItems",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_LectureContentProgress_LectureContentId_StudentId",
                table: "LectureContentProgress",
                columns: new[] { "LectureContentId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LectureContentProgress_StudentId",
                table: "LectureContentProgress",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_LectureContents_LectureId",
                table: "LectureContents",
                column: "LectureId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LectureContentItemProgress");

            migrationBuilder.DropTable(
                name: "LectureContentProgress");

            migrationBuilder.DropTable(
                name: "LectureContentItems");

            migrationBuilder.DropTable(
                name: "LectureContents");

            migrationBuilder.DropColumn(
                name: "IsOnlineLearning",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "OnlineLearningEnabledAt",
                table: "Batches");
        }
    }
}
