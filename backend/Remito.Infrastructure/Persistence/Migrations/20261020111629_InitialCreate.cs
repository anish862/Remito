using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Remito.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdCampaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdCampaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Currencies",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DecimalPlaces = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "Exchanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LogoUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    HasApp = table.Column<bool>(type: "bit", nullable: false),
                    AppDeepLink = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    PlayStoreUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    GatewayUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Tier = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsPlaceholder = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exchanges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NewsItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RateHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Pair = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    Bucket = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RateHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RateSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RateSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdCreatives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Slot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    TargetUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    AltText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdCreatives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdCreatives_AdCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "AdCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Corridors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromCurrency = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    ToCurrency = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Corridors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Corridors_Currencies_FromCurrency",
                        column: x => x.FromCurrency,
                        principalTable: "Currencies",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Corridors_Currencies_ToCurrency",
                        column: x => x.ToCurrency,
                        principalTable: "Currencies",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Pair = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    Bid = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Ask = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Mid = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FetchedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsStale = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketRates_RateSources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "RateSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Alerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Pair = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Target = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastTriggeredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Alerts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FcmToken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FcmTokenHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Platform = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Devices_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Favourites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Favourites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Favourites_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AdImpressions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreativeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdImpressions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdImpressions_AdCreatives_CreativeId",
                        column: x => x.CreativeId,
                        principalTable: "AdCreatives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdImpressions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExchangeFees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExchangeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorridorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MinAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    FlatFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PercentFee = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeFees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExchangeFees_Corridors_CorridorId",
                        column: x => x.CorridorId,
                        principalTable: "Corridors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExchangeFees_Exchanges_ExchangeId",
                        column: x => x.ExchangeId,
                        principalTable: "Exchanges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExchangeRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExchangeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorridorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    FetchedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsStale = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExchangeRates_Corridors_CorridorId",
                        column: x => x.CorridorId,
                        principalTable: "Corridors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExchangeRates_Exchanges_ExchangeId",
                        column: x => x.ExchangeId,
                        principalTable: "Exchanges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RedirectEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClickId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExchangeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorridorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RedirectEvents", x => x.Id);
                    table.UniqueConstraint("AK_RedirectEvents_ClickId", x => x.ClickId);
                    table.ForeignKey(
                        name: "FK_RedirectEvents_Corridors_CorridorId",
                        column: x => x.CorridorId,
                        principalTable: "Corridors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RedirectEvents_Exchanges_ExchangeId",
                        column: x => x.ExchangeId,
                        principalTable: "Exchanges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RedirectEvents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReferralConversions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClickId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExternalRef = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ConvertedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferralConversions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReferralConversions_RedirectEvents_ClickId",
                        column: x => x.ClickId,
                        principalTable: "RedirectEvents",
                        principalColumn: "ClickId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommissionLedgers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionLedgers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommissionLedgers_Currencies_CurrencyCode",
                        column: x => x.CurrencyCode,
                        principalTable: "Currencies",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommissionLedgers_ReferralConversions_ConversionId",
                        column: x => x.ConversionId,
                        principalTable: "ReferralConversions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Currencies",
                columns: new[] { "Code", "DecimalPlaces", "Name", "Symbol" },
                values: new object[,]
                {
                    { "AED", 2, "United Arab Emirates Dirham", "د.إ" },
                    { "BDT", 2, "Bangladeshi Taka", "৳" },
                    { "INR", 2, "Indian Rupee", "₹" },
                    { "LKR", 2, "Sri Lankan Rupee", "රු" },
                    { "NPR", 2, "Nepalese Rupee", "रू" },
                    { "OMR", 3, "Omani Rial", "ر.ع." },
                    { "PHP", 2, "Philippine Peso", "₱" },
                    { "PKR", 2, "Pakistani Rupee", "₨" },
                    { "SAR", 2, "Saudi Riyal", "ر.س" },
                    { "USD", 2, "United States Dollar", "$" }
                });

            migrationBuilder.InsertData(
                table: "Exchanges",
                columns: new[] { "Id", "AppDeepLink", "GatewayUrl", "HasApp", "IsPlaceholder", "LogoUrl", "Name", "PlayStoreUrl", "Slug", "Status", "Tier" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), null, null, false, true, null, "Placeholder Exchange 01", null, "placeholder-exchange-01", "Pending", "Standard" },
                    { new Guid("10000000-0000-0000-0000-000000000002"), null, null, false, true, null, "Placeholder Exchange 02", null, "placeholder-exchange-02", "Pending", "Standard" },
                    { new Guid("10000000-0000-0000-0000-000000000003"), null, null, false, true, null, "Placeholder Exchange 03", null, "placeholder-exchange-03", "Pending", "Standard" },
                    { new Guid("10000000-0000-0000-0000-000000000004"), null, null, false, true, null, "Placeholder Exchange 04", null, "placeholder-exchange-04", "Pending", "Standard" },
                    { new Guid("10000000-0000-0000-0000-000000000005"), null, null, false, true, null, "Placeholder Exchange 05", null, "placeholder-exchange-05", "Pending", "Standard" },
                    { new Guid("10000000-0000-0000-0000-000000000006"), null, null, false, true, null, "Placeholder Exchange 06", null, "placeholder-exchange-06", "Pending", "Standard" },
                    { new Guid("10000000-0000-0000-0000-000000000007"), null, null, false, true, null, "Placeholder Exchange 07", null, "placeholder-exchange-07", "Pending", "Standard" },
                    { new Guid("10000000-0000-0000-0000-000000000008"), null, null, false, true, null, "Placeholder Exchange 08", null, "placeholder-exchange-08", "Pending", "Standard" },
                    { new Guid("10000000-0000-0000-0000-000000000009"), null, null, false, true, null, "Placeholder Exchange 09", null, "placeholder-exchange-09", "Pending", "Standard" },
                    { new Guid("10000000-0000-0000-0000-000000000010"), null, null, false, true, null, "Placeholder Exchange 10", null, "placeholder-exchange-10", "Pending", "Standard" }
                });

            migrationBuilder.InsertData(
                table: "Corridors",
                columns: new[] { "Id", "FromCurrency", "IsActive", "ToCurrency" },
                values: new object[] { new Guid("0f43b2c9-44b3-4b91-8e7f-2aa9e19ecb01"), "OMR", true, "INR" });

            migrationBuilder.CreateIndex(
                name: "IX_AdCampaigns_IsActive_StartsAt_EndsAt",
                table: "AdCampaigns",
                columns: new[] { "IsActive", "StartsAt", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AdCreatives_CampaignId",
                table: "AdCreatives",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_AdCreatives_Slot_SortOrder",
                table: "AdCreatives",
                columns: new[] { "Slot", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_AdImpressions_CreativeId_CreatedAt",
                table: "AdImpressions",
                columns: new[] { "CreativeId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AdImpressions_UserId",
                table: "AdImpressions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_Pair_IsActive_Direction_Target",
                table: "Alerts",
                columns: new[] { "Pair", "IsActive", "Direction", "Target" });

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_UserId_IsActive",
                table: "Alerts",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId_CreatedAt",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId_CreatedAt",
                table: "AuditLogs",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CommissionLedgers_ConversionId",
                table: "CommissionLedgers",
                column: "ConversionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommissionLedgers_CurrencyCode",
                table: "CommissionLedgers",
                column: "CurrencyCode");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionLedgers_Status_CurrencyCode",
                table: "CommissionLedgers",
                columns: new[] { "Status", "CurrencyCode" });

            migrationBuilder.CreateIndex(
                name: "IX_Corridors_FromCurrency_ToCurrency",
                table: "Corridors",
                columns: new[] { "FromCurrency", "ToCurrency" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Corridors_ToCurrency",
                table: "Corridors",
                column: "ToCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_FcmTokenHash",
                table: "Devices",
                column: "FcmTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_UserId_Platform",
                table: "Devices",
                columns: new[] { "UserId", "Platform" });

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeFees_CorridorId",
                table: "ExchangeFees",
                column: "CorridorId");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeFees_ExchangeId_CorridorId_MinAmount_MaxAmount",
                table: "ExchangeFees",
                columns: new[] { "ExchangeId", "CorridorId", "MinAmount", "MaxAmount" });

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_CorridorId_IsStale_Rate",
                table: "ExchangeRates",
                columns: new[] { "CorridorId", "IsStale", "Rate" });

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_ExchangeId_CorridorId_FetchedAt",
                table: "ExchangeRates",
                columns: new[] { "ExchangeId", "CorridorId", "FetchedAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Exchanges_Slug",
                table: "Exchanges",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Exchanges_Status_Tier",
                table: "Exchanges",
                columns: new[] { "Status", "Tier" });

            migrationBuilder.CreateIndex(
                name: "IX_Favourites_UserId_Type_TargetId",
                table: "Favourites",
                columns: new[] { "UserId", "Type", "TargetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketRates_Pair_FetchedAt",
                table: "MarketRates",
                columns: new[] { "Pair", "FetchedAt" },
                descending: new[] { false, true })
                .Annotation("SqlServer:Include", new[] { "Mid", "SourceId", "IsStale" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketRates_SourceId_Pair_FetchedAt",
                table: "MarketRates",
                columns: new[] { "SourceId", "Pair", "FetchedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_NewsItems_IsPublished_PublishedAt",
                table: "NewsItems",
                columns: new[] { "IsPublished", "PublishedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_NewsItems_Slug",
                table: "NewsItems",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RateHistories_Pair_Bucket_Timestamp",
                table: "RateHistories",
                columns: new[] { "Pair", "Bucket", "Timestamp" },
                unique: true,
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_RateSources_IsActive_Priority",
                table: "RateSources",
                columns: new[] { "IsActive", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_RateSources_Name",
                table: "RateSources",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RedirectEvents_ClickId",
                table: "RedirectEvents",
                column: "ClickId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RedirectEvents_CorridorId_CreatedAt",
                table: "RedirectEvents",
                columns: new[] { "CorridorId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RedirectEvents_ExchangeId_CreatedAt",
                table: "RedirectEvents",
                columns: new[] { "ExchangeId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RedirectEvents_UserId",
                table: "RedirectEvents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralConversions_ClickId",
                table: "ReferralConversions",
                column: "ClickId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReferralConversions_ExternalRef",
                table: "ReferralConversions",
                column: "ExternalRef",
                unique: true,
                filter: "[ExternalRef] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_DeletedAt",
                table: "Users",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true,
                filter: "[Email] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_ExternalId",
                table: "Users",
                column: "ExternalId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdImpressions");

            migrationBuilder.DropTable(
                name: "Alerts");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "CommissionLedgers");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "ExchangeFees");

            migrationBuilder.DropTable(
                name: "ExchangeRates");

            migrationBuilder.DropTable(
                name: "Favourites");

            migrationBuilder.DropTable(
                name: "MarketRates");

            migrationBuilder.DropTable(
                name: "NewsItems");

            migrationBuilder.DropTable(
                name: "RateHistories");

            migrationBuilder.DropTable(
                name: "AdCreatives");

            migrationBuilder.DropTable(
                name: "ReferralConversions");

            migrationBuilder.DropTable(
                name: "RateSources");

            migrationBuilder.DropTable(
                name: "AdCampaigns");

            migrationBuilder.DropTable(
                name: "RedirectEvents");

            migrationBuilder.DropTable(
                name: "Corridors");

            migrationBuilder.DropTable(
                name: "Exchanges");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Currencies");
        }
    }
}
