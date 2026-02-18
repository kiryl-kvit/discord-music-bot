using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordMusicBot.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "play_queue_items",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Position = table.Column<long>(type: "INTEGER", nullable: false),
                    EnqueuedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_play_queue_items", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_play_queue_items_GuildId_Position",
                table: "play_queue_items",
                columns: new[] { "GuildId", "Position" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "play_queue_items");
        }
    }
}
