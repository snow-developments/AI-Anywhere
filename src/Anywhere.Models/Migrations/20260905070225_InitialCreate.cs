using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Anywhere.Models.Migrations {
  /// <inheritdoc />
  public partial class InitialCreate : Migration {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) {
      migrationBuilder.CreateTable(
          name: "Profiles",
          columns: table => new {
            Id = table.Column<int>(type: "INTEGER", nullable: false)
                  .Annotation("Sqlite:Autoincrement", true),
            Name = table.Column<string>(type: "TEXT", nullable: false),
            Command = table.Column<string>(type: "TEXT", nullable: false),
            Args = table.Column<string>(type: "TEXT", nullable: false),
            Env = table.Column<string>(type: "TEXT", nullable: false),
            WorkingDir = table.Column<string>(type: "TEXT", nullable: false),
            CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
          },
          constraints: table => {
            table.PrimaryKey("PK_Profiles", x => x.Id);
          });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) {
      migrationBuilder.DropTable(
          name: "Profiles");
    }
  }
}
