using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CryptoTrader.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "api_credentials_metadata",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trading_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    exchange_id = table.Column<string>(type: "text", nullable: true),
                    key_hint = table.Column<string>(type: "text", nullable: false),
                    permissions = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_credentials_metadata", x => x.id);
                },
                comment: "Key hint and permissions only. Do not add a secret, token, or api key column.");

            migrationBuilder.CreateTable(
                name: "backtest_runs",
                columns: table => new
                {
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    strategy_id = table.Column<string>(type: "text", nullable: false),
                    strategy_version = table.Column<string>(type: "text", nullable: false),
                    engine_version = table.Column<string>(type: "text", nullable: false),
                    parameters_json = table.Column<string>(type: "text", nullable: false),
                    from_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    to_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    assumptions_engine_version = table.Column<string>(type: "text", nullable: false),
                    funding_modeled = table.Column<bool>(type: "boolean", nullable: false),
                    partial_fills_modeled = table.Column<bool>(type: "boolean", nullable: false),
                    fill_price_model = table.Column<string>(type: "text", nullable: false),
                    stop_and_take_profit_conflict = table.Column<string>(type: "text", nullable: false),
                    assumptions_notes = table.Column<string>(type: "text", nullable: false),
                    equity_curve = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backtest_runs", x => x.run_id);
                });

            migrationBuilder.CreateTable(
                name: "balances_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trading_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset = table.Column<string>(type: "text", nullable: false),
                    total = table.Column<decimal>(type: "numeric", nullable: false),
                    available = table.Column<decimal>(type: "numeric", nullable: false),
                    captured_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_balances_snapshots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "candles",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    exchange_id = table.Column<string>(type: "text", nullable: false),
                    instrument_id = table.Column<string>(type: "text", nullable: false),
                    timeframe = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    open_time_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    close_time_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    open = table.Column<decimal>(type: "numeric", nullable: false),
                    high = table.Column<decimal>(type: "numeric", nullable: false),
                    low = table.Column<decimal>(type: "numeric", nullable: false),
                    close = table.Column<decimal>(type: "numeric", nullable: false),
                    volume = table.Column<decimal>(type: "numeric", nullable: false),
                    is_closed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "exchanges",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exchanges", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "executions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instrument_id = table.Column<string>(type: "text", nullable: false),
                    side = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    price = table.Column<decimal>(type: "numeric", nullable: false),
                    fee = table.Column<decimal>(type: "numeric", nullable: false),
                    fee_asset = table.Column<string>(type: "text", nullable: false),
                    time_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    exchange_trade_id = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_executions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "funding_payments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    trading_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exchange_id = table.Column<string>(type: "text", nullable: false),
                    instrument_id = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    funding_time_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_funding_payments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "funding_rates",
                columns: table => new
                {
                    exchange_id = table.Column<string>(type: "text", nullable: false),
                    instrument_id = table.Column<string>(type: "text", nullable: false),
                    funding_time_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    rate = table.Column<decimal>(type: "numeric", nullable: false),
                    received_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_funding_rates", x => new { x.exchange_id, x.instrument_id, x.funding_time_utc });
                });

            migrationBuilder.CreateTable(
                name: "instruments",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    exchange_id = table.Column<string>(type: "text", nullable: false),
                    exchange_symbol = table.Column<string>(type: "text", nullable: false),
                    base_asset = table.Column<string>(type: "text", nullable: false),
                    quote_asset = table.Column<string>(type: "text", nullable: false),
                    instrument_type = table.Column<int>(type: "integer", nullable: false),
                    price_tick = table.Column<decimal>(type: "numeric", nullable: false),
                    quantity_step = table.Column<decimal>(type: "numeric", nullable: false),
                    min_quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    min_notional = table.Column<decimal>(type: "numeric", nullable: true),
                    contract_multiplier = table.Column<decimal>(type: "numeric", nullable: true),
                    is_trading_enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_instruments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "market_trades",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    exchange_id = table.Column<string>(type: "text", nullable: false),
                    instrument_id = table.Column<string>(type: "text", nullable: false),
                    exchange_trade_id = table.Column<string>(type: "text", nullable: false),
                    price = table.Column<decimal>(type: "numeric", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    side = table.Column<int>(type: "integer", nullable: true),
                    time_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_market_trades", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "order_book_snapshots",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    exchange_id = table.Column<string>(type: "text", nullable: false),
                    instrument_id = table.Column<string>(type: "text", nullable: false),
                    sequence = table.Column<long>(type: "bigint", nullable: true),
                    is_consistent = table.Column<bool>(type: "boolean", nullable: false),
                    exchange_time_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    received_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    bids = table.Column<string>(type: "jsonb", nullable: false),
                    asks = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_book_snapshots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_order_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    trading_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exchange_id = table.Column<string>(type: "text", nullable: false),
                    instrument_id = table.Column<string>(type: "text", nullable: false),
                    instrument_type = table.Column<int>(type: "integer", nullable: false),
                    side = table.Column<int>(type: "integer", nullable: false),
                    order_type = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    limit_price = table.Column<decimal>(type: "numeric", nullable: true),
                    reduce_only = table.Column<bool>(type: "boolean", nullable: false),
                    trading_mode = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    filled_quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    filled_notional = table.Column<decimal>(type: "numeric", nullable: false),
                    applied_exchange_trade_ids = table.Column<string>(type: "jsonb", nullable: false),
                    exchange_order_id = table.Column<string>(type: "text", nullable: true),
                    reject_reason = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "paper_accounts",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exchange_id = table.Column<string>(type: "text", nullable: false),
                    quote_asset = table.Column<string>(type: "text", nullable: false),
                    cash = table.Column<decimal>(type: "numeric", nullable: false),
                    peak_equity = table.Column<decimal>(type: "numeric", nullable: false),
                    day_start_equity = table.Column<decimal>(type: "numeric", nullable: false),
                    last_equity = table.Column<decimal>(type: "numeric", nullable: false),
                    day_utc = table.Column<DateOnly>(type: "date", nullable: false),
                    emergency_stop = table.Column<bool>(type: "boolean", nullable: false),
                    positions = table.Column<string>(type: "jsonb", nullable: false),
                    marks = table.Column<string>(type: "jsonb", nullable: false),
                    applied_exchange_trade_ids = table.Column<string>(type: "jsonb", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paper_accounts", x => x.account_id);
                });

            migrationBuilder.CreateTable(
                name: "positions",
                columns: table => new
                {
                    trading_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instrument_id = table.Column<string>(type: "text", nullable: false),
                    exchange_id = table.Column<string>(type: "text", nullable: false),
                    instrument_type = table.Column<int>(type: "integer", nullable: false),
                    side = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    average_price = table.Column<decimal>(type: "numeric", nullable: false),
                    realized_pnl = table.Column<decimal>(type: "numeric", nullable: false),
                    fees = table.Column<decimal>(type: "numeric", nullable: false),
                    funding = table.Column<decimal>(type: "numeric", nullable: false),
                    fee_asset = table.Column<string>(type: "text", nullable: true),
                    leverage = table.Column<decimal>(type: "numeric", nullable: false),
                    margin = table.Column<decimal>(type: "numeric", nullable: true),
                    contract_multiplier = table.Column<decimal>(type: "numeric", nullable: true),
                    opened_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_positions", x => new { x.trading_account_id, x.instrument_id });
                });

            migrationBuilder.CreateTable(
                name: "reconciliation_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trading_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    summary = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reconciliation_runs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "risk_decisions",
                columns: table => new
                {
                    signal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    strategy_instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    approved = table.Column<bool>(type: "boolean", nullable: false),
                    approved_quantity = table.Column<decimal>(type: "numeric", nullable: true),
                    estimated_max_loss = table.Column<decimal>(type: "numeric", nullable: true),
                    applied_constraints = table.Column<string>(type: "jsonb", nullable: false),
                    rejection_code = table.Column<string>(type: "text", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_decisions", x => x.signal_id);
                });

            migrationBuilder.CreateTable(
                name: "risk_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    causation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "strategies",
                columns: table => new
                {
                    strategy_id = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_strategies", x => new { x.strategy_id, x.version });
                });

            migrationBuilder.CreateTable(
                name: "strategy_configurations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    strategy_id = table.Column<string>(type: "text", nullable: false),
                    strategy_version = table.Column<string>(type: "text", nullable: false),
                    parameters_json = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_strategy_configurations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "strategy_instances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    strategy_id = table.Column<string>(type: "text", nullable: false),
                    strategy_version = table.Column<string>(type: "text", nullable: false),
                    instrument_id = table.Column<string>(type: "text", nullable: false),
                    parameters_json = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_strategy_instances", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "trade_signals",
                columns: table => new
                {
                    signal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    strategy_instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    strategy_id = table.Column<string>(type: "text", nullable: false),
                    strategy_version = table.Column<string>(type: "text", nullable: false),
                    instrument_id = table.Column<string>(type: "text", nullable: false),
                    action = table.Column<int>(type: "integer", nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reference_price = table.Column<decimal>(type: "numeric", nullable: false),
                    stop_loss_price = table.Column<decimal>(type: "numeric", nullable: true),
                    take_profit_price = table.Column<decimal>(type: "numeric", nullable: true),
                    atr = table.Column<decimal>(type: "numeric", nullable: true),
                    explanation = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trade_signals", x => x.signal_id);
                });

            migrationBuilder.CreateTable(
                name: "trading_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    exchange_id = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    mode = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trading_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "backtest_metrics",
                columns: table => new
                {
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    initial_equity = table.Column<decimal>(type: "numeric", nullable: false),
                    final_equity = table.Column<decimal>(type: "numeric", nullable: false),
                    net_profit = table.Column<decimal>(type: "numeric", nullable: false),
                    roi_fraction = table.Column<decimal>(type: "numeric", nullable: false),
                    max_drawdown_fraction = table.Column<decimal>(type: "numeric", nullable: false),
                    sharpe = table.Column<decimal>(type: "numeric", nullable: true),
                    sortino = table.Column<decimal>(type: "numeric", nullable: true),
                    profit_factor = table.Column<decimal>(type: "numeric", nullable: true),
                    win_rate_fraction = table.Column<decimal>(type: "numeric", nullable: false),
                    expectancy = table.Column<decimal>(type: "numeric", nullable: false),
                    trade_count = table.Column<int>(type: "integer", nullable: false),
                    total_fees = table.Column<decimal>(type: "numeric", nullable: false),
                    total_slippage = table.Column<decimal>(type: "numeric", nullable: false),
                    average_holding_ticks = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backtest_metrics", x => x.run_id);
                    table.ForeignKey(
                        name: "FK_backtest_metrics_backtest_runs_run_id",
                        column: x => x.run_id,
                        principalTable: "backtest_runs",
                        principalColumn: "run_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "backtest_trades",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trade_number = table.Column<int>(type: "integer", nullable: false),
                    instrument_id = table.Column<string>(type: "text", nullable: false),
                    entry_time_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    exit_time_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    entry_price = table.Column<decimal>(type: "numeric", nullable: false),
                    exit_price = table.Column<decimal>(type: "numeric", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    fees = table.Column<decimal>(type: "numeric", nullable: false),
                    slippage_cost = table.Column<decimal>(type: "numeric", nullable: false),
                    net_pnl = table.Column<decimal>(type: "numeric", nullable: false),
                    exit_reason = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backtest_trades", x => x.id);
                    table.ForeignKey(
                        name: "FK_backtest_trades_backtest_runs_run_id",
                        column: x => x.run_id,
                        principalTable: "backtest_runs",
                        principalColumn: "run_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_api_credentials_metadata_account",
                table: "api_credentials_metadata",
                column: "trading_account_id");

            migrationBuilder.CreateIndex(
                name: "ux_backtest_trades_run_number",
                table: "backtest_trades",
                columns: new[] { "run_id", "trade_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_balances_snapshots_account_time",
                table: "balances_snapshots",
                columns: new[] { "trading_account_id", "captured_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_candles_identity",
                table: "candles",
                columns: new[] { "exchange_id", "instrument_id", "timeframe", "open_time_utc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_executions_order_trade",
                table: "executions",
                columns: new[] { "order_id", "exchange_trade_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_funding_payments_account_time",
                table: "funding_payments",
                columns: new[] { "trading_account_id", "funding_time_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_instruments_exchange_symbol",
                table: "instruments",
                columns: new[] { "exchange_id", "exchange_symbol" });

            migrationBuilder.CreateIndex(
                name: "ix_market_trades_instrument_time",
                table: "market_trades",
                columns: new[] { "instrument_id", "time_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_market_trades_identity",
                table: "market_trades",
                columns: new[] { "exchange_id", "instrument_id", "exchange_trade_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_order_book_snapshots_instrument_time",
                table: "order_book_snapshots",
                columns: new[] { "exchange_id", "instrument_id", "exchange_time_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_orders_account_mode",
                table: "orders",
                columns: new[] { "trading_account_id", "trading_mode" });

            migrationBuilder.CreateIndex(
                name: "ux_orders_account_client_order_id",
                table: "orders",
                columns: new[] { "trading_account_id", "client_order_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reconciliation_runs_account_started",
                table: "reconciliation_runs",
                columns: new[] { "trading_account_id", "started_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_risk_events_occurred_at",
                table: "risk_events",
                column: "occurred_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_strategy_configurations_strategy",
                table: "strategy_configurations",
                columns: new[] { "strategy_id", "strategy_version" });

            migrationBuilder.CreateIndex(
                name: "ix_strategy_instances_strategy",
                table: "strategy_instances",
                columns: new[] { "strategy_id", "strategy_version" });

            migrationBuilder.CreateIndex(
                name: "ix_trade_signals_strategy_instrument_time",
                table: "trade_signals",
                columns: new[] { "strategy_id", "instrument_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_trading_accounts_exchange_id",
                table: "trading_accounts",
                column: "exchange_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "api_credentials_metadata");

            migrationBuilder.DropTable(
                name: "backtest_metrics");

            migrationBuilder.DropTable(
                name: "backtest_trades");

            migrationBuilder.DropTable(
                name: "balances_snapshots");

            migrationBuilder.DropTable(
                name: "candles");

            migrationBuilder.DropTable(
                name: "exchanges");

            migrationBuilder.DropTable(
                name: "executions");

            migrationBuilder.DropTable(
                name: "funding_payments");

            migrationBuilder.DropTable(
                name: "funding_rates");

            migrationBuilder.DropTable(
                name: "instruments");

            migrationBuilder.DropTable(
                name: "market_trades");

            migrationBuilder.DropTable(
                name: "order_book_snapshots");

            migrationBuilder.DropTable(
                name: "orders");

            migrationBuilder.DropTable(
                name: "paper_accounts");

            migrationBuilder.DropTable(
                name: "positions");

            migrationBuilder.DropTable(
                name: "reconciliation_runs");

            migrationBuilder.DropTable(
                name: "risk_decisions");

            migrationBuilder.DropTable(
                name: "risk_events");

            migrationBuilder.DropTable(
                name: "strategies");

            migrationBuilder.DropTable(
                name: "strategy_configurations");

            migrationBuilder.DropTable(
                name: "strategy_instances");

            migrationBuilder.DropTable(
                name: "trade_signals");

            migrationBuilder.DropTable(
                name: "trading_accounts");

            migrationBuilder.DropTable(
                name: "backtest_runs");
        }
    }
}
