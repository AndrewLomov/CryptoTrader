using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoTrader.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MultiExchangeMarketData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "contract_kind",
                table: "instruments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "contract_value",
                table: "instruments",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contract_value_currency",
                table: "instruments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "settlement_currency",
                table: "instruments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "base_volume",
                table: "candles",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "quote_volume",
                table: "candles",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "volume_unit",
                table: "candles",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "candle_consumer_checkpoints",
                columns: table => new
                {
                    exchange_id = table.Column<string>(type: "text", nullable: false),
                    instrument_id = table.Column<string>(type: "text", nullable: false),
                    timeframe = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    open_time_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumer = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<int>(type: "integer", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candle_consumer_checkpoints", x => new { x.exchange_id, x.instrument_id, x.timeframe, x.open_time_utc, x.consumer });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candle_consumer_checkpoints");

            migrationBuilder.DropColumn(
                name: "contract_kind",
                table: "instruments");

            migrationBuilder.DropColumn(
                name: "contract_value",
                table: "instruments");

            migrationBuilder.DropColumn(
                name: "contract_value_currency",
                table: "instruments");

            migrationBuilder.DropColumn(
                name: "settlement_currency",
                table: "instruments");

            migrationBuilder.DropColumn(
                name: "base_volume",
                table: "candles");

            migrationBuilder.DropColumn(
                name: "quote_volume",
                table: "candles");

            migrationBuilder.DropColumn(
                name: "volume_unit",
                table: "candles");
        }
    }
}
