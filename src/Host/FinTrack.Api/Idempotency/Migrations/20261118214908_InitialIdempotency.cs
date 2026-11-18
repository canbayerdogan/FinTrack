using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinTrack.Api.Idempotency.Migrations
{
    /// <inheritdoc />
    public partial class InitialIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "shared");

            migrationBuilder.CreateTable(
                name: "IdempotencyKeys",
                schema: "shared",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScopeHash = table.Column<string>(type: "char(64)", maxLength: 64, nullable: false),
                    UserHash = table.Column<string>(type: "char(64)", maxLength: 64, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    RequestHash = table.Column<string>(type: "char(64)", maxLength: 64, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    RequestContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ResponseStatusCode = table.Column<int>(type: "int", nullable: true),
                    ResponseContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ResponseHeaders = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ResponseBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TombstoneExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyKeys", x => x.Id);
                    table.CheckConstraint("CK_IdempotencyKeys_2xx", "ResponseStatusCode IS NULL OR ResponseStatusCode BETWEEN 200 AND 299");
                    table.CheckConstraint("CK_IdempotencyKeys_Completed", "Status <> 2 OR (ResponseStatusCode BETWEEN 200 AND 299 AND ResponseBody IS NOT NULL AND ResponseContentType IS NOT NULL)");
                    table.CheckConstraint("CK_IdempotencyKeys_Status", "Status IN (1, 2, 3)");
                    table.CheckConstraint("CK_IdempotencyKeys_Tombstone", "Status <> 3 OR (ResponseBody IS NULL AND ResponseHeaders IS NULL)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyKeys_ExpiresAtUtc",
                schema: "shared",
                table: "IdempotencyKeys",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyKeys_TombstoneExpiresAtUtc",
                schema: "shared",
                table: "IdempotencyKeys",
                column: "TombstoneExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyKeys_UserHash_Key",
                schema: "shared",
                table: "IdempotencyKeys",
                columns: new[] { "UserHash", "Key" });

            migrationBuilder.CreateIndex(
                name: "UX_IdempotencyKeys_ScopeHash_Key",
                schema: "shared",
                table: "IdempotencyKeys",
                columns: new[] { "ScopeHash", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdempotencyKeys",
                schema: "shared");
        }
    }
}
