using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FevCore.Infrastructure.Migrations
{
    /// <summary>
    /// Los indices de identificacion de adquirentes y de codigo de productos
    /// pasan a ser unicos, solo entre los activos (INV-ADQ-01, RF-07).
    ///
    /// SI ESTA MIGRACION FALLA al aplicarse sobre una base con datos, es
    /// porque ya hay dos adquirentes activos con la misma identificacion, o
    /// dos productos activos con el mismo codigo: los que la comprobacion de
    /// la aplicacion dejo pasar por llegar a la vez. Fallar es el
    /// comportamiento correcto. La alternativa seria crear el indice
    /// saltandose esas filas, y entonces la regla se cumpliria para lo nuevo
    /// y no para lo que ya esta mal.
    ///
    /// Antes de reintentar hay que resolver los duplicados a mano: decidir
    /// cual es el bueno y desactivar el otro. Ver docs/08-despliegue.md.
    /// </summary>
    public partial class IndicesUnicosCatalogos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_productos_codigo",
                table: "productos");

            migrationBuilder.DropIndex(
                name: "ix_adquirentes_identificacion",
                table: "adquirentes");

            migrationBuilder.CreateIndex(
                name: "ix_productos_codigo",
                table: "productos",
                column: "Codigo",
                unique: true,
                filter: "\"Activo\"");

            migrationBuilder.CreateIndex(
                name: "ix_adquirentes_identificacion",
                table: "adquirentes",
                columns: new[] { "Datos_TipoIdentificacion", "Datos_Identificacion" },
                unique: true,
                filter: "\"Activo\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_productos_codigo",
                table: "productos");

            migrationBuilder.DropIndex(
                name: "ix_adquirentes_identificacion",
                table: "adquirentes");

            migrationBuilder.CreateIndex(
                name: "ix_productos_codigo",
                table: "productos",
                column: "Codigo");

            migrationBuilder.CreateIndex(
                name: "ix_adquirentes_identificacion",
                table: "adquirentes",
                columns: new[] { "Datos_TipoIdentificacion", "Datos_Identificacion" });
        }
    }
}
