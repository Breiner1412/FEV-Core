using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FevCore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCatalogos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AdquirenteId",
                table: "documentos",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "AdquirenteSnapshot_Correo",
                table: "documentos",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdquirenteSnapshot_DigitoVerificacion",
                table: "documentos",
                type: "character varying(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdquirenteSnapshot_Direccion",
                table: "documentos",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AdquirenteSnapshot_Identificacion",
                table: "documentos",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AdquirenteSnapshot_MunicipioCodigo",
                table: "documentos",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AdquirenteSnapshot_RazonSocial",
                table: "documentos",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AdquirenteSnapshot_Regimen",
                table: "documentos",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AdquirenteSnapshot_Responsabilidades",
                table: "documentos",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AdquirenteSnapshot_Telefono",
                table: "documentos",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdquirenteSnapshot_TipoIdentificacion",
                table: "documentos",
                type: "character varying(5)",
                maxLength: 5,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EmisorSnapshot_Correo",
                table: "documentos",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmisorSnapshot_DigitoVerificacion",
                table: "documentos",
                type: "character varying(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmisorSnapshot_Direccion",
                table: "documentos",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EmisorSnapshot_Identificacion",
                table: "documentos",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EmisorSnapshot_MunicipioCodigo",
                table: "documentos",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EmisorSnapshot_RazonSocial",
                table: "documentos",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EmisorSnapshot_Regimen",
                table: "documentos",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EmisorSnapshot_Responsabilidades",
                table: "documentos",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EmisorSnapshot_Telefono",
                table: "documentos",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmisorSnapshot_TipoIdentificacion",
                table: "documentos",
                type: "character varying(5)",
                maxLength: 5,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "adquirentes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Datos_TipoIdentificacion = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Datos_Identificacion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Datos_DigitoVerificacion = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    Datos_RazonSocial = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Datos_Direccion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Datos_MunicipioCodigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Datos_Correo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Datos_Telefono = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Datos_Regimen = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Datos_Responsabilidades = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_adquirentes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "emisores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Datos_TipoIdentificacion = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Datos_Identificacion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Datos_DigitoVerificacion = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    Datos_RazonSocial = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Datos_Direccion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Datos_MunicipioCodigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Datos_Correo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Datos_Telefono = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Datos_Regimen = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Datos_Responsabilidades = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NombreComercial = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_emisores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "productos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UnidadMedida = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "impuestos_producto",
                columns: table => new
                {
                    ProductoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Tarifa = table.Column<decimal>(type: "numeric(8,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_impuestos_producto", x => new { x.ProductoId, x.Id });
                    table.ForeignKey(
                        name: "FK_impuestos_producto_productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_adquirentes_identificacion",
                table: "adquirentes",
                columns: new[] { "Datos_TipoIdentificacion", "Datos_Identificacion" });

            migrationBuilder.CreateIndex(
                name: "ix_productos_codigo",
                table: "productos",
                column: "Codigo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "adquirentes");

            migrationBuilder.DropTable(
                name: "emisores");

            migrationBuilder.DropTable(
                name: "impuestos_producto");

            migrationBuilder.DropTable(
                name: "productos");

            migrationBuilder.DropColumn(
                name: "AdquirenteId",
                table: "documentos");

            migrationBuilder.DropColumn(
                name: "AdquirenteSnapshot_Correo",
                table: "documentos");

            migrationBuilder.DropColumn(
                name: "AdquirenteSnapshot_DigitoVerificacion",
                table: "documentos");

            migrationBuilder.DropColumn(
                name: "AdquirenteSnapshot_Direccion",
                table: "documentos");

            migrationBuilder.DropColumn(
                name: "AdquirenteSnapshot_Identificacion",
                table: "documentos");

            migrationBuilder.DropColumn(
                name: "AdquirenteSnapshot_MunicipioCodigo",
                table: "documentos");

            migrationBuilder.DropColumn(
                name: "AdquirenteSnapshot_RazonSocial",
                table: "documentos");

            migrationBuilder.DropColumn(
                name: "AdquirenteSnapshot_Regimen",
                table: "documentos");

            migrationBuilder.DropColumn(
                name: "AdquirenteSnapshot_Responsabilidades",
                table: "documentos");

            migrationBuilder.DropColumn(
                name: "AdquirenteSnapshot_Telefono",
                table: "documentos");

            migrationBuilder.DropColumn(
                name: "AdquirenteSnapshot_TipoIdentificacion",
                table: "documentos");

            migrationBuilder.DropColumn(
                name: "EmisorSnapshot_Correo",
                table: "documentos");

            migrationBuilder.DropColumn(
                name: "EmisorSnapshot_DigitoVerificacion",
                table: "documentos");

            migrationBuilder.DropColumn(
                name: "EmisorSnapshot_Direccion",
                table: "documentos");

            migrationBuilder.DropColumn(
                name: "EmisorSnapshot_Identificacion",
                table: "documentos");

            migrationBuilder.DropColumn(
                name: "EmisorSnapshot_MunicipioCodigo",
                table: "documentos");

            migrationBuilder.DropColumn(
                name: "EmisorSnapshot_RazonSocial",
                table: "documentos");

            migrationBuilder.DropColumn(
                name: "EmisorSnapshot_Regimen",
                table: "documentos");

            migrationBuilder.DropColumn(
                name: "EmisorSnapshot_Responsabilidades",
                table: "documentos");

            migrationBuilder.DropColumn(
                name: "EmisorSnapshot_Telefono",
                table: "documentos");

            migrationBuilder.DropColumn(
                name: "EmisorSnapshot_TipoIdentificacion",
                table: "documentos");
        }
    }
}
