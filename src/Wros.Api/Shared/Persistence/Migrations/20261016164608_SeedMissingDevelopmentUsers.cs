using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wros.Api.Shared.Persistence.Migrations;

public partial class SeedMissingDevelopmentUsers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Retain this migration ID for existing databases. EF UseSeeding creates missing
        // Development accounts after migration application, including on repeated updates.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve users and audit history.
    }
}
