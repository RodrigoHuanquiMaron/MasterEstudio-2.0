using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PlataformaCursos.Migrations
{
    /// <inheritdoc />
    public partial class RedisenoFigma : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Nota",
                table: "Inscripciones",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(4,2)",
                oldPrecision: 4,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCalificacion",
                table: "Inscripciones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Anonima",
                table: "Donaciones",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Sentimiento",
                table: "Donaciones",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SentimientoConfianza",
                table: "Donaciones",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Categoria",
                table: "Cursos",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Creditos",
                table: "Cursos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ImagenUrl",
                table: "Cursos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Instructor",
                table: "Cursos",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Carrera",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ciclo",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SesionesEstudio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<string>(type: "text", nullable: false),
                    CursoId = table.Column<int>(type: "integer", nullable: false),
                    Inicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Segundos = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SesionesEstudio", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SesionesEstudio_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SesionesEstudio_Cursos_CursoId",
                        column: x => x.CursoId,
                        principalTable: "Cursos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SesionesEstudio_CursoId",
                table: "SesionesEstudio",
                column: "CursoId");

            migrationBuilder.CreateIndex(
                name: "IX_SesionesEstudio_UsuarioId_Inicio",
                table: "SesionesEstudio",
                columns: new[] { "UsuarioId", "Inicio" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SesionesEstudio");

            migrationBuilder.DropColumn(
                name: "FechaCalificacion",
                table: "Inscripciones");

            migrationBuilder.DropColumn(
                name: "Anonima",
                table: "Donaciones");

            migrationBuilder.DropColumn(
                name: "Sentimiento",
                table: "Donaciones");

            migrationBuilder.DropColumn(
                name: "SentimientoConfianza",
                table: "Donaciones");

            migrationBuilder.DropColumn(
                name: "Categoria",
                table: "Cursos");

            migrationBuilder.DropColumn(
                name: "Creditos",
                table: "Cursos");

            migrationBuilder.DropColumn(
                name: "ImagenUrl",
                table: "Cursos");

            migrationBuilder.DropColumn(
                name: "Instructor",
                table: "Cursos");

            migrationBuilder.DropColumn(
                name: "Carrera",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Ciclo",
                table: "AspNetUsers");

            migrationBuilder.AlterColumn<decimal>(
                name: "Nota",
                table: "Inscripciones",
                type: "numeric(4,2)",
                precision: 4,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(5,2)",
                oldPrecision: 5,
                oldScale: 2,
                oldNullable: true);
        }
    }
}
