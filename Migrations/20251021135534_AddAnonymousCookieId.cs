using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace project.Migrations
{
    /// <inheritdoc />
    public partial class AddAnonymousCookieId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnonymousCookieId",
                table: "TravelPlans",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TravelPlans_AnonymousCookieId",
                table: "TravelPlans",
                column: "AnonymousCookieId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TravelPlans_AnonymousCookieId",
                table: "TravelPlans");

            migrationBuilder.DropColumn(
                name: "AnonymousCookieId",
                table: "TravelPlans");
        }
    }
}
