using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace project.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalIdToTravelPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "TravelPlans",
                type: "TEXT",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "TravelPlans");
        }
    }
}
