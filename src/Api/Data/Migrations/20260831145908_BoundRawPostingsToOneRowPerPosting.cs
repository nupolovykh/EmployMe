using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class BoundRawPostingsToOneRowPerPosting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The table was append-only until now, so every re-ingest of the same
            // posting left another row behind. The unique index below cannot be
            // created while those survive — on the deployed database this was 21
            // duplicates across 1,016 rows, and without this step the migration
            // fails at deploy time rather than here.
            //
            // The newest row per posting is the one kept: this table exists to
            // replay a mapping bug against a posting's current shape.
            migrationBuilder.Sql("""
                DELETE FROM "RawPostings" r
                USING "RawPostings" newer
                WHERE r."SourceId" = newer."SourceId"
                  AND r."ExternalId" = newer."ExternalId"
                  AND (r."FetchedAt", r."Id") < (newer."FetchedAt", newer."Id");
                """);

            migrationBuilder.DropIndex(
                name: "IX_RawPostings_SourceId_ExternalId",
                table: "RawPostings");

            migrationBuilder.CreateIndex(
                name: "IX_RawPostings_SourceId_ExternalId",
                table: "RawPostings",
                columns: new[] { "SourceId", "ExternalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only the index is reversible. The rows deleted above are gone, and
            // re-running ingest is what repopulates them — one per posting.
            migrationBuilder.DropIndex(
                name: "IX_RawPostings_SourceId_ExternalId",
                table: "RawPostings");

            migrationBuilder.CreateIndex(
                name: "IX_RawPostings_SourceId_ExternalId",
                table: "RawPostings",
                columns: new[] { "SourceId", "ExternalId" });
        }
    }
}
