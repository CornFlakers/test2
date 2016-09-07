using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MvcMovie.Data.Migrations
{
    public partial class BiWeeklyExp : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "transType",
                table: "Trans",
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "Trans",
                maxLength: 60,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "transType",
                table: "Trans",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "Trans",
                nullable: true);
        }
    }
}
