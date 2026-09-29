using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FevCore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarXmlFirmado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "XmlFirmado",
                table: "documentos",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "XmlFirmado",
                table: "documentos");
        }
    }
}
