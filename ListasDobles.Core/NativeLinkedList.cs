namespace ListasDobles.Core;

// ============================================================================
// CLASE: NativeLinkedList<T>
// ----------------------------------------------------------------------------
// Es un "adaptador" (envoltorio): por dentro usa LinkedList<T>, la lista
// doblemente enlazada OFICIAL de .NET, pero le pone el "enchufe" ILista<T>
// para que el programa la use igual que a las otras dos.
//
// Es la misma idea que CustomLinkedList<T> (nodos con Next y Previous) pero
// fabricada y probada por Microsoft. Sirve como "vara de medir" para
// comparar contra nuestra versión hecha a mano.
// ============================================================================
public sealed class NativeLinkedList<T> : ILista<T> where T : class
{
    // La lista oficial de .NET que hace el trabajo real.
    private readonly LinkedList<T> _lista = new();

    public string Nombre => "LinkedList<T>";
    public int Count => _lista.Count;

    // AddLast = "agregar al final" (equivale a nuestro Agregar).
    public void Agregar(T elemento) => _lista.AddLast(elemento);
    public void Limpiar() => _lista.Clear();

    // Recorrer adelante: se parte del primer nodo y se sigue .Next hasta null.
    // (Igual que en nuestra lista, para que la comparación sea equivalente.)
    public IEnumerable<T> Adelante()
    {
        for (var n = _lista.First; n is not null; n = n.Next)
            yield return n.Value;
    }

    // Recorrer atrás: se parte del último nodo y se sigue .Previous.
    public IEnumerable<T> Atras()
    {
        for (var n = _lista.Last; n is not null; n = n.Previous)
            yield return n.Value;
    }

    // Busca el NODO (no solo el dato) que cumpla la condición, caminando uno
    // por uno desde el principio.
    private LinkedListNode<T>? BuscarNodo(Predicate<T> condicion)
    {
        for (var n = _lista.First; n is not null; n = n.Next)
            if (condicion(n.Value)) return n;
        return null;
    }

    public T? Buscar(Predicate<T> condicion) => BuscarNodo(condicion)?.Value;

    // Eliminar: buscar el nodo y pedirle a la lista oficial que lo retire
    // (ella misma arregla las manos Next/Previous de los vecinos).
    public bool Eliminar(Predicate<T> condicion)
    {
        if (BuscarNodo(condicion) is not { } nodo) return false;
        _lista.Remove(nodo);
        return true;
    }

    // Modificar: buscar el nodo y reemplazar el dato que guarda.
    public bool Modificar(Predicate<T> condicion, Func<T, T> transformar)
    {
        if (BuscarNodo(condicion) is not { } nodo) return false;
        nodo.Value = transformar(nodo.Value);
        return true;
    }

    // Modificar masivo: una caminata por todos los nodos.
    public int ModificarDonde(Predicate<T> condicion, Func<T, T> transformar)
    {
        var total = 0;
        for (var n = _lista.First; n is not null; n = n.Next)
        {
            if (!condicion(n.Value)) continue;
            n.Value = transformar(n.Value);
            total++;
        }
        return total;
    }

    // Eliminar masivo: igual que en nuestra lista, se anota el "siguiente"
    // ANTES de quitar el nodo actual (si no, se perdería el hilo del recorrido).
    public int EliminarDonde(Predicate<T> condicion)
    {
        var total = 0;
        var n = _lista.First;
        while (n is not null)
        {
            var siguiente = n.Next;
            if (condicion(n.Value))
            {
                _lista.Remove(n);
                total++;
            }
            n = siguiente;
        }
        return total;
    }

    // ORDENAR: LinkedList<T> no trae un método para ordenarse.
    // Estrategia: 1) copiar los datos a un arreglo  2) ordenar el arreglo con
    // Array.Sort (rápido, hecho por Microsoft)  3) volver a escribir los
    // datos ordenados dentro de los MISMOS nodos, de principio a fin.
    public void Ordenar(Comparison<T> comparacion)
    {
        if (_lista.Count < 2) return;

        var arreglo = new T[_lista.Count];
        _lista.CopyTo(arreglo, 0);          // Paso 1
        Array.Sort(arreglo, comparacion);   // Paso 2

        var i = 0;
        for (var n = _lista.First; n is not null; n = n.Next)   // Paso 3
            n.Value = arreglo[i++];
    }
}
