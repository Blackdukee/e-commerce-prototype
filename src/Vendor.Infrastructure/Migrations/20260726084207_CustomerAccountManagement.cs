using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendor.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CustomerAccountManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Customers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Customer");

            migrationBuilder.AddColumn<DateTime>(
                name: "RoleChangedAtUtc",
                table: "Customers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RoleChangedByCustomerId",
                table: "Customers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Customers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<DateTime>(
                name: "SuspendedAtUtc",
                table: "Customers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuspensionReason",
                table: "Customers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PerformedByCustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAuditLogs_CustomerId",
                table: "CustomerAuditLogs",
                column: "CustomerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerAuditLogs");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "RoleChangedAtUtc",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "RoleChangedByCustomerId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "SuspendedAtUtc",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "SuspensionReason",
                table: "Customers");
        }
    }
}
