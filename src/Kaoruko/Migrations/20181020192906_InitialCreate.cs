using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace WagahighChoices.Kaoruko.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SearchResults",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Selections = table.Column<string>(nullable: true),
                    Choices = table.Column<string>(nullable: false),
                    Heroine = table.Column<int>(nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchResults", x => x.Id);
                    table.UniqueConstraint("AK_SearchResults_Choices", x => x.Choices);
                });

            migrationBuilder.CreateTable(
                name: "Workers",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ConnectedAt = table.Column<DateTimeOffset>(nullable: false),
                    DisconnectedAt = table.Column<DateTimeOffset>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkerJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    Choices = table.Column<string>(nullable: false),
                    WorkerId = table.Column<int>(nullable: true),
                    SearchResultId = table.Column<int>(nullable: true),
                    EnqueuedAt = table.Column<DateTimeOffset>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkerJobs", x => x.Id);
                    table.UniqueConstraint("AK_WorkerJobs_Choices", x => x.Choices);
                    table.ForeignKey(
                        name: "FK_WorkerJobs_SearchResults_SearchResultId",
                        column: x => x.SearchResultId,
                        principalTable: "SearchResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkerJobs_Workers_WorkerId",
                        column: x => x.WorkerId,
                        principalTable: "Workers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkerLogs",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WorkerId = table.Column<int>(nullable: false),
                    Message = table.Column<string>(nullable: false),
                    IsError = table.Column<bool>(nullable: false),
                    TimestampOnWorker = table.Column<DateTimeOffset>(nullable: false),
                    TimestampOnServer = table.Column<DateTimeOffset>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkerLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkerLogs_Workers_WorkerId",
                        column: x => x.WorkerId,
                        principalTable: "Workers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkerJobs_SearchResultId",
                table: "WorkerJobs",
                column: "SearchResultId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkerJobs_WorkerId",
                table: "WorkerJobs",
                column: "WorkerId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkerLogs_WorkerId",
                table: "WorkerLogs",
                column: "WorkerId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkerJobs");

            migrationBuilder.DropTable(
                name: "WorkerLogs");

            migrationBuilder.DropTable(
                name: "SearchResults");

            migrationBuilder.DropTable(
                name: "Workers");
        }
    }
}
