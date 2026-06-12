using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Support.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OutboxAndDraftFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClassificationOutbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FreeText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PNR = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    PassengerLastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassificationOutbox", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationOutbox_Status_CreatedAt",
                table: "ClassificationOutbox",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClassificationOutbox");
        }
    }
}
