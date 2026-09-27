using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FevCore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarRangosNumeracion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rangos_numeracion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Prefijo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TipoDocumento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NumeroInicial = table.Column<long>(type: "bigint", nullable: false),
                    NumeroFinal = table.Column<long>(type: "bigint", nullable: false),
                    VigenteDesde = table.Column<DateOnly>(type: "date", nullable: false),
                    VigenteHasta = table.Column<DateOnly>(type: "date", nullable: false),
                    UltimoAsignado = table.Column<long>(type: "bigint", nullable: true),
                    NumeroAutorizacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ClaveTecnica = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rangos_numeracion", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_rangos_prefijo_tipo",
                table: "rangos_numeracion",
                columns: new[] { "Prefijo", "TipoDocumento" });

            migrationBuilder.CreateIndex(
                name: "ix_rangos_tipo_vigencia",
                table: "rangos_numeracion",
                columns: new[] { "TipoDocumento", "VigenteDesde", "VigenteHasta" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rangos_numeracion");
        }
    }
}
