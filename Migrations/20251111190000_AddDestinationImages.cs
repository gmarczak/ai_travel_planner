using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace project.Migrations
{
    public partial class AddDestinationImages : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DestinationImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Destination = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PhotographerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PhotographerUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CachedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsageCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DestinationImages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DestinationImages_Destination",
                table: "DestinationImages",
                column: "Destination",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DestinationImages_CachedAt",
                table: "DestinationImages",
                column: "CachedAt");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DestinationImages");
        }
    }
}
