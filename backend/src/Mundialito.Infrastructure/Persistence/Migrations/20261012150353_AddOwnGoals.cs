using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mundialito.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnGoals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOwnGoal",
                table: "Goals",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsOwnGoal",
                table: "Goals");
        }
    }
}
