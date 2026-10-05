namespace ListasDobles.Core;

// ============================================================================
// CLASE: Registro
// ----------------------------------------------------------------------------
// ¿Qué es? Una "fila" de nuestra tabla de datos: una persona con su Id,
// nombre, correo, ciudad y un monto de dinero.
//
// ¿Qué papel juega? Es el DATO que viaja dentro de las tres listas. Piense en
// ella como la "tarjeta de un cliente"; las listas son los "archiveros" donde
// guardamos esas tarjetas de distintas maneras.
//
// ¿Por qué es un "record"? Un record es un tipo pensado para guardar datos.
// Es INMUTABLE: una vez creado no se edita; si queremos "cambiarlo" se crea
// una copia nueva con los cambios (usando la palabra "with").
// ============================================================================
public sealed record Registro(int Id, string Nombre, string Correo, string Ciudad, decimal Monto)
{
    // Primera línea que lleva todo archivo CSV: los nombres de las columnas.
    public const string Encabezado = "Id,Nombre,Correo,Ciudad,Monto";

    // Cómo se ve el registro cuando lo imprimimos en la consola:
    // cada campo se acomoda en una columna de ancho fijo para que quede alineado.
    public override string ToString() =>
        $"{Id,9} | {Nombre,-24} | {Correo,-34} | {Ciudad,-16} | {Monto,12:N2}";
}
