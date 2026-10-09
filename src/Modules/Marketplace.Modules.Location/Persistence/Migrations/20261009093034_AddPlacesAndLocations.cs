using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Marketplace.Modules.Location.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlacesAndLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inbox_message",
                schema: "location",
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
                name: "item_location",
                schema: "location",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    geo = table.Column<Point>(type: "geography (point, 4326)", nullable: false),
                    public_geo = table.Column<Point>(type: "geography (point, 4326)", nullable: false),
                    area_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_item_location", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "place",
                schema: "location",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_place", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_location",
                schema: "location",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    public_geo = table.Column<Point>(type: "geography (point, 4326)", nullable: false),
                    area_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    radius_km = table.Column<int>(type: "integer", nullable: true),
                    source = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_location", x => x.user_id);
                });

            migrationBuilder.InsertData(
                schema: "location",
                table: "place",
                columns: new[] { "id", "city", "kind", "latitude", "longitude", "name" },
                values: new object[,]
                {
                    { 1, "Chicago", "city", 41.878100000000003, -87.629800000000003, "Chicago, IL" },
                    { 2, "New York", "city", 40.678199999999997, -73.944199999999995, "Brooklyn, NY" },
                    { 3, "Dallas", "city", 32.776699999999998, -96.796999999999997, "Dallas, TX" },
                    { 4, "Seattle", "city", 47.606200000000001, -122.3321, "Seattle, WA" },
                    { 5, "Portland", "city", 45.5152, -122.6784, "Portland, OR" },
                    { 6, "San Diego", "city", 32.715699999999998, -117.1611, "San Diego, CA" },
                    { 101, "Chicago", "area", 41.908799999999999, -87.677599999999998, "Wicker Park, Chicago" },
                    { 102, "Chicago", "area", 41.884999999999998, -87.784499999999994, "Oak Park" },
                    { 103, "Chicago", "area", 42.045099999999998, -87.687700000000007, "Evanston" },
                    { 104, "Chicago", "area", 41.750799999999998, -88.153499999999994, "Naperville" },
                    { 105, "Chicago", "area", 41.8827, -87.647999999999996, "West Loop, Chicago" },
                    { 106, "Chicago", "area", 41.7943, -87.590699999999998, "Hyde Park, Chicago" },
                    { 107, "Chicago", "area", 41.921399999999998, -87.651300000000006, "Lincoln Park, Chicago" },
                    { 108, "Chicago", "area", 41.878599999999999, -87.625100000000003, "The Loop, Chicago" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_item_location_owner_id",
                schema: "location",
                table: "item_location",
                column: "owner_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbox_message",
                schema: "location");

            migrationBuilder.DropTable(
                name: "item_location",
                schema: "location");

            migrationBuilder.DropTable(
                name: "place",
                schema: "location");

            migrationBuilder.DropTable(
                name: "user_location",
                schema: "location");
        }
    }
}
