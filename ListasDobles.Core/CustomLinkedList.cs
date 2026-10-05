namespace ListasDobles.Core;

// ============================================================================
// CLASE: CustomNode<T>   (el "nodo" o eslabón)
// ----------------------------------------------------------------------------
// Imagine un eslabón de cadena: guarda UN dato (Value) y tiene dos "manos":
//   - Next     -> la mano que agarra al eslabón de ATRÁS (el siguiente).
//   - Previous -> la mano que agarra al eslabón de ADELANTE (el anterior).
// Una "referencia" es simplemente la dirección de otro objeto en la memoria;
// cuando una mano vale null significa "no estoy agarrando a nadie".
// ============================================================================
public sealed class CustomNode<T>(T value)
{
    public T Value { get; set; } = value;          // El dato guardado en el eslabón.
    public CustomNode<T>? Next { get; set; }       // Quién va después de mí (null = soy el último).
    public CustomNode<T>? Previous { get; set; }   // Quién va antes de mí (null = soy el primero).
}

// ============================================================================
// CLASE: CustomLinkedList<T>   (nuestra lista doblemente enlazada)
// ----------------------------------------------------------------------------
// Es una CADENA de eslabones (nodos). La lista solo recuerda dos cosas:
//   _head = el primer eslabón (la "cabeza")
//   _tail = el último eslabón (la "cola")
// Para llegar a cualquier otro hay que caminar eslabón por eslabón.
//
//   null <- [A] <-> [B] <-> [C] -> null
//            ^                ^
//          _head            _tail
//
// "Doblemente" enlazada = se puede caminar en los dos sentidos.
// La escribimos desde cero para entender cómo funciona por dentro y
// compararla con la versión oficial de .NET (LinkedList<T>).
// ============================================================================
public sealed class CustomLinkedList<T> : ILista<T> where T : class
{
    private CustomNode<T>? _head;   // Primer eslabón (null si la lista está vacía).
    private CustomNode<T>? _tail;   // Último eslabón (null si la lista está vacía).

    public string Nombre => "CustomLinkedList<T>";

    // Contador de elementos. Lo mantenemos al día en cada alta y baja,
    // así no hay que recorrer la cadena para saber cuántos hay.
    public int Count { get; private set; }

    // ------------------------------------------------------------------
    // AGREGAR (al final)
    // Problema: poner un dato nuevo al final de la cadena.
    // Pasos:
    //   1) Se crea un eslabón nuevo; su mano "Previous" agarra al antiguo último.
    //   2) Si la lista estaba vacía, el nuevo es a la vez cabeza y cola.
    //      Si no, el antiguo último estira su mano "Next" hacia el nuevo.
    //   3) El nuevo pasa a ser la cola.
    // Solo se tocan 2 eslabones, sin importar cuántos haya: es MUY rápido.
    //
    //   antes:  [A] <-> [B]            _tail = B
    //   ahora:  [A] <-> [B] <-> [N]    _tail = N
    // ------------------------------------------------------------------
    public void Agregar(T elemento)
    {
        var nodo = new CustomNode<T>(elemento) { Previous = _tail };   // Paso 1
        if (_tail is null) _head = nodo;                               // Paso 2 (lista vacía)
        else _tail.Next = nodo;                                        // Paso 2 (el último agarra al nuevo)
        _tail = nodo;                                                  // Paso 3
        Count++;
    }

    // LIMPIAR: basta con "soltar" cabeza y cola; los eslabones quedan sin
    // dueño y el recolector de basura (GC) se encarga de liberarlos.
    public void Limpiar()
    {
        _head = _tail = null;
        Count = 0;
    }

    // ------------------------------------------------------------------
    // RECORRER ADELANTE: se empieza en la cabeza y se sigue la mano "Next"
    // hasta llegar a null (el final). "yield return" entrega un dato a la vez,
    // como un guía que va mostrando cada eslabón sin armar otra lista.
    // ------------------------------------------------------------------
    public IEnumerable<T> Adelante()
    {
        for (var n = _head; n is not null; n = n.Next)
            yield return n.Value;
    }

    // RECORRER ATRÁS: lo mismo, pero desde la cola siguiendo "Previous".
    // Esta es la gracia de ser DOBLEMENTE enlazada.
    public IEnumerable<T> Atras()
    {
        for (var n = _tail; n is not null; n = n.Previous)
            yield return n.Value;
    }

    // BUSCAR (privado): camina desde la cabeza preguntando a cada eslabón
    // "¿cumples la condición?". Devuelve el ESLABÓN (no solo el dato) porque
    // quien llama a veces lo necesita para quitarlo o cambiarlo.
    // Es lento a propósito: no hay atajos, se revisa uno por uno.
    private CustomNode<T>? BuscarNodo(Predicate<T> condicion)
    {
        for (var n = _head; n is not null; n = n.Next)
            if (condicion(n.Value)) return n;
        return null;
    }

    // ------------------------------------------------------------------
    // DESENLAZAR (sacar un eslabón de la cadena)
    // Problema: quitar un eslabón del medio sin romper la cadena.
    // Idea: el vecino de ATRÁS y el de ADELANTE se dan la mano directamente,
    // "puenteando" al eslabón que se va.
    //
    //   antes:  [A] <-> [X] <-> [C]       (quitamos X)
    //   ahora:  [A] <-------> [C]         X queda suelto
    //
    // Pasos:
    //   1) Quien está antes de X ahora apunta (Next) al que está después de X.
    //      (Si X era la cabeza, la nueva cabeza es el que sigue.)
    //   2) Quien está después de X ahora apunta (Previous) al que estaba antes.
    //      (Si X era la cola, la nueva cola es el anterior.)
    //   3) X suelta sus dos manos (null) y se descuenta 1 al contador.
    // ------------------------------------------------------------------
    private void Desenlazar(CustomNode<T> nodo)
    {
        // Paso 1: arreglar el lado "de adelante".
        if (nodo.Previous is null) _head = nodo.Next;
        else nodo.Previous.Next = nodo.Next;

        // Paso 2: arreglar el lado "de atrás".
        if (nodo.Next is null) _tail = nodo.Previous;
        else nodo.Next.Previous = nodo.Previous;

        // Paso 3: el eslabón retirado ya no agarra a nadie.
        nodo.Next = null;
        nodo.Previous = null;
        Count--;
    }

    // BUSCAR (público): igual que BuscarNodo pero devuelve solo el dato.
    public T? Buscar(Predicate<T> condicion) => BuscarNodo(condicion)?.Value;

    // ELIMINAR: 1) buscar el eslabón (lento, uno por uno)
    //           2) sacarlo con Desenlazar (rápido, solo cambia manos).
    public bool Eliminar(Predicate<T> condicion)
    {
        if (BuscarNodo(condicion) is not { } nodo) return false;   // No existe.
        Desenlazar(nodo);
        return true;
    }

    // MODIFICAR: 1) buscar el eslabón  2) cambiar el dato que guarda por el
    // que entregue "transformar". La cadena no se toca, solo el contenido.
    public bool Modificar(Predicate<T> condicion, Func<T, T> transformar)
    {
        if (BuscarNodo(condicion) is not { } nodo) return false;
        nodo.Value = transformar(nodo.Value);
        return true;
    }

    // MODIFICAR MASIVO: una sola caminata por toda la cadena; en cada eslabón
    // que cumpla la condición se cambia el dato. Devuelve cuántos cambió.
    public int ModificarDonde(Predicate<T> condicion, Func<T, T> transformar)
    {
        var total = 0;
        for (var n = _head; n is not null; n = n.Next)
        {
            if (!condicion(n.Value)) continue;
            n.Value = transformar(n.Value);
            total++;
        }
        return total;
    }

    // ELIMINAR MASIVO: una sola caminata. TRUCO IMPORTANTE: antes de quitar un
    // eslabón guardamos quién es su siguiente ("siguiente"), porque al
    // desenlazarlo sus manos quedan en null y ya no sabríamos por dónde seguir.
    public int EliminarDonde(Predicate<T> condicion)
    {
        var total = 0;
        var n = _head;
        while (n is not null)
        {
            var siguiente = n.Next;          // Anotamos a dónde seguir ANTES de tocar nada.
            if (condicion(n.Value))
            {
                Desenlazar(n);
                total++;
            }
            n = siguiente;
        }
        return total;
    }

    // ==================================================================
    // ORDENAMIENTO: MERGE SORT ("ordenar por mezcla")
    // ------------------------------------------------------------------
    // Analogía: tiene un montón enorme de exámenes revueltos.
    //   1) DIVIDIR: parta el montón por la mitad, y cada mitad otra vez,
    //      hasta que cada pila tenga UN solo examen (una pila de uno ya
    //      está ordenada, ¡no hay nada que ordenar!).
    //   2) MEZCLAR: junte dos pilas ya ordenadas mirando siempre el examen
    //      de arriba de cada una y tomando el menor. Al terminar, una sola
    //      pila ordenada. Se repite hasta tener una única pila.
    //
    // ¿Por qué es ideal para listas enlazadas? Porque NO mueve datos ni
    // necesita "ir a la posición k": solo cambia las manos Next de los
    // eslabones. Es estable (si dos empatan, conservan su orden) y su costo
    // crece de forma muy razonable: O(n log n).
    // ==================================================================
    public void Ordenar(Comparison<T> comparacion)
    {
        if (_head is null || Count < 2) return;   // Con 0 o 1 elementos ya está ordenada.

        // Se ordena usando SOLO las manos "Next". La función devuelve el
        // nuevo primer eslabón. Durante este proceso las manos "Previous"
        // quedan desactualizadas (no importa, se arreglan abajo).
        _head = MergeSort(_head, Count, comparacion);

        // RECONSTRUIR "Previous" y la cola: se camina la cadena ya ordenada
        // y, en cada eslabón, se le dice quién es el que lo precede.
        //   [A] -> [B] -> [C]     "previo" va siguiendo al eslabón actual:
        //   B.Previous = A, C.Previous = B ... y el último visto es la cola.
        CustomNode<T>? previo = null;
        for (var n = _head; n is not null; n = n.Next)
        {
            n.Previous = previo;
            previo = n;
        }
        _tail = previo;
    }

    // Parte 1: DIVIDIR. Recibe el primer eslabón de un tramo y cuántos
    // eslabones tiene ese tramo ("cantidad"). Devuelve el tramo ya ordenado.
    private static CustomNode<T> MergeSort(CustomNode<T> cabeza, int cantidad, Comparison<T> cmp)
    {
        // Caso base: un solo eslabón ya está ordenado. Nos aseguramos de que
        // no arrastre una mano "Next" hacia otro tramo.
        if (cantidad == 1)
        {
            cabeza.Next = null;
            return cabeza;
        }

        // Buscar el eslabón del medio caminando "mitad - 1" pasos desde el inicio.
        var mitad = cantidad / 2;
        var medio = cabeza;
        for (var i = 1; i < mitad; i++) medio = medio.Next!;

        // CORTE: la primera mitad termina en "medio"; la segunda empieza
        // en lo que "medio" tenía agarrado. Se suelta esa mano y quedan dos
        // cadenas independientes.
        //   antes: [1]->[2]->[3]->[4]
        //   ahora: [1]->[2]   y   [3]->[4]
        var derecha = medio.Next!;
        medio.Next = null;

        // Se ordena cada mitad (la función se llama a sí misma con tramos
        // cada vez más chicos) y luego se mezclan los resultados.
        var izq = MergeSort(cabeza, mitad, cmp);
        var der = MergeSort(derecha, cantidad - mitad, cmp);
        return Mezclar(izq, der, cmp);
    }

    // Parte 2: MEZCLAR dos cadenas ya ordenadas en una sola ordenada.
    // "x" e "y" son los dedos que señalan el eslabón de turno de cada cadena;
    // "cola" es el último eslabón de la cadena resultado.
    private static CustomNode<T> Mezclar(CustomNode<T> a, CustomNode<T> b, Comparison<T> cmp)
    {
        CustomNode<T>? x = a, y = b;
        CustomNode<T> cabeza;   // Primer eslabón del resultado.

        // El primer eslabón del resultado es el menor de los dos primeros.
        // (Con "<= 0" gana el de la izquierda en empates: eso hace al
        // algoritmo ESTABLE.) Ese dedo avanza al siguiente eslabón.
        if (cmp(x.Value, y.Value) <= 0) { cabeza = x; x = x.Next; }
        else { cabeza = y; y = y.Next; }

        var cola = cabeza;
        // Mientras ambas cadenas tengan eslabones, se compara el de turno de
        // cada una: el menor se ENGANCHA al final del resultado (cola.Next = ...)
        // y su dedo avanza. Aquí es donde se "reconectan" los punteros Next.
        while (x is not null && y is not null)
        {
            if (cmp(x.Value, y.Value) <= 0) { cola.Next = x; x = x.Next; }
            else { cola.Next = y; y = y.Next; }
            cola = cola.Next!;   // La cola ahora es el eslabón recién enganchado.
        }

        // Cuando una cadena se agota, el resto de la otra ya está ordenado:
        // se engancha completo de un solo golpe.
        cola.Next = x ?? y;
        return cabeza;
    }
}
