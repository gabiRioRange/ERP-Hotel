using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsoleApp1.Migrations
{
    /// <inheritdoc />
    public partial class HardeningAndCompliancePhase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FamilyId",
                table: "RefreshTokens",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "AuthLockouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FailedAttempts = table.Column<int>(type: "integer", nullable: false),
                    LastFailedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LockedUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthLockouts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: false),
                    ResponseJson = table.Column<string>(type: "text", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SecurityAuditExportJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ClientIdFilter = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EventTypeFilter = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    SuccessFilter = table.Column<bool>(type: "boolean", nullable: true),
                    FromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ToUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Payload = table.Column<byte[]>(type: "bytea", nullable: true),
                    Sha256 = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityAuditExportJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SecurityExportAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ClientIdFilter = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EventTypeFilter = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    SuccessFilter = table.Column<bool>(type: "boolean", nullable: true),
                    FromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ToUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RowsExported = table.Column<int>(type: "integer", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TraceId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    OccurredUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityExportAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditLogs_TraceId",
                table: "SecurityAuditLogs",
                column: "TraceId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_FamilyId_ExpiresUtc",
                table: "RefreshTokens",
                columns: new[] { "FamilyId", "ExpiresUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuthLockouts_ClientId_IpAddress",
                table: "AuthLockouts",
                columns: new[] { "ClientId", "IpAddress" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_CreatedUtc",
                table: "IdempotencyRecords",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_Scope_Key",
                table: "IdempotencyRecords",
                columns: new[] { "Scope", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditExportJobs_Status_CreatedUtc",
                table: "SecurityAuditExportJobs",
                columns: new[] { "Status", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityExportAuditLogs_OccurredUtc",
                table: "SecurityExportAuditLogs",
                column: "OccurredUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthLockouts");

            migrationBuilder.DropTable(
                name: "IdempotencyRecords");

            migrationBuilder.DropTable(
                name: "SecurityAuditExportJobs");

            migrationBuilder.DropTable(
                name: "SecurityExportAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_SecurityAuditLogs_TraceId",
                table: "SecurityAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_FamilyId_ExpiresUtc",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "FamilyId",
                table: "RefreshTokens");
        }
    }
}
