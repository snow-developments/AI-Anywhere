using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Anywhere.Models.Migrations {
  /// <inheritdoc />
  public partial class AddSessionsAndMessages : Migration {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) {
      migrationBuilder.CreateTable(
          name: "Messages",
          columns: table => new {
            Id = table.Column<int>(type: "INTEGER", nullable: false)
                  .Annotation("Sqlite:Autoincrement", true),
            SessionId = table.Column<int>(type: "INTEGER", nullable: false),
            Role = table.Column<string>(type: "TEXT", nullable: false),
            Content = table.Column<string>(type: "TEXT", nullable: false),
            ToolCallJson = table.Column<string>(type: "TEXT", nullable: true),
            CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
          },
          constraints: table => {
            table.PrimaryKey("PK_Messages", x => x.Id);
          });

      migrationBuilder.CreateTable(
          name: "Sessions",
          columns: table => new {
            Id = table.Column<int>(type: "INTEGER", nullable: false)
                  .Annotation("Sqlite:Autoincrement", true),
            ProfileId = table.Column<int>(type: "INTEGER", nullable: false),
            WorkingDir = table.Column<string>(type: "TEXT", nullable: false),
            CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
          },
          constraints: table => {
            table.PrimaryKey("PK_Sessions", x => x.Id);
          });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) {
      migrationBuilder.DropTable(
          name: "Messages");

      migrationBuilder.DropTable(
          name: "Sessions");
    }
  }
}
