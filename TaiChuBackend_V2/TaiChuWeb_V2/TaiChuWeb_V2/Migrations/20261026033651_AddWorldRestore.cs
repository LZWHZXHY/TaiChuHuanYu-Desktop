using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaiChuWeb_V2.Migrations
{
    /// <inheritdoc />
    public partial class AddWorldRestore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorldRestores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    OwnerId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SourceProjectId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TargetProjectId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    State = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Plan = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Uploaded = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorldRestores", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");









            migrationBuilder.CreateIndex(
                name: "IX_WorldRestores_OwnerId_SourceProjectId",
                table: "WorldRestores",
                columns: new[] { "OwnerId", "SourceProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorldRestores_State_UpdatedAt",
                table: "WorldRestores",
                columns: new[] { "State", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorldRestores");








        }
    }
}
