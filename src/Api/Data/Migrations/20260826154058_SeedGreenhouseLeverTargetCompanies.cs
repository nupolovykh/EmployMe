using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedGreenhouseLeverTargetCompanies : Migration
    {
        private static readonly DateTimeOffset SpikeDate = new(2026, 8, 26, 0, 0, 0, TimeSpan.Zero);
        private static readonly TimeSpan OneHour = TimeSpan.FromHours(1);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Adapter-level rows for the two Tier A sources cleared in EM-45/46 (spikes/greenhouse,
            // spikes/lever). Compliance fields transcribed from docs/SOURCES.md, not re-asserted:
            // neither ATS states an attribution requirement, so AttributionRequired/
            // CanonicalUrlRequired are false; MinPollInterval is the conservative "≥1h, no
            // rate-limit headers observed" figure from both NOTES.md files, not a hard vendor
            // constraint like Jobicy's.
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
                        // Id 1 is taken by a pre-existing "hh.ru" row left over from before the
                        // EM-51 schema rebuild (local dev DB, not part of any migration) —
                        // starting at 2 so this seed doesn't collide with it.
                        2, "greenhouse", "Greenhouse", /* Tier.A */ 0,
                        "https://boards-api.greenhouse.io", "greenhouse", /* AuthKind.None */ 0,
                        OneHour, false, null,
                        false, "https://developers.greenhouse.io/job-board.html", SpikeDate, true,
                        true, null, null, 0, SpikeDate,
                    },
                    {
                        3, "lever", "Lever", /* Tier.A */ 0,
                        "https://api.lever.co", "lever", /* AuthKind.None */ 0,
                        OneHour, false, null,
                        false, "https://hire.lever.co/developer/documentation", SpikeDate, true,
                        true, null, null, 0, SpikeDate,
                    },
                });

            // Target-company registry seed (EM-50, revised scope: a small live-verified seed to
            // unblock the EM-45/46 spikes, not 30 rows collected up front — see docs/SOURCES.md).
            // status: 0=Active, 1=Watch, 2=Rejected. hiring_geo: 0=GlobalRemote, 1=EuRemote,
            // 2=RelocationSponsor, 3=OnsiteOnly.
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
                        1, 2, "Remote", "remotecom",
                        "Global EOR/payroll platform, hires backend; none open in this snapshot",
                        /* GlobalRemote */ 0, /* Watch */ 1, SpikeDate, 100,
                    },
                    {
                        2, 2, "Remote People", "remotepeople",
                        "Global remote-hiring platform; has an open Back-End Engineer (Python) role and junior-titled roles elsewhere",
                        /* GlobalRemote */ 0, /* Active */ 0, SpikeDate, 17,
                    },
                    {
                        3, 2, "Xapo Bank", "xapo61",
                        "Crypto/digital bank, explicit \"Remote - Work from Anywhere\"; no current backend opening but the hiring model is Georgia-compatible",
                        /* GlobalRemote */ 0, /* Watch */ 1, SpikeDate, 12,
                    },
                    {
                        4, 3, "Qonto", "qonto",
                        "French fintech, EU-remote; current postings are non-eng",
                        /* EuRemote */ 1, /* Watch */ 1, SpikeDate, 5,
                    },
                    {
                        5, 3, "RemoFirst", "remofirst",
                        "Global remote-first HR platform; thin posting volume right now",
                        /* GlobalRemote */ 0, /* Watch */ 1, SpikeDate, 1,
                    },
                    {
                        6, 3, "Peerspace", "peerspace",
                        "US marketplace startup, remote-friendly; current backend opening is senior-only (Clojure)",
                        /* GlobalRemote */ 0, /* Watch */ 1, SpikeDate, 3,
                    },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "TargetCompanies",
                keyColumn: "Id",
                keyValues: new object[] { 1, 2, 3, 4, 5, 6 });

            migrationBuilder.DeleteData(
                table: "Sources",
                keyColumn: "Id",
                keyValues: new object[] { 2, 3 });
        }
    }
}
