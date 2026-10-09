using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Marketplace.Modules.Search.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddListingCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inbox_message",
                schema: "search",
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
                name: "listing_card",
                schema: "search",
                columns: table => new
                {
                    listing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    auction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    seller_handle = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    title = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    category_id = table.Column<int>(type: "integer", nullable: false),
                    category_name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    emoji = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    thumbnail_url = table.Column<string>(type: "text", nullable: true),
                    condition = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    delivery = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    shipping_cost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    public_geo = table.Column<Point>(type: "geography (point, 4326)", nullable: false),
                    area_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    bid_count = table.Column<int>(type: "integer", nullable: false),
                    buy_now_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    last_seq = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_listing_card", x => x.listing_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_listing_card_auction_id",
                schema: "search",
                table: "listing_card",
                column: "auction_id");

            migrationBuilder.CreateIndex(
                name: "ix_listing_card_public_geo",
                schema: "search",
                table: "listing_card",
                column: "public_geo")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_listing_card_status_ends_at",
                schema: "search",
                table: "listing_card",
                columns: new[] { "status", "ends_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbox_message",
                schema: "search");

            migrationBuilder.DropTable(
                name: "listing_card",
                schema: "search");
        }
    }
}
