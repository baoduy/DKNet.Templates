using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Minimal.Infra.Migrations
{
    /// <inheritdoc />
    public partial class InitDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "pro");

            migrationBuilder.EnsureSchema(
                name: "seq");

            migrationBuilder.CreateSequence<int>(
                name: "Seq_Membership",
                schema: "seq",
                maxValue: 99999L,
                cyclic: true);

            migrationBuilder.CreateSequence<int>(
                name: "Seq_None",
                schema: "seq",
                cyclic: true);

            migrationBuilder.CreateTable(
                name: "CustomerProfiles",
                schema: "pro",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BirthDay = table.Column<DateTime>(type: "Date", nullable: true),
                    Email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    MembershipNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Avatar = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerProfiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerProfiles_Email",
                schema: "pro",
                table: "CustomerProfiles",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerProfiles_MembershipNo",
                schema: "pro",
                table: "CustomerProfiles",
                column: "MembershipNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerProfiles",
                schema: "pro");

            migrationBuilder.DropSequence(
                name: "Seq_Membership",
                schema: "seq");

            migrationBuilder.DropSequence(
                name: "Seq_None",
                schema: "seq");
        }
    }
}
