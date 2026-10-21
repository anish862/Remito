using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Remito.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRatePipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ManualRateOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Pair = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Bid = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Ask = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Mid = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManualRateOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManualRateOverrides_RateSources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "RateSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RateQuarantines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Pair = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Bid = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Ask = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Mid = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    FetchedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    QuarantinedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RateQuarantines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RateQuarantines_RateSources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "RateSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ManualRateOverrides_Pair_ExpiresAt",
                table: "ManualRateOverrides",
                columns: new[] { "Pair", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ManualRateOverrides_SourceId",
                table: "ManualRateOverrides",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_RateQuarantines_SourceId_Pair_QuarantinedAt",
                table: "RateQuarantines",
                columns: new[] { "SourceId", "Pair", "QuarantinedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ManualRateOverrides");

            migrationBuilder.DropTable(
                name: "RateQuarantines");
        }
    }
}
