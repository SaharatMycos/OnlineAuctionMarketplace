using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Modules.Auctions.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuctionsAndBids : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "auction",
                schema: "auctions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    listing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seller_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seller_handle = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    title = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    start_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    reserve_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    buy_now_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    original_ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    soft_close = table.Column<TimeSpan>(type: "interval", nullable: false),
                    max_extension = table.Column<TimeSpan>(type: "interval", nullable: false),
                    current_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    leader_id = table.Column<Guid>(type: "uuid", nullable: true),
                    leader_handle = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    leader_max = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    bid_count = table.Column<int>(type: "integer", nullable: false),
                    last_seq = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    winner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    final_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    close_reason = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auction", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bid_request",
                schema: "auctions",
                columns: table => new
                {
                    bidder_id = table.Column<Guid>(type: "uuid", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    auction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    response = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bid_request", x => new { x.bidder_id, x.idempotency_key });
                });

            migrationBuilder.CreateTable(
                name: "inbox_message",
                schema: "auctions",
                columns: table => new
                {
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    handler = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inbox_message", x => new { x.message_id, x.handler });
                });

            migrationBuilder.CreateTable(
                name: "bid",
                schema: "auctions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    auction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bidder_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bidder_handle = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    max_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    seq = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bid", x => x.id);
                    table.ForeignKey(
                        name: "fk_bid_auction_auction_id",
                        column: x => x.auction_id,
                        principalSchema: "auctions",
                        principalTable: "auction",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_auction_listing_id",
                schema: "auctions",
                table: "auction",
                column: "listing_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_auction_status_ends_at",
                schema: "auctions",
                table: "auction",
                columns: new[] { "status", "ends_at" });

            migrationBuilder.CreateIndex(
                name: "ix_bid_auction_id_seq",
                schema: "auctions",
                table: "bid",
                columns: new[] { "auction_id", "seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bid_bidder_id_auction_id",
                schema: "auctions",
                table: "bid",
                columns: new[] { "bidder_id", "auction_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bid",
                schema: "auctions");

            migrationBuilder.DropTable(
                name: "bid_request",
                schema: "auctions");

            migrationBuilder.DropTable(
                name: "inbox_message",
                schema: "auctions");

            migrationBuilder.DropTable(
                name: "auction",
                schema: "auctions");
        }
    }
}
