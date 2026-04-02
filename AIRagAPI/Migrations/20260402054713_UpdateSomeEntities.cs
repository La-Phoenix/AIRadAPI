using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIRagAPI.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSomeEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_ConversationId",
                table: "Messages");

            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "Messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TokenCount",
                table: "Messages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Conversations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MessageCount",
                table: "Conversations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "Conversations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConversationId_Order",
                table: "Messages",
                columns: new[] { "ConversationId", "Order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_ConversationId_Order",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "Order",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "TokenCount",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "MessageCount",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "Conversations");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConversationId",
                table: "Messages",
                column: "ConversationId");
        }
    }
}
