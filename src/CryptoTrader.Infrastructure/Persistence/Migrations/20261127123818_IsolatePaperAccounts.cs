using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoTrader.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IsolatePaperAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "applied_candle_keys",
                table: "paper_accounts",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "skip_reason",
                table: "candle_consumer_checkpoints",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_paper_accounts_exchange_quote",
                table: "paper_accounts",
                columns: new[] { "exchange_id", "quote_asset" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_paper_accounts_exchange_quote",
                table: "paper_accounts");

            migrationBuilder.DropColumn(
                name: "applied_candle_keys",
                table: "paper_accounts");

            migrationBuilder.DropColumn(
                name: "skip_reason",
                table: "candle_consumer_checkpoints");
        }
    }
}
