using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinTrack.Modules.Debts.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialDebts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "debts");

            migrationBuilder.CreateTable(
                name: "Loans",
                schema: "debts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Principal = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    MonthlyPayment = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    InstallmentCount = table.Column<int>(type: "int", nullable: false),
                    FirstDueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LinkedCashInflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Loans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PersonalDebts",
                schema: "debts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CounterpartyName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Principal = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    BorrowedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LinkedCashInflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalDebts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Installments",
                schema: "debts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Installments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Installments_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "debts",
                        principalTable: "Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PersonalDebtRepayments",
                schema: "debts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonalDebtId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    PaidOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalDebtRepayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonalDebtRepayments_PersonalDebts_PersonalDebtId",
                        column: x => x.PersonalDebtId,
                        principalSchema: "debts",
                        principalTable: "PersonalDebts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InstallmentPayments",
                schema: "debts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstallmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    PaidOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstallmentPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstallmentPayments_Installments_InstallmentId",
                        column: x => x.InstallmentId,
                        principalSchema: "debts",
                        principalTable: "Installments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InstallmentPayments_InstallmentId",
                schema: "debts",
                table: "InstallmentPayments",
                column: "InstallmentId",
                unique: true,
                filter: "[Status] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_InstallmentPayments_UserId_PaidOn",
                schema: "debts",
                table: "InstallmentPayments",
                columns: new[] { "UserId", "PaidOn" });

            migrationBuilder.CreateIndex(
                name: "IX_Installments_DueDate",
                schema: "debts",
                table: "Installments",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_Installments_LoanId_Number",
                schema: "debts",
                table: "Installments",
                columns: new[] { "LoanId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Installments_UserId_Status",
                schema: "debts",
                table: "Installments",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Loans_LinkedCashInflowId",
                schema: "debts",
                table: "Loans",
                column: "LinkedCashInflowId",
                unique: true,
                filter: "[LinkedCashInflowId] <> '00000000-0000-0000-0000-000000000000'");

            migrationBuilder.CreateIndex(
                name: "IX_Loans_UserId_FirstDueDate",
                schema: "debts",
                table: "Loans",
                columns: new[] { "UserId", "FirstDueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Loans_UserId_Status",
                schema: "debts",
                table: "Loans",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonalDebtRepayments_PersonalDebtId_Status",
                schema: "debts",
                table: "PersonalDebtRepayments",
                columns: new[] { "PersonalDebtId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonalDebtRepayments_UserId_PaidOn",
                schema: "debts",
                table: "PersonalDebtRepayments",
                columns: new[] { "UserId", "PaidOn" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonalDebts_LinkedCashInflowId",
                schema: "debts",
                table: "PersonalDebts",
                column: "LinkedCashInflowId",
                unique: true,
                filter: "[LinkedCashInflowId] <> '00000000-0000-0000-0000-000000000000'");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalDebts_UserId_BorrowedOn",
                schema: "debts",
                table: "PersonalDebts",
                columns: new[] { "UserId", "BorrowedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonalDebts_UserId_Status",
                schema: "debts",
                table: "PersonalDebts",
                columns: new[] { "UserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InstallmentPayments",
                schema: "debts");

            migrationBuilder.DropTable(
                name: "PersonalDebtRepayments",
                schema: "debts");

            migrationBuilder.DropTable(
                name: "Installments",
                schema: "debts");

            migrationBuilder.DropTable(
                name: "PersonalDebts",
                schema: "debts");

            migrationBuilder.DropTable(
                name: "Loans",
                schema: "debts");
        }
    }
}
