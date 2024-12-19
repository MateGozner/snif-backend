using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SNIF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MatchMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Matches",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InitiatiorPetId = table.Column<string>(type: "text", nullable: false),
                    TargetPetId = table.Column<string>(type: "text", nullable: false),
                    Purpose = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Matches_Pets_InitiatiorPetId",
                        column: x => x.InitiatiorPetId,
                        principalTable: "Pets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Matches_Pets_TargetPetId",
                        column: x => x.TargetPetId,
                        principalTable: "Pets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Matches_InitiatiorPetId",
                table: "Matches",
                column: "InitiatiorPetId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_TargetPetId",
                table: "Matches",
                column: "TargetPetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Matches");
        }
    }
}
