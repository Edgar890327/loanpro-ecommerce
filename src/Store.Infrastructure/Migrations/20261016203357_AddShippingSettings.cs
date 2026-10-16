using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Store.Infrastructure.Migrations
{
    public partial class AddShippingSettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                table: "PaymentProviderSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "ShippingStrategySettings",
                columns: table => new
                {
                    StrategyKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PublicConfigurationJson = table.Column<string>(type: "text", nullable: false),
                    ProtectedSecrets = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShippingStrategySettings", x => x.StrategyKey);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShippingStrategySettings_IsActive",
                table: "ShippingStrategySettings",
                column: "IsActive",
                unique: true,
                filter: "\"IsActive\" = TRUE");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShippingStrategySettings");

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                table: "PaymentProviderSettings");
        }
    }
}
