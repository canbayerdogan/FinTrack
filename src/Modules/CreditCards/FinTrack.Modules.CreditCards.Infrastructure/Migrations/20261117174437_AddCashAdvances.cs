using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinTrack.Modules.CreditCards.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCashAdvances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CashAdvances",
                schema: "cards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    TakenOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LinkedCashInflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashAdvances", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashAdvances_LinkedCashInflowId",
                schema: "cards",
                table: "CashAdvances",
                column: "LinkedCashInflowId",
                unique: true,
                filter: "[LinkedCashInflowId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CashAdvances_UserId_CardId_TakenOn",
                schema: "cards",
                table: "CashAdvances",
                columns: new[] { "UserId", "CardId", "TakenOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashAdvances",
                schema: "cards");
        }
    }
}
