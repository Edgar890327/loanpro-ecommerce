using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Store.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ImportBatchUserForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImportedBy",
                table: "ImportBatches");

            migrationBuilder.AddColumn<Guid>(
                name: "ImportedByUserId",
                table: "ImportBatches",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_ImportedByUserId",
                table: "ImportBatches",
                column: "ImportedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ImportBatches_Users_ImportedByUserId",
                table: "ImportBatches",
                column: "ImportedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ImportBatches_Users_ImportedByUserId",
                table: "ImportBatches");

            migrationBuilder.DropIndex(
                name: "IX_ImportBatches_ImportedByUserId",
                table: "ImportBatches");

            migrationBuilder.DropColumn(
                name: "ImportedByUserId",
                table: "ImportBatches");

            migrationBuilder.AddColumn<string>(
                name: "ImportedBy",
                table: "ImportBatches",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }
    }
}
