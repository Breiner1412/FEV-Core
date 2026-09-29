using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FevCore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarBandejaDeSalida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ErroresValidacion",
                table: "documentos",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "tareas_salida",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreadaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Intentos = table.Column<int>(type: "integer", nullable: false),
                    ProximoIntentoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TomadaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletadaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UltimoError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tareas_salida", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tareas_salida_documentos_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "documentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transmisiones",
                columns: table => new
                {
                    NumeroIntento = table.Column<int>(type: "integer", nullable: false),
                    DocumentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnviadaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IdentificadorSeguimiento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Resultado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RespuestaCruda = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transmisiones", x => new { x.DocumentoId, x.NumeroIntento });
                    table.ForeignKey(
                        name: "FK_transmisiones_documentos_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "documentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tareas_documento_tipo",
                table: "tareas_salida",
                columns: new[] { "DocumentoId", "Tipo" });

            migrationBuilder.CreateIndex(
                name: "ix_tareas_pendientes",
                table: "tareas_salida",
                column: "ProximoIntentoEn",
                filter: "\"CompletadaEn\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tareas_salida");

            migrationBuilder.DropTable(
                name: "transmisiones");

            migrationBuilder.DropColumn(
                name: "ErroresValidacion",
                table: "documentos");
        }
    }
}
