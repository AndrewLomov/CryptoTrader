using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoTrader.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GlobalPaperTradingControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "paper_trading_control",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    emergency_stop = table.Column<bool>(type: "boolean", nullable: false),
                    reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paper_trading_control", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "paper_trading_control");
        }
    }
}
