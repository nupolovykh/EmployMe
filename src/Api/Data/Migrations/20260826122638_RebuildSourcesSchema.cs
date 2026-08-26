using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RebuildSourcesSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Not a RenameColumn: the old SourceType{HhRu=0,JobsGe=1,International=2} and the new
            // SourceTier{A=0,B=1,C=2,D=3} enums don't share ordinal meaning. Renaming the column
            // would silently reinterpret any stored value under the new enum (e.g. HhRu(0) read
            // back as Tier.A(0)) — exactly the failure shape CLAUDE.md already documents for why
            // SourceType.HhRu couldn't just be removed from the old enum. Drop and re-add instead,
            // defaulting new rows to the most restrictive tier (D) rather than inheriting a
            // meaning from the column being replaced.
            migrationBuilder.DropColumn(
                name: "Type",
                table: "Sources");

            migrationBuilder.AddColumn<int>(
                name: "Tier",
                table: "Sources",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Sources",
                newName: "Slug");

            migrationBuilder.AddColumn<string>(
                name: "AdapterType",
                table: "Sources",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AttributionHtml",
                table: "Sources",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AttributionRequired",
                table: "Sources",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "AuthKind",
                table: "Sources",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "CanonicalUrlRequired",
                table: "Sources",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ConsecutiveFailures",
                table: "Sources",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "Sources",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "Enabled",
                table: "Sources",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastErrorAt",
                table: "Sources",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSuccessAt",
                table: "Sources",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "MinPollInterval",
                table: "Sources",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<bool>(
                name: "PublicDeployEnabled",
                table: "Sources",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TermsReviewedAt",
                table: "Sources",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TermsUrl",
                table: "Sources",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RawPostings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceId = table.Column<int>(type: "integer", nullable: false),
                    ExternalId = table.Column<string>(type: "text", nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    FetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawPostings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RawPostings_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sources_Slug",
                table: "Sources",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RawPostings_SourceId_ExternalId",
                table: "RawPostings",
                columns: new[] { "SourceId", "ExternalId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RawPostings");

            migrationBuilder.DropIndex(
                name: "IX_Sources_Slug",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "AdapterType",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "AttributionHtml",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "AttributionRequired",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "AuthKind",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "CanonicalUrlRequired",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "ConsecutiveFailures",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "Enabled",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "LastErrorAt",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "LastSuccessAt",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "MinPollInterval",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "PublicDeployEnabled",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "TermsReviewedAt",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "TermsUrl",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "Tier",
                table: "Sources");

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Sources",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.RenameColumn(
                name: "Slug",
                table: "Sources",
                newName: "Name");
        }
    }
}
