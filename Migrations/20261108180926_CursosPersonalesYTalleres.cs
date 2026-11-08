using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PlataformaCursos.Migrations
{
    /// <inheritdoc />
    public partial class CursosPersonalesYTalleres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Plataforma",
                table: "Cursos",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PropietarioId",
                table: "Cursos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UrlExterna",
                table: "Cursos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Talleres",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Titulo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    Facilitador = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Categoria = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Modalidad = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Lugar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DuracionMinutos = table.Column<int>(type: "integer", nullable: false),
                    Cupos = table.Column<int>(type: "integer", nullable: false),
                    ImagenUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Talleres", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InscripcionesTaller",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<string>(type: "text", nullable: false),
                    TallerId = table.Column<int>(type: "integer", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InscripcionesTaller", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InscripcionesTaller_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InscripcionesTaller_Talleres_TallerId",
                        column: x => x.TallerId,
                        principalTable: "Talleres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cursos_PropietarioId",
                table: "Cursos",
                column: "PropietarioId");

            migrationBuilder.CreateIndex(
                name: "IX_InscripcionesTaller_TallerId",
                table: "InscripcionesTaller",
                column: "TallerId");

            migrationBuilder.CreateIndex(
                name: "IX_InscripcionesTaller_UsuarioId_TallerId",
                table: "InscripcionesTaller",
                columns: new[] { "UsuarioId", "TallerId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Cursos_AspNetUsers_PropietarioId",
                table: "Cursos",
                column: "PropietarioId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cursos_AspNetUsers_PropietarioId",
                table: "Cursos");

            migrationBuilder.DropTable(
                name: "InscripcionesTaller");

            migrationBuilder.DropTable(
                name: "Talleres");

            migrationBuilder.DropIndex(
                name: "IX_Cursos_PropietarioId",
                table: "Cursos");

            migrationBuilder.DropColumn(
                name: "Plataforma",
                table: "Cursos");

            migrationBuilder.DropColumn(
                name: "PropietarioId",
                table: "Cursos");

            migrationBuilder.DropColumn(
                name: "UrlExterna",
                table: "Cursos");
        }
    }
}
