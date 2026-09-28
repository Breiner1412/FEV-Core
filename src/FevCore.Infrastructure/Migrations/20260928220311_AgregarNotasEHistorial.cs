using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FevCore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarNotasEHistorial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DocumentoReferenciadoId",
                table: "documentos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Motivo",
                table: "documentos",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Observaciones",
                table: "documentos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "transiciones_estado",
                columns: table => new
                {
                    Secuencia = table.Column<int>(type: "integer", nullable: false),
                    DocumentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    EstadoAnterior = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    EstadoNuevo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OcurridaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Detalle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transiciones_estado", x => new { x.DocumentoId, x.Secuencia });
                    table.ForeignKey(
                        name: "FK_transiciones_estado_documentos_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "documentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_documentos_referenciado",
                table: "documentos",
                column: "DocumentoReferenciadoId");

            migrationBuilder.AddForeignKey(
                name: "FK_documentos_documentos_DocumentoReferenciadoId",
                table: "documentos",
                column: "DocumentoReferenciadoId",
                principalTable: "documentos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_documentos_documentos_DocumentoReferenciadoId",
                table: "documentos");

            migrationBuilder.DropTable(
                name: "transiciones_estado");

            migrationBuilder.DropIndex(
                name: "ix_documentos_referenciado",
                table: "documentos");

            migrationBuilder.DropColumn(
                name: "DocumentoReferenciadoId",
                table: "documentos");

            migrationBuilder.DropColumn(
                name: "Motivo",
                table: "documentos");

            migrationBuilder.DropColumn(
                name: "Observaciones",
                table: "documentos");
        }
    }
}
