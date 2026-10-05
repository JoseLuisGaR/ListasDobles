namespace ListasDobles.Core;

// ============================================================================
// CLASE: NativeList<T>
// ----------------------------------------------------------------------------
// Adaptador de List<T>, la lista "dinámica" estándar de .NET.
//
// Analogía: una fila de asientos numerados en un cine. Todos están JUNTOS y
// en orden, así que ir al asiento 50 es inmediato. Si la fila se llena, el
// cine se muda a una sala más grande (normalmente del doble de tamaño) y
// copia a todos. Por dentro List<T> usa un ARREGLO: un bloque de memoria
// continuo, sin huecos y sin nodos ni punteros Next/Previous.
//
// Aquí NO hay "eslabones": se trabaja con posiciones (índices 0, 1, 2...).
// ============================================================================
public sealed class NativeList<T> : ILista<T> where T : class
{
    private readonly List<T> _lista = new();

    public string Nombre => "List<T>";
    public int Count => _lista.Count;

    // Add = poner en el siguiente asiento libre (si no hay, la lista se
    // agranda copiando todo a un arreglo mayor; pasa pocas veces).
    public void Agregar(T elemento) => _lista.Add(elemento);
    public void Limpiar() => _lista.Clear();

    // Recorrer adelante: posición 0, 1, 2 ... hasta la última.
    public IEnumerable<T> Adelante()
    {
        for (var i = 0; i < _lista.Count; i++)
            yield return _lista[i];
    }

    // Recorrer atrás: desde la última posición hasta la 0.
    public IEnumerable<T> Atras()
    {
        for (var i = _lista.Count - 1; i >= 0; i--)
            yield return _lista[i];
    }

    // Busca la POSICIÓN del primer elemento que cumpla la condición
    // (o -1 si no existe). Se revisa uno por uno, igual que en las otras listas.
    private int BuscarIndice(Predicate<T> condicion)
    {
        for (var i = 0; i < _lista.Count; i++)
            if (condicion(_lista[i])) return i;
        return -1;
    }

    public T? Buscar(Predicate<T> condicion)
    {
        var i = BuscarIndice(condicion);
        return i < 0 ? null : _lista[i];
    }

    // Eliminar: buscar la posición y usar RemoveAt. OJO: al quitar a alguien
    // de la fila, TODOS los de atrás deben correrse un asiento. Por eso
    // eliminar en el medio de un arreglo es más costoso que en una lista enlazada.
    public bool Eliminar(Predicate<T> condicion)
    {
        var i = BuscarIndice(condicion);
        if (i < 0) return false;
        _lista.RemoveAt(i);
        return true;
    }

    // Modificar: buscar la posición y reemplazar lo que hay en ese asiento.
    public bool Modificar(Predicate<T> condicion, Func<T, T> transformar)
    {
        var i = BuscarIndice(condicion);
        if (i < 0) return false;
        _lista[i] = transformar(_lista[i]);
        return true;
    }

    // Modificar masivo: se recorren todas las posiciones y se reemplazan las
    // que cumplan la condición.
    public int ModificarDonde(Predicate<T> condicion, Func<T, T> transformar)
    {
        var total = 0;
        for (var i = 0; i < _lista.Count; i++)
        {
            if (!condicion(_lista[i])) continue;
            _lista[i] = transformar(_lista[i]);
            total++;
        }
        return total;
    }

    // Eliminar masivo: RemoveAll es la forma oficial y eficiente de borrar
    // muchos elementos: recorre UNA vez y va compactando el arreglo. Quitar
    // uno por uno con RemoveAt sería muy lento (mucho desplazamiento).
    public int EliminarDonde(Predicate<T> condicion) => _lista.RemoveAll(condicion);

    // Ordenar: List<T> ya trae su propio Sort que ordena "en sitio",
    // sin crear otra lista.
    public void Ordenar(Comparison<T> comparacion) => _lista.Sort(comparacion);
}
