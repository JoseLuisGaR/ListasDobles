namespace ListasDobles.Core;

// ============================================================================
// CLASE: ProgresoDirecto<T>
// ----------------------------------------------------------------------------
// Un "mensajero" muy sencillo: cuando el benchmark quiere avisar "voy en la
// ronda 2...", llama a Report y este mensaje se entrega INMEDIATAMENTE a la
// función que le pasamos (en la consola, Console.WriteLine).
// Se usa en lugar de Progress<T> porque este último entrega los mensajes un
// poco después y, en la consola, aparecerían desordenados.
// ============================================================================
public sealed class ProgresoDirecto<T>(Action<T> accion) : IProgress<T>
{
    public void Report(T value) => accion(value);
}
