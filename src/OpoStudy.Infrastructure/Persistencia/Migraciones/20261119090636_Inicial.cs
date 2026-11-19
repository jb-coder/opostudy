using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpoStudy.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "oposiciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    nombre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    organismo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    grupo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_oposiciones", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "temas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    oposicion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    titulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    bloque = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_temas", x => x.id);
                    table.ForeignKey(
                        name: "fk_temas_oposiciones_oposicion_id",
                        column: x => x.oposicion_id,
                        principalTable: "oposiciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    firebase_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    fecha_alta = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    oposicion_activa_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuarios", x => x.id);
                    table.ForeignKey(
                        name: "fk_usuarios_oposiciones_oposicion_activa_id",
                        column: x => x.oposicion_activa_id,
                        principalTable: "oposiciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "preguntas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tema_id = table.Column<Guid>(type: "uuid", nullable: false),
                    clave = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    enunciado = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    aclaracion = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    referencia_legal = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_preguntas", x => x.id);
                    table.ForeignKey(
                        name: "fk_preguntas_temas_tema_id",
                        column: x => x.tema_id,
                        principalTable: "temas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "intentos_test",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    oposicion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tema_id = table.Column<Guid>(type: "uuid", nullable: true),
                    modo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha_inicio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fecha_fin = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_intentos_test", x => x.id);
                    table.ForeignKey(
                        name: "fk_intentos_test_oposiciones_oposicion_id",
                        column: x => x.oposicion_id,
                        principalTable: "oposiciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_intentos_test_temas_tema_id",
                        column: x => x.tema_id,
                        principalTable: "temas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_intentos_test_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "opciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pregunta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    texto = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    es_correcta = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_opciones", x => x.id);
                    table.ForeignKey(
                        name: "fk_opciones_preguntas_pregunta_id",
                        column: x => x.pregunta_id,
                        principalTable: "preguntas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "respuestas_usuario",
                columns: table => new
                {
                    intento_test_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pregunta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    opcion_elegida_id = table.Column<Guid>(type: "uuid", nullable: true),
                    es_correcta = table.Column<bool>(type: "boolean", nullable: true),
                    respondida_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_respuestas_usuario", x => new { x.intento_test_id, x.pregunta_id });
                    table.ForeignKey(
                        name: "fk_respuestas_usuario_intentos_test_intento_test_id",
                        column: x => x.intento_test_id,
                        principalTable: "intentos_test",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_respuestas_usuario_opciones_opcion_elegida_id",
                        column: x => x.opcion_elegida_id,
                        principalTable: "opciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_respuestas_usuario_preguntas_pregunta_id",
                        column: x => x.pregunta_id,
                        principalTable: "preguntas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_intentos_test_oposicion_id",
                table: "intentos_test",
                column: "oposicion_id");

            migrationBuilder.CreateIndex(
                name: "ix_intentos_test_tema_id",
                table: "intentos_test",
                column: "tema_id");

            migrationBuilder.CreateIndex(
                name: "ix_intentos_test_usuario_id_fecha_inicio",
                table: "intentos_test",
                columns: new[] { "usuario_id", "fecha_inicio" });

            migrationBuilder.CreateIndex(
                name: "ix_opciones_pregunta_id_orden",
                table: "opciones",
                columns: new[] { "pregunta_id", "orden" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_oposiciones_slug",
                table: "oposiciones",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_preguntas_clave",
                table: "preguntas",
                column: "clave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_preguntas_tema_id",
                table: "preguntas",
                column: "tema_id");

            migrationBuilder.CreateIndex(
                name: "ix_respuestas_usuario_intento_test_id_orden",
                table: "respuestas_usuario",
                columns: new[] { "intento_test_id", "orden" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_respuestas_usuario_opcion_elegida_id",
                table: "respuestas_usuario",
                column: "opcion_elegida_id");

            migrationBuilder.CreateIndex(
                name: "ix_respuestas_usuario_pregunta_id",
                table: "respuestas_usuario",
                column: "pregunta_id");

            migrationBuilder.CreateIndex(
                name: "ix_temas_oposicion_id_numero",
                table: "temas",
                columns: new[] { "oposicion_id", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_temas_oposicion_id_slug",
                table: "temas",
                columns: new[] { "oposicion_id", "slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_firebase_uid",
                table: "usuarios",
                column: "firebase_uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_oposicion_activa_id",
                table: "usuarios",
                column: "oposicion_activa_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "respuestas_usuario");

            migrationBuilder.DropTable(
                name: "intentos_test");

            migrationBuilder.DropTable(
                name: "opciones");

            migrationBuilder.DropTable(
                name: "usuarios");

            migrationBuilder.DropTable(
                name: "preguntas");

            migrationBuilder.DropTable(
                name: "temas");

            migrationBuilder.DropTable(
                name: "oposiciones");
        }
    }
}
