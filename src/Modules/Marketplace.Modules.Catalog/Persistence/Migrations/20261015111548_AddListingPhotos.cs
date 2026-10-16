using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Modules.Catalog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddListingPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "listing_photo",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    seller_id = table.Column<Guid>(type: "uuid", nullable: false),
                    listing_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    unattached_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    content_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_listing_photo", x => x.id);
                    table.ForeignKey(
                        name: "fk_listing_photo_listing_listing_id",
                        column: x => x.listing_id,
                        principalSchema: "catalog",
                        principalTable: "listing",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_listing_photo_listing_id",
                schema: "catalog",
                table: "listing_photo",
                column: "listing_id");

            migrationBuilder.CreateIndex(
                name: "ix_listing_photo_unattached_at_id",
                schema: "catalog",
                table: "listing_photo",
                columns: new[] { "unattached_at", "id" },
                filter: "listing_id IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "listing_photo",
                schema: "catalog");
        }
    }
}
