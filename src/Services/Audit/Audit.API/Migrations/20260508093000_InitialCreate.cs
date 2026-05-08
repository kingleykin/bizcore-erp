using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Audit.API.Migrations
{
    /// <inheritdoc />
    [Migration("20260508093000_InitialCreate")]
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArchiveEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TraceId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SpanId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    ServiceName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EntityId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    AuditLevel = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PerformedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PerformedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PerformedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PreviousHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiveEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TraceId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SpanId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    ServiceName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EntityId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    AuditLevel = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PerformedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PerformedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PerformedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PreviousHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IsReversed = table.Column<bool>(type: "bit", nullable: false),
                    ReversedByEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReversalReason = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditHashChainHeads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartitionKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    CurrentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditHashChainHeads", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveEntries_ArchivedAt",
                table: "ArchiveEntries",
                column: "ArchivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveEntries_PerformedAt",
                table: "ArchiveEntries",
                column: "PerformedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveEntries_ServiceName",
                table: "ArchiveEntries",
                column: "ServiceName");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_Action",
                table: "AuditEntries",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_AuditLevel",
                table: "AuditEntries",
                column: "AuditLevel");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_CorrelationId",
                table: "AuditEntries",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_EntityName",
                table: "AuditEntries",
                column: "EntityName");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_PerformedAt",
                table: "AuditEntries",
                column: "PerformedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_PerformedBy",
                table: "AuditEntries",
                column: "PerformedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_ServiceName",
                table: "AuditEntries",
                column: "ServiceName");

            migrationBuilder.CreateIndex(
                name: "IX_AuditHashChainHeads_PartitionKey_Sequence",
                table: "AuditHashChainHeads",
                columns: new[] { "PartitionKey", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchiveEntries");

            migrationBuilder.DropTable(
                name: "AuditEntries");

            migrationBuilder.DropTable(
                name: "AuditHashChainHeads");
        }
    }
}
