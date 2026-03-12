using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SNIF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoCallTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PaymentProviderSubscriptionId",
                table: "Subscriptions",
                newName: "StripeSubscriptionId");

            migrationBuilder.RenameColumn(
                name: "PaymentProviderCustomerId",
                table: "Subscriptions",
                newName: "StripeCustomerId");

            migrationBuilder.RenameIndex(
                name: "IX_Subscriptions_PaymentProviderSubscriptionId",
                table: "Subscriptions",
                newName: "IX_Subscriptions_StripeSubscriptionId");

            migrationBuilder.CreateTable(
                name: "VideoCalls",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    MatchId = table.Column<string>(type: "text", nullable: false),
                    CallerUserId = table.Column<string>(type: "text", nullable: false),
                    ReceiverUserId = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoCalls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoCalls_AspNetUsers_CallerUserId",
                        column: x => x.CallerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VideoCalls_AspNetUsers_ReceiverUserId",
                        column: x => x.ReceiverUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VideoCalls_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VideoCalls_CallerUserId",
                table: "VideoCalls",
                column: "CallerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoCalls_MatchId",
                table: "VideoCalls",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoCalls_ReceiverUserId",
                table: "VideoCalls",
                column: "ReceiverUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VideoCalls");

            migrationBuilder.RenameColumn(
                name: "StripeSubscriptionId",
                table: "Subscriptions",
                newName: "PaymentProviderSubscriptionId");

            migrationBuilder.RenameColumn(
                name: "StripeCustomerId",
                table: "Subscriptions",
                newName: "PaymentProviderCustomerId");

            migrationBuilder.RenameIndex(
                name: "IX_Subscriptions_StripeSubscriptionId",
                table: "Subscriptions",
                newName: "IX_Subscriptions_PaymentProviderSubscriptionId");
        }
    }
}
