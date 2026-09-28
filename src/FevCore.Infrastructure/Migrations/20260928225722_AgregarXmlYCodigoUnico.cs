using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FevCore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarXmlYCodigoUnico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodigoUnico",
                table: "documentos",
                type: "character varying(96)",
                maxLength: 96,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Xml",
                table: "documentos",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CodigoUnico",
                table: "documentos");

            migrationBuilder.DropColumn(
                name: "Xml",
                table: "documentos");
        }
    }
}
