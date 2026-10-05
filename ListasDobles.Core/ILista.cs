namespace ListasDobles.Core;

// ============================================================================
// INTERFAZ: ILista<T>
// ----------------------------------------------------------------------------
// Una interfaz es un "contrato": una lista de operaciones que toda clase que
// lo firme está OBLIGADA a ofrecer. No dice CÓMO se hacen, solo QUÉ se puede
// hacer (como el menú de un restaurante: dice los platos, no la receta).
//
// Las tres estructuras que comparamos firman este contrato:
//   1) CustomLinkedList<T>  -> lista doblemente enlazada hecha por nosotros.
//   2) NativeLinkedList<T>  -> envoltorio de LinkedList<T> (la de .NET).
//   3) NativeList<T>        -> envoltorio de List<T> (arreglo dinámico de .NET).
//
// Gracias a este contrato, el resto del programa (menús, pantalla, benchmark)
// trata a las tres listas EXACTAMENTE igual. Eso hace justa la comparación.
//
// "T" es un comodín: significa "el tipo de dato que guarde la lista" (aquí,
// Registro). "where T : class" significa que T debe ser una clase.
// ============================================================================
public interface ILista<T> where T : class
{
    // Nombre para mostrar en pantallas y reportes.
    string Nombre { get; }

    // Cuántos elementos hay guardados ahora mismo.
    int Count { get; }

    // Pone un elemento AL FINAL de la lista.
    void Agregar(T elemento);

    // Vacía la lista por completo.
    void Limpiar();

    // Entrega los elementos del primero al último / del último al primero.
    IEnumerable<T> Adelante();
    IEnumerable<T> Atras();

    // Un "Predicate<T>" es una pregunta de sí/no sobre un elemento,
    // por ejemplo: "¿este registro tiene Id = 5?".

    // Devuelve el primer elemento que cumpla la pregunta (o null si no hay).
    T? Buscar(Predicate<T> condicion);

    // Elimina el primer elemento que cumpla la pregunta. Devuelve true si lo logró.
    bool Eliminar(Predicate<T> condicion);

    // Busca el primer elemento que cumpla la pregunta y lo reemplaza por la
    // versión que devuelva "transformar" (una "receta" que recibe el viejo y
    // entrega el nuevo).
    bool Modificar(Predicate<T> condicion, Func<T, T> transformar);

    // Versiones MASIVAS: actúan sobre TODOS los elementos que cumplan la
    // pregunta, en una sola pasada. Devuelven cuántos fueron afectados.
    int ModificarDonde(Predicate<T> condicion, Func<T, T> transformar);
    int EliminarDonde(Predicate<T> condicion);

    // Ordena la lista. "Comparison<T>" es una regla que dice, dados dos
    // elementos, cuál va primero.
    void Ordenar(Comparison<T> comparacion);
}
