using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Anywhere.Models.Migrations {
  /// <inheritdoc />
  public partial class MakeProfileWorkingDirOptional : Migration {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) {
      migrationBuilder.AlterColumn<string>(
          name: "WorkingDir",
          table: "Profiles",
          type: "TEXT",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "TEXT");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) {
      migrationBuilder.AlterColumn<string>(
          name: "WorkingDir",
          table: "Profiles",
          type: "TEXT",
          nullable: false,
          defaultValue: "",
          oldClrType: typeof(string),
          oldType: "TEXT",
          oldNullable: true);
    }
  }
}
