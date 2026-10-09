using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Marketplace.Modules.Catalog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoriesAndListings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "category",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    slug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    emoji = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_category", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inbox_message",
                schema: "catalog",
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
                name: "listing",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    seller_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seller_handle = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    category_id = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    condition = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    photo_urls = table.Column<List<string>>(type: "text[]", nullable: false),
                    specs = table.Column<string>(type: "jsonb", nullable: false),
                    delivery = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    shipping_cost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    public_geo = table.Column<Point>(type: "geography (point, 4326)", nullable: true),
                    area_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    start_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    reserve_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    buy_now_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_listing", x => x.id);
                    table.ForeignKey(
                        name: "fk_listing_category_category_id",
                        column: x => x.category_id,
                        principalSchema: "catalog",
                        principalTable: "category",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "catalog",
                table: "category",
                columns: new[] { "id", "emoji", "name", "slug" },
                values: new object[,]
                {
                    { 1, "📷", "Electronics", "electronics" },
                    { 2, "🃏", "Collectibles", "collectibles" },
                    { 3, "👟", "Fashion", "fashion" },
                    { 4, "🛋️", "Home & Garden", "home-garden" },
                    { 5, "🧱", "Toys & Hobbies", "toys-hobbies" },
                    { 6, "🏭", "Industrial", "industrial" },
                    { 7, "📦", "Liquidation lots", "liquidation-lots" },
                    { 8, "🌹", "Food & Agriculture", "food-agriculture" },
                    { 9, "🚲", "Sports & Outdoors", "sports-outdoors" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_category_slug",
                schema: "catalog",
                table: "category",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_listing_category_id",
                schema: "catalog",
                table: "listing",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_listing_seller_id_created_at",
                schema: "catalog",
                table: "listing",
                columns: new[] { "seller_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbox_message",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "listing",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "category",
                schema: "catalog");
        }
    }
}
