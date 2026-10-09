using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Modules.Orders.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrdersAndDealAgreements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "agreement_number_seq",
                schema: "orders",
                startValue: 1001L);

            migrationBuilder.CreateTable(
                name: "inbox_message",
                schema: "orders",
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
                name: "order",
                schema: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    auction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    listing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    buyer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    buyer_handle = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    seller_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seller_handle = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    delivery = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    shipping_cost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    accept_by = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "deal_agreement",
                schema: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    agreed_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    handover = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    handover_date = table.Column<DateOnly>(type: "date", nullable: false),
                    payment_method = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    pickup_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    area_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    buyer_accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    seller_accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    buyer_paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    seller_received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_deal_agreement", x => x.id);
                    table.ForeignKey(
                        name: "fk_deal_agreement_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "orders",
                        principalTable: "order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "deal_event",
                schema: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_deal_event", x => x.id);
                    table.ForeignKey(
                        name: "fk_deal_event_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "orders",
                        principalTable: "order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_deal_agreement_number",
                schema: "orders",
                table: "deal_agreement",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_deal_agreement_order_id",
                schema: "orders",
                table: "deal_agreement",
                column: "order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_deal_event_order_id_at",
                schema: "orders",
                table: "deal_event",
                columns: new[] { "order_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_order_auction_id",
                schema: "orders",
                table: "order",
                column: "auction_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_order_buyer_id",
                schema: "orders",
                table: "order",
                column: "buyer_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_seller_id",
                schema: "orders",
                table: "order",
                column: "seller_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "deal_agreement",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "deal_event",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "inbox_message",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "order",
                schema: "orders");

            migrationBuilder.DropSequence(
                name: "agreement_number_seq",
                schema: "orders");
        }
    }
}
