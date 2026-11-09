using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PlataformaCursos.Migrations
{
    /// <inheritdoc />
    public partial class Recordatorios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConexionesGoogle",
                columns: table => new
                {
                    UsuarioId = table.Column<string>(type: "text", nullable: false),
                    RefreshTokenCifrado = table.Column<string>(type: "text", nullable: false),
                    Correo = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    FechaConexion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConexionesGoogle", x => x.UsuarioId);
                    table.ForeignKey(
                        name: "FK_ConexionesGoogle_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Recordatorios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<string>(type: "text", nullable: false),
                    CursoId = table.Column<int>(type: "integer", nullable: true),
                    Titulo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Notas = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DuracionMinutos = table.Column<int>(type: "integer", nullable: false),
                    AvisoMinutosAntes = table.Column<int>(type: "integer", nullable: false),
                    Completado = table.Column<bool>(type: "boolean", nullable: false),
                    GoogleEventId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    GoogleEventUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recordatorios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Recordatorios_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Recordatorios_Cursos_CursoId",
                        column: x => x.CursoId,
                        principalTable: "Cursos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Recordatorios_CursoId",
                table: "Recordatorios",
                column: "CursoId");

            migrationBuilder.CreateIndex(
                name: "IX_Recordatorios_UsuarioId_Fecha",
                table: "Recordatorios",
                columns: new[] { "UsuarioId", "Fecha" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConexionesGoogle");

            migrationBuilder.DropTable(
                name: "Recordatorios");
        }
    }
}
