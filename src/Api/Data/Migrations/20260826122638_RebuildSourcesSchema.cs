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
            migrationBuilder.RenameColumn(
                name: "Type",
                table: "Sources",
                newName: "Tier");

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

            migrationBuilder.RenameColumn(
                name: "Tier",
                table: "Sources",
                newName: "Type");

            migrationBuilder.RenameColumn(
                name: "Slug",
                table: "Sources",
                newName: "Name");
        }
    }
}
