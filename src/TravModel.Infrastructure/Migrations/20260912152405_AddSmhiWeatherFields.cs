using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravModel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSmhiWeatherFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Tracks",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Tracks",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ValidAtUtc",
                table: "Observations",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "ValidAtUtc",
                table: "Observations");
        }
    }
}
