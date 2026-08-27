using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedTierBSources : Migration
    {
        private static readonly DateTimeOffset SpikeDate = new(2026, 8, 26, 0, 0, 0, TimeSpan.Zero);
        private static readonly TimeSpan OneHour = TimeSpan.FromHours(1);
        private static readonly TimeSpan OneDay = TimeSpan.FromHours(24);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The Tier B half of EM-53's four-adapter set. Greenhouse (Id 2) and
            // Lever (Id 3) were seeded by SeedGreenhouseLeverTargetCompanies.
            // Compliance fields are transcribed from docs/SOURCES.md and, for both
            // rows, from the terms each API embeds in its own response body —
            // stronger evidence than a scraped ToS page.
            migrationBuilder.InsertData(
                table: "Sources",
                columns: new[]
                {
                    "Id", "Slug", "DisplayName", "Tier", "BaseUrl", "AdapterType", "AuthKind",
                    "MinPollInterval", "AttributionRequired", "AttributionHtml",
                    "CanonicalUrlRequired", "TermsUrl", "TermsReviewedAt", "PublicDeployEnabled",
                    "Enabled", "LastSuccessAt", "LastErrorAt", "ConsecutiveFailures", "CreatedAt",
                },
                values: new object[,]
                {
                    {
                        // Binding constraint, not a default: Jobicy's polling cap is
                        // once per hour and the scheduler reads it from this column.
                        4, "jobicy", "Jobicy", /* Tier.B */ 1,
                        "https://jobicy.com", "jobicy", /* AuthKind.None */ 0,
                        OneHour, true,
                        "Sourced from <a href=\"https://jobicy.com\" rel=\"noopener\">Jobicy</a>",
                        // friendlyNotice: application buttons must redirect to the
                        // original job URL provided in the feed.
                        true, "https://jobi.cy/apidocs", SpikeDate, true,
                        true, null, null, 0, SpikeDate,
                    },
                    {
                        5, "arbeitnow", "Arbeitnow", /* Tier.B */ 1,
                        "https://www.arbeitnow.com", "arbeitnow", /* AuthKind.None */ 0,
                        OneHour, true,
                        "Sourced from <a href=\"https://www.arbeitnow.com\" rel=\"noopener\">Arbeitnow</a>",
                        // meta.terms asks for a link back but states no requirement
                        // about where the apply button points.
                        false, "https://www.arbeitnow.com/terms", SpikeDate, true,
                        true, null, null, 0, SpikeDate,
                    },
                    {
                        // Spiked and technically working, but its terms require prior
                        // written approval that does not exist (A-003). The row is
                        // recorded so the exclusion is enforced by data rather than by
                        // remembering not to write the adapter. Both flags stay false
                        // until that approval exists.
                        6, "himalayas", "Himalayas", /* Tier.B */ 1,
                        "https://himalayas.app", "himalayas", /* AuthKind.None */ 0,
                        OneDay, true,
                        "Sourced from <a href=\"https://himalayas.app\" rel=\"noopener\">Himalayas</a>",
                        false, "https://himalayas.app/terms", SpikeDate, false,
                        false, null, null, 0, SpikeDate,
                    },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Sources", keyColumn: "Id", keyValues: new object[] { 4, 5, 6 });
        }
    }
}
