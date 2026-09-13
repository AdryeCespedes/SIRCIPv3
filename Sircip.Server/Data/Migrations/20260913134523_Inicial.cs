using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sircip.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NombreUsuario = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ContrasenaHash = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Rol = table.Column<byte>(type: "tinyint", nullable: false),
                    Habilitado = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                    table.CheckConstraint("CK_Usuarios_Rol", "[Rol] IN (1, 2)");
                });

            // Default para las altas manuales en la base, que no tienen por qué indicar
            // Habilitado. No se declara en el modelo de EF: con un default en la base, EF omite
            // del INSERT el valor false de un bool, y un usuario creado deshabilitado quedaría
            // habilitado. Así EF siempre envía el valor explícito.
            migrationBuilder.Sql("ALTER TABLE [Usuarios] ADD CONSTRAINT [DF_Usuarios_Habilitado] DEFAULT CAST(1 AS bit) FOR [Habilitado];");

            migrationBuilder.CreateTable(
                name: "Importaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Periodo = table.Column<int>(type: "int", nullable: false),
                    FechaImportacionUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    Resultado = table.Column<byte>(type: "tinyint", nullable: false),
                    CantidadRegistros = table.Column<int>(type: "int", nullable: true),
                    DetalleError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BajaUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    BajaUsuarioId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Importaciones", x => x.Id);
                    table.CheckConstraint("CK_Importaciones_Resultado", "[Resultado] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_Importaciones_Usuarios_BajaUsuarioId",
                        column: x => x.BajaUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Importaciones_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Sesiones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    RolAlEmitir = table.Column<byte>(type: "tinyint", nullable: false),
                    UltimaActividadUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CerradaUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sesiones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sesiones_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Importaciones_BajaUsuarioId",
                table: "Importaciones",
                column: "BajaUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Importaciones_Periodo_Resultado_BajaUtc",
                table: "Importaciones",
                columns: new[] { "Periodo", "Resultado", "BajaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Importaciones_UsuarioId",
                table: "Importaciones",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Sesiones_TokenHash",
                table: "Sesiones",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sesiones_UsuarioId",
                table: "Sesiones",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_NombreUsuario",
                table: "Usuarios",
                column: "NombreUsuario",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Importaciones");

            migrationBuilder.DropTable(
                name: "Sesiones");

            migrationBuilder.DropTable(
                name: "Usuarios");
        }
    }
}
