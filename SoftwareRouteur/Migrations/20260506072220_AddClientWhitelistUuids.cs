using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoftwareRouteur.Migrations
{
    /// <inheritdoc />
    public partial class AddClientWhitelistUuids : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "opnsense_whitelist_uuid",
                table: "clients",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "opnsense_allow_rule_uuid",
                table: "clients",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "opnsense_whitelist_uuid",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "opnsense_allow_rule_uuid",
                table: "clients");
        }
    }
}
