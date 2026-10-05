using System.Diagnostics;

namespace ListasDobles.Core;

// ============================================================================
// CLASE: GestorListas   (el "coordinador")
// ----------------------------------------------------------------------------
// Es lo único que las pantallas (Consola y Windows Forms) necesitan conocer.
// Guarda las TRES listas y, cuando el usuario pide una acción (importar,
// agregar, eliminar...), la aplica a las tres para que siempre contengan los
// mismos datos y se puedan comparar justamente.
//
//   Pantalla  ->  GestorListas  ->  las 3 listas  ->  resultado a la pantalla
// ============================================================================
public sealed class GestorListas
{
    // Exactamente las 3 estructuras comparadas, siempre en este orden:
    //   [0] nuestra lista enlazada, [1] LinkedList<T> oficial, [2] List<T> oficial.
    public IReadOnlyList<ILista<Registro>> Listas { get; } =
    [
        new CustomLinkedList<Registro>(),
        new NativeLinkedList<Registro>(),
        new NativeList<Registro>(),
    ];

    // ------------------------------------------------------------------
    // IMPORTAR
    // Problema: cargar un CSV en las tres listas.
    // Pasos: 1) leer el archivo UNA sola vez a un arreglo
    //        2) vaciar cada lista
    //        3) meter los mismos registros, en el mismo orden, en cada lista.
    // Devuelve cuántos registros se cargaron.
    // ------------------------------------------------------------------
    public int Importar(string ruta)
    {
        var datos = CsvService.Leer(ruta).ToArray();
        foreach (var lista in Listas)
        {
            lista.Limpiar();
            foreach (var r in datos) lista.Agregar(r);
        }
        return datos.Length;
    }

    // ------------------------------------------------------------------
    // EXPORTAR
    // Problema: guardar el contenido de UNA lista en un CSV.
    // Se recorre la lista de principio a fin y se escribe con "mezclar: true",
    // por lo que el archivo queda en ORDEN ALEATORIO (ver CsvService.Escribir).
    // ------------------------------------------------------------------
    public int Exportar(string ruta, ILista<Registro> lista) =>
        CsvService.Escribir(ruta, lista.Adelante(), mezclar: true);

    // RECORRER: devuelve los elementos de una lista hacia adelante (true) o
    // hacia atrás (false). Quien llama decide cuántos mostrar.
    public IEnumerable<Registro> Recorrer(ILista<Registro> lista, bool adelante) =>
        adelante ? lista.Adelante() : lista.Atras();

    // BUSCAR por Id. Basta preguntarle a una lista (las tres tienen lo mismo).
    // "r => r.Id == id" se lee: "dado un registro r, ¿su Id es el buscado?".
    public Registro? Buscar(int id) => Listas[0].Buscar(r => r.Id == id);

    // SIGUIENTE ID: el mayor Id existente + 1 (o 1 si no hay datos).
    public int SiguienteId() =>
        Listas[0].Count == 0 ? 1 : Listas[0].Adelante().Max(r => r.Id) + 1;

    // ------------------------------------------------------------------
    // AGREGAR
    // Problema: añadir un registro nuevo sin repetir Ids.
    // Pasos: 1) si ya existe ese Id, se rechaza (devuelve false)
    //        2) si no, se agrega AL FINAL de las tres listas.
    // ------------------------------------------------------------------
    public bool Agregar(Registro registro)
    {
        if (Buscar(registro.Id) is not null) return false;
        foreach (var lista in Listas) lista.Agregar(registro);
        return true;
    }

    // ------------------------------------------------------------------
    // ELIMINAR
    // Problema: quitar un registro por Id de las tres listas.
    // Cada lista busca el registro y lo quita a su manera (nodos: se
    // "puentea"; arreglo: se corren los de atrás). Devuelve en cuántas
    // listas se encontró (normalmente 3, o 0 si el Id no existe).
    // ------------------------------------------------------------------
    public int Eliminar(int id) => Listas.Count(l => l.Eliminar(r => r.Id == id));

    // ------------------------------------------------------------------
    // MODIFICAR
    // Problema: actualizar los datos de un registro existente.
    // Cada lista busca el registro por Id y lo REEMPLAZA por "nuevo" (que
    // conserva el mismo Id gracias a "with { Id = id }"). Recuerde: un
    // Registro no se edita, se sustituye por una copia actualizada.
    // ------------------------------------------------------------------
    public int Modificar(int id, Registro nuevo) =>
        Listas.Count(l => l.Modificar(r => r.Id == id, _ => nuevo with { Id = id }));

    // ------------------------------------------------------------------
    // ORDENAR
    // Problema: ordenar las tres listas por el criterio elegido y saber
    // cuánto tardó cada una.
    // Pasos: 1) obtener la regla de comparación del criterio
    //        2) por cada lista: arrancar el cronómetro (Stopwatch), ordenar,
    //           detener el cronómetro y anotar los milisegundos.
    // ------------------------------------------------------------------
    public IReadOnlyList<(string Lista, double Milisegundos)> Ordenar(CriterioOrden criterio)
    {
        var comparacion = Criterios.Comparador(criterio);
        var tiempos = new List<(string Lista, double Milisegundos)>(Listas.Count);

        foreach (var lista in Listas)
        {
            var sw = Stopwatch.StartNew();   // Arranca el cronómetro.
            lista.Ordenar(comparacion);      // Lo que queremos medir.
            sw.Stop();                       // Detiene el cronómetro.
            tiempos.Add((lista.Nombre, sw.Elapsed.TotalMilliseconds));
        }
        return tiempos;
    }
}
