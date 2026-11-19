using OpoStudy.Domain.Comun;

namespace OpoStudy.Domain.Usuarios;

public sealed class Usuario
{
    private Usuario()
    {
        FirebaseUid = string.Empty;
    }

    public Guid Id { get; private set; }

    /// <summary>Identificador del usuario en Firebase Auth. Es un dato personal: no debe aparecer en logs.</summary>
    public string FirebaseUid { get; private set; }

    public DateTimeOffset FechaAlta { get; private set; }
    public Guid? OposicionActivaId { get; private set; }

    public static Usuario Crear(string firebaseUid, DateTimeOffset ahora) => new()
    {
        Id = Guid.CreateVersion7(),
        FirebaseUid = Regla.TextoObligatorio(firebaseUid, Limites.FirebaseUid, "usuario.firebase_uid_invalido"),
        FechaAlta = ahora.ToUniversalTime(),
    };

    public void ElegirOposicion(Guid oposicionId) => OposicionActivaId = oposicionId;
}