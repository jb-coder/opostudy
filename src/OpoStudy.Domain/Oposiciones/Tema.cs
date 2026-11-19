using OpoStudy.Domain.Comun;

namespace OpoStudy.Domain.Oposiciones;

public sealed class Tema
{
    private Tema()
    {
        Slug = Titulo = string.Empty;
    }

    public Guid Id { get; private set; }
    public Guid OposicionId { get; private set; }
    public string Slug { get; private set; }
    public int Numero { get; private set; }
    public string Titulo { get; private set; }
    public string? Bloque { get; private set; }

    public static Tema Crear(Guid oposicionId, string slug, int numero, string titulo, string? bloque)
    {
        var tema = new Tema
        {
            Id = Guid.CreateVersion7(),
            OposicionId = oposicionId,
            Slug = Regla.Slug(slug, "tema.slug_invalido"),
        };
        tema.Actualizar(numero, titulo, bloque);
        return tema;
    }

    public void Actualizar(int numero, string titulo, string? bloque)
    {
        if (numero <= 0)
        {
            throw new ReglaDominioException("tema.numero_invalido", "El número de tema debe ser mayor que 0.");
        }

        Numero = numero;
        Titulo = Regla.TextoObligatorio(titulo, Limites.Nombre, "tema.titulo_invalido");
        Bloque = Regla.TextoOpcional(bloque, Limites.Nombre, "tema.bloque_invalido");
    }
}