using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Minimal.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddLoyaltyMemberships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LoyaltyMemberships",
                schema: "pro",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Tier = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyMemberships", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyMemberships_MemberName",
                schema: "pro",
                table: "LoyaltyMemberships",
                column: "MemberName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoyaltyMemberships",
                schema: "pro");
        }
    }
}
