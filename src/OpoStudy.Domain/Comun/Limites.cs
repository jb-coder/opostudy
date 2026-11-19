namespace OpoStudy.Domain.Comun;

/// <summary>Longitudes máximas de los textos. El dominio las valida y la persistencia las usa para las columnas.</summary>
public static class Limites
{
    public const int Slug = 100;
    public const int Nombre = 300;
    public const int Grupo = 20;
    public const int Enunciado = 2000;
    public const int TextoOpcion = 1000;
    public const int Aclaracion = 4000;
    public const int ReferenciaLegal = 300;
    public const int Clave = 150;
    public const int FirebaseUid = 128;

    public const int MinOpciones = 2;
    public const int MaxOpciones = 6;
    public const int MaxPreguntasPorTest = 100;
}