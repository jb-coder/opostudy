using OpoStudy.Domain.Comun;

namespace OpoStudy.Domain.Oposiciones;

public sealed class Oposicion
{
    private Oposicion()
    {
        Slug = Nombre = Organismo = Grupo = string.Empty;
    }

    public Guid Id { get; private set; }
    public string Slug { get; private set; }
    public string Nombre { get; private set; }
    public string Organismo { get; private set; }
    public string Grupo { get; private set; }
    public bool Activa { get; private set; }

    public static Oposicion Crear(string slug, string nombre, string organismo, string grupo)
    {
        var oposicion = new Oposicion
        {
            Id = Guid.CreateVersion7(),
            Slug = Regla.Slug(slug, "oposicion.slug_invalido"),
            Activa = true,
        };
        oposicion.Actualizar(nombre, organismo, grupo);
        return oposicion;
    }

    public void Actualizar(string nombre, string organismo, string grupo)
    {
        Nombre = Regla.TextoObligatorio(nombre, Limites.Nombre, "oposicion.nombre_invalido");
        Organismo = Regla.TextoObligatorio(organismo, Limites.Nombre, "oposicion.organismo_invalido");
        Grupo = Regla.TextoObligatorio(grupo, Limites.Grupo, "oposicion.grupo_invalido");
    }

    public void Activar() => Activa = true;

    public void Desactivar() => Activa = false;
}