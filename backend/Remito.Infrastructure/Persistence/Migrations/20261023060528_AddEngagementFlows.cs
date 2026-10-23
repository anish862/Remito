using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Remito.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEngagementFlows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReferralConversions_ExternalRef",
                table: "ReferralConversions");

            migrationBuilder.DropIndex(
                name: "IX_Favourites_UserId_Type_TargetId",
                table: "Favourites");

            migrationBuilder.AddColumn<bool>(
                name: "DailySummaryEnabled",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastAlertSummaryAt",
                table: "Users",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuietHoursEndMinute",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuietHoursStartMinute",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RateAlertsEnabled",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExchangeId",
                table: "ReferralConversions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql(
                """
                UPDATE conversion
                SET ExchangeId = redirect.ExchangeId
                FROM ReferralConversions AS conversion
                INNER JOIN RedirectEvents AS redirect ON redirect.ClickId = conversion.ClickId;
                """);

            migrationBuilder.AddColumn<string>(
                name: "TargetCode",
                table: "Favourites",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AllowedRedirectHosts",
                table: "Exchanges",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommissionCurrencyCode",
                table: "Exchanges",
                type: "nchar(3)",
                fixedLength: true,
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CommissionFlatAmount",
                table: "Exchanges",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0.50m);

            migrationBuilder.AddColumn<bool>(
                name: "DailySummary",
                table: "Alerts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsArmed",
                table: "Alerts",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOneTime",
                table: "Alerts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PartnerReconciliationIssues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExchangeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalRef = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ClickId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IssueType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsResolved = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerReconciliationIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartnerReconciliationIssues_Exchanges_ExchangeId",
                        column: x => x.ExchangeId,
                        principalTable: "Exchanges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PartnerReportRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExchangeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalRef = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ClickId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ConvertedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Commission = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerReportRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartnerReportRows_Currencies_CurrencyCode",
                        column: x => x.CurrencyCode,
                        principalTable: "Currencies",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PartnerReportRows_Exchanges_ExchangeId",
                        column: x => x.ExchangeId,
                        principalTable: "Exchanges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                columns: new[] { "AllowedRedirectHosts", "CommissionCurrencyCode", "CommissionFlatAmount" },
                values: new object[] { null, null, 0.50m });

            migrationBuilder.UpdateData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"),
                columns: new[] { "AllowedRedirectHosts", "CommissionCurrencyCode", "CommissionFlatAmount" },
                values: new object[] { null, null, 0.50m });

            migrationBuilder.UpdateData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"),
                columns: new[] { "AllowedRedirectHosts", "CommissionCurrencyCode", "CommissionFlatAmount" },
                values: new object[] { null, null, 0.50m });

            migrationBuilder.UpdateData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000004"),
                columns: new[] { "AllowedRedirectHosts", "CommissionCurrencyCode", "CommissionFlatAmount" },
                values: new object[] { null, null, 0.50m });

            migrationBuilder.UpdateData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000005"),
                columns: new[] { "AllowedRedirectHosts", "CommissionCurrencyCode", "CommissionFlatAmount" },
                values: new object[] { null, null, 0.50m });

            migrationBuilder.UpdateData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000006"),
                columns: new[] { "AllowedRedirectHosts", "CommissionCurrencyCode", "CommissionFlatAmount" },
                values: new object[] { null, null, 0.50m });

            migrationBuilder.UpdateData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000007"),
                columns: new[] { "AllowedRedirectHosts", "CommissionCurrencyCode", "CommissionFlatAmount" },
                values: new object[] { null, null, 0.50m });

            migrationBuilder.UpdateData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000008"),
                columns: new[] { "AllowedRedirectHosts", "CommissionCurrencyCode", "CommissionFlatAmount" },
                values: new object[] { null, null, 0.50m });

            migrationBuilder.UpdateData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000009"),
                columns: new[] { "AllowedRedirectHosts", "CommissionCurrencyCode", "CommissionFlatAmount" },
                values: new object[] { null, null, 0.50m });

            migrationBuilder.UpdateData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000010"),
                columns: new[] { "AllowedRedirectHosts", "CommissionCurrencyCode", "CommissionFlatAmount" },
                values: new object[] { null, null, 0.50m });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_QuietHoursEndMinute",
                table: "Users",
                sql: "[QuietHoursEndMinute] IS NULL OR [QuietHoursEndMinute] BETWEEN 0 AND 1439");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_QuietHoursStartMinute",
                table: "Users",
                sql: "[QuietHoursStartMinute] IS NULL OR [QuietHoursStartMinute] BETWEEN 0 AND 1439");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralConversions_ExchangeId_ExternalRef",
                table: "ReferralConversions",
                columns: new[] { "ExchangeId", "ExternalRef" },
                unique: true,
                filter: "[ExternalRef] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Favourites_UserId_Type_TargetId_TargetCode",
                table: "Favourites",
                columns: new[] { "UserId", "Type", "TargetId", "TargetCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Exchanges_CommissionCurrencyCode",
                table: "Exchanges",
                column: "CommissionCurrencyCode");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerReconciliationIssues_ExchangeId_ExternalRef",
                table: "PartnerReconciliationIssues",
                columns: new[] { "ExchangeId", "ExternalRef" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartnerReportRows_CurrencyCode",
                table: "PartnerReportRows",
                column: "CurrencyCode");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerReportRows_ExchangeId_ConvertedAt",
                table: "PartnerReportRows",
                columns: new[] { "ExchangeId", "ConvertedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerReportRows_ExchangeId_ExternalRef",
                table: "PartnerReportRows",
                columns: new[] { "ExchangeId", "ExternalRef" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Exchanges_Currencies_CommissionCurrencyCode",
                table: "Exchanges",
                column: "CommissionCurrencyCode",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReferralConversions_Exchanges_ExchangeId",
                table: "ReferralConversions",
                column: "ExchangeId",
                principalTable: "Exchanges",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Exchanges_Currencies_CommissionCurrencyCode",
                table: "Exchanges");

            migrationBuilder.DropForeignKey(
                name: "FK_ReferralConversions_Exchanges_ExchangeId",
                table: "ReferralConversions");

            migrationBuilder.DropTable(
                name: "PartnerReconciliationIssues");

            migrationBuilder.DropTable(
                name: "PartnerReportRows");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_QuietHoursEndMinute",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_QuietHoursStartMinute",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_ReferralConversions_ExchangeId_ExternalRef",
                table: "ReferralConversions");

            migrationBuilder.DropIndex(
                name: "IX_Favourites_UserId_Type_TargetId_TargetCode",
                table: "Favourites");

            migrationBuilder.DropIndex(
                name: "IX_Exchanges_CommissionCurrencyCode",
                table: "Exchanges");

            migrationBuilder.DropColumn(
                name: "DailySummaryEnabled",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastAlertSummaryAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "QuietHoursEndMinute",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "QuietHoursStartMinute",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RateAlertsEnabled",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ExchangeId",
                table: "ReferralConversions");

            migrationBuilder.DropColumn(
                name: "TargetCode",
                table: "Favourites");

            migrationBuilder.DropColumn(
                name: "AllowedRedirectHosts",
                table: "Exchanges");

            migrationBuilder.DropColumn(
                name: "CommissionCurrencyCode",
                table: "Exchanges");

            migrationBuilder.DropColumn(
                name: "CommissionFlatAmount",
                table: "Exchanges");

            migrationBuilder.DropColumn(
                name: "DailySummary",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "IsArmed",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "IsOneTime",
                table: "Alerts");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralConversions_ExternalRef",
                table: "ReferralConversions",
                column: "ExternalRef",
                unique: true,
                filter: "[ExternalRef] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Favourites_UserId_Type_TargetId",
                table: "Favourites",
                columns: new[] { "UserId", "Type", "TargetId" },
                unique: true);
        }
    }
}
