using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoveHimalayasToTierC : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Himalayas was seeded at Tier B, the tier for sources that are free
            // and keyless. Its terms require Himalayas' prior written approval
            // (A-003), which is the definition of Tier C — "requires registration,
            // an API key, or partner approval". At Tier B the only thing between
            // an uncleared upstream and a live request was two mutable booleans;
            // at Tier C the ingest refuses it by tier, before Enabled is read.
            migrationBuilder.Sql(@"UPDATE ""Sources"" SET ""Tier"" = 2 WHERE ""Slug"" = 'himalayas';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE ""Sources"" SET ""Tier"" = 1 WHERE ""Slug"" = 'himalayas';");
        }
    }
}
