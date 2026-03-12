using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SNIF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Sprint5_AllEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Reports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WarningCount",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "WarningCount",
                table: "AspNetUsers");
        }
    }
}
