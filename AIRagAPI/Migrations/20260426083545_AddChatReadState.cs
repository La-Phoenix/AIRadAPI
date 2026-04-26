using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIRagAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddChatReadState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastReadAt",
                table: "ChatMembers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastReadOrder",
                table: "ChatMembers",
                type: "integer",
                nullable: false,
                defaultValue: -1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastReadAt",
                table: "ChatMembers");

            migrationBuilder.DropColumn(
                name: "LastReadOrder",
                table: "ChatMembers");
        }
    }
}
