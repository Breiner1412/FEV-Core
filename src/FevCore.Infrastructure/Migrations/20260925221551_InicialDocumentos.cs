using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FevCore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InicialDocumentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "documentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReferenciaExterna = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IntegradorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Prefijo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Consecutivo = table.Column<long>(type: "bigint", nullable: false),
                    FechaEmision = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Moneda = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    TotalBruto = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    TotalDescuentos = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    TotalBaseImponible = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    TotalImpuestos = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    TotalAPagar = table.Column<decimal>(type: "numeric(18,6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documentos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "lineas",
                columns: table => new
                {
                    DocumentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    ProductoId = table.Column<Guid>(type: "uuid", nullable: true),
                    Codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UnidadMedida = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Cantidad = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    Descuento = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    BaseGravable = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lineas", x => new { x.DocumentoId, x.Id });
                    table.ForeignKey(
                        name: "FK_lineas_documentos_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "documentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "impuestos_linea",
                columns: table => new
                {
                    LineaDocumentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineaId = table.Column<int>(type: "integer", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Tarifa = table.Column<decimal>(type: "numeric(8,4)", nullable: false),
                    BaseGravable = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(18,6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_impuestos_linea", x => new { x.LineaDocumentoId, x.LineaId, x.Id });
                    table.ForeignKey(
                        name: "FK_impuestos_linea_lineas_LineaDocumentoId_LineaId",
                        columns: x => new { x.LineaDocumentoId, x.LineaId },
                        principalTable: "lineas",
                        principalColumns: new[] { "DocumentoId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_documentos_integrador_referencia",
                table: "documentos",
                columns: new[] { "IntegradorId", "ReferenciaExterna" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_documentos_prefijo_consecutivo",
                table: "documentos",
                columns: new[] { "Prefijo", "Consecutivo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "impuestos_linea");

            migrationBuilder.DropTable(
                name: "lineas");

            migrationBuilder.DropTable(
                name: "documentos");
        }
    }
}
