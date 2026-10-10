using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mundialito.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTraceabilityToDomainEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "DomainEvents",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TraceId",
                table: "DomainEvents",
                type: "nchar(32)",
                fixedLength: true,
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DomainEvents_TraceId",
                table: "DomainEvents",
                column: "TraceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DomainEvents_TraceId",
                table: "DomainEvents");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "DomainEvents");

            migrationBuilder.DropColumn(
                name: "TraceId",
                table: "DomainEvents");
        }
    }
}
