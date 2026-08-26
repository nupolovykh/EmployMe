using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RecordSezzleRejection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Not part of the EM-50 seed batch — found while mining leftover verification data
            // from an earlier (abandoned) blind company search. Recorded as `rejected` rather than
            // discarded, per the registry's own stated purpose: a documented rejection prevents
            // re-discovering and re-evaluating the same company later for free.
            migrationBuilder.InsertData(
                table: "TargetCompanies",
                columns: new[]
                {
                    "Id", "SourceId", "CompanyName", "BoardToken", "WhyTarget", "HiringGeo",
                    "Status", "VerifiedAt", "JobsSeen",
                },
                values: new object[,]
                {
                    {
                        7, /* Greenhouse */ 2, "Sezzle", "sezzle",
                        "Real per-country remote hiring at junior level (\"Junior Software Engineer\", \"Software Engineer II\" titles across Argentina/Brazil/Chile/Colombia/Mexico/Turkey/Poland/India/Venezuela) — closest structural match found to the Georgia-compatible model, but Georgia is not one of the listed countries as of this check. Rejected on the geography criterion alone; revisit if they add it.",
                        /* GlobalRemote -- imprecise: HiringGeo has no "per-country list, not Georgia" value; kept for schema simplicity, see WhyTarget for the actual nuance */ 0,
                        /* Rejected */ 2, new DateTimeOffset(2026, 8, 26, 0, 0, 0, TimeSpan.Zero), 183,
                    },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "TargetCompanies",
                keyColumn: "Id",
                keyValues: new object[] { 7 });
        }
    }
}
