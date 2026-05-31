using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MujOpenAiApi.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentArtifacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChatId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LessonId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LessonTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UserPrompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentRuns_Chats_ChatId",
                        column: x => x.ChatId,
                        principalTable: "Chats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToolName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ArgumentsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentActions_AgentRuns_AgentRunId",
                        column: x => x.AgentRunId,
                        principalTable: "AgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GeneratedArtifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LessonId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContentJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneratedArtifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GeneratedArtifacts_AgentRuns_AgentRunId",
                        column: x => x.AgentRunId,
                        principalTable: "AgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentActions_AgentRunId",
                table: "AgentActions",
                column: "AgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentActions_ToolName",
                table: "AgentActions",
                column: "ToolName");

            migrationBuilder.CreateIndex(
                name: "IX_AgentRuns_CreatedAtUtc",
                table: "AgentRuns",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AgentRuns_ChatId",
                table: "AgentRuns",
                column: "ChatId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentRuns_LessonId",
                table: "AgentRuns",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedArtifacts_AgentRunId",
                table: "GeneratedArtifacts",
                column: "AgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedArtifacts_CreatedAtUtc",
                table: "GeneratedArtifacts",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedArtifacts_LessonId",
                table: "GeneratedArtifacts",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedArtifacts_Type",
                table: "GeneratedArtifacts",
                column: "Type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentActions");

            migrationBuilder.DropTable(
                name: "GeneratedArtifacts");

            migrationBuilder.DropTable(
                name: "AgentRuns");
        }
    }
}
