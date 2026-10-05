using System.Diagnostics;
using System.Globalization;
using ListasDobles.Core;

namespace ListasDobles.Consola;

// ============================================================================
// CLASE: Menu   (la "cara" de la versión Consola)
// ----------------------------------------------------------------------------
// Muestra las opciones, lee lo que escribe el usuario y se lo pide al
// GestorListas. NO contiene lógica de listas: es solo un intérprete entre la
// persona y el Core. Cada opción del menú tiene su propio método más abajo.
// ============================================================================
internal sealed class Menu(GestorListas gestor)
{
    // Carpeta desde donde se ejecutó el programa: ahí se proponen los archivos.
    private static readonly string Carpeta = Environment.CurrentDirectory;

    // ------------------------------------------------------------------
    // EJECUTAR: el ciclo principal. Repite hasta que se elige "0) Salir":
    //   1) dibuja el menú (con cuántos registros hay en cada lista),
    //   2) lee la opción escrita,
    //   3) llama al método correspondiente.
    // Si ocurre un error "esperable" (archivo inexistente, número mal
    // escrito...) se muestra el mensaje y el programa continúa.
    // ------------------------------------------------------------------
    public void Ejecutar()
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("==== CustomLinkedList<T> vs LinkedList<T> vs List<T> (.NET 10) ====");
            Console.WriteLine("Registros: " + string.Join(" | ", gestor.Listas.Select(l => $"{l.Nombre}: {l.Count:N0}")));
            Console.WriteLine(" 1) Importar CSV");
            Console.WriteLine(" 2) Exportar CSV (orden aleatorio)");
            Console.WriteLine(" 3) Agregar registro");
            Console.WriteLine(" 4) Eliminar registro por ID");
            Console.WriteLine(" 5) Recorrer lista (adelante / atrás)");
            Console.WriteLine(" 6) Modificar registro");
            Console.WriteLine(" 7) Ordenar listas");
            Console.WriteLine(" 8) Benchmark comparativo");
            Console.WriteLine(" 9) Generar CSV de ejemplo (orden aleatorio)");
            Console.WriteLine(" 0) Salir");
            Console.Write("Opción: ");

            try
            {
                switch (Console.ReadLine()?.Trim())
                {
                    case "1": Importar(); break;
                    case "2": Exportar(); break;
                    case "3": Agregar(); break;
                    case "4": Eliminar(); break;
                    case "5": Recorrer(); break;
                    case "6": Modificar(); break;
                    case "7": Ordenar(); break;
                    case "8": CorrerBenchmark(); break;
                    case "9": Generar(); break;
                    case "0": return;
                    default: Console.WriteLine("Opción no válida."); break;
                }
            }
            catch (Exception ex) when (ex is IOException or FormatException or OverflowException or UnauthorizedAccessException)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }

    // ----- Ayudas para leer datos del teclado -----

    // Pregunta un texto. Si el usuario solo pulsa Enter y hay un valor por
    // defecto (lo que sale entre corchetes), se usa ese.
    private static string Leer(string mensaje, string? defecto = null)
    {
        Console.Write(defecto is null ? $"{mensaje}: " : $"{mensaje} [{defecto}]: ");
        var s = Console.ReadLine()?.Trim();
        return string.IsNullOrEmpty(s) ? defecto ?? "" : s;
    }

    // Pregunta un número entero; repite la pregunta hasta que escriba uno válido.
    private static int LeerEntero(string mensaje, int? defecto = null)
    {
        while (true)
        {
            if (int.TryParse(Leer(mensaje, defecto?.ToString()), out var v)) return v;
            Console.WriteLine("Número no válido.");
        }
    }

    // Igual, pero para montos con decimales.
    private static decimal LeerDecimal(string mensaje, decimal defecto)
    {
        while (true)
        {
            var s = Leer(mensaje, defecto.ToString("0.00", CultureInfo.CurrentCulture));
            if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.CurrentCulture, out var v)) return v;
            Console.WriteLine("Monto no válido.");
        }
    }

    // Muestra las 3 listas numeradas y devuelve la que el usuario elija.
    private ILista<Registro> ElegirLista()
    {
        for (var i = 0; i < gestor.Listas.Count; i++)
            Console.WriteLine($"  {i + 1}) {gestor.Listas[i].Nombre}");
        var n = LeerEntero("Lista (1-3)", 1);
        return gestor.Listas[Math.Clamp(n, 1, gestor.Listas.Count) - 1];
    }

    // ----- Opciones del menú -----

    // 1) IMPORTAR: pide la ruta del CSV, comprueba que exista y lo carga en las
    // 3 listas midiendo cuánto tardó con un cronómetro.
    private void Importar()
    {
        var ruta = Leer("Ruta del CSV", Path.Combine(Carpeta, "datos.csv"));
        if (!File.Exists(ruta))
        {
            Console.WriteLine("El archivo no existe. Use la opción 9 para generar uno de ejemplo.");
            return;
        }
        var sw = Stopwatch.StartNew();
        var n = gestor.Importar(ruta);
        sw.Stop();
        Console.WriteLine($"{n:N0} registros cargados en las 3 listas ({sw.Elapsed.TotalMilliseconds:N2} ms).");
    }

    // 2) EXPORTAR: el usuario elige qué lista guardar y dónde; el archivo
    // resultante queda en orden aleatorio.
    private void Exportar()
    {
        var lista = ElegirLista();
        var ruta = Leer("Ruta de salida", Path.Combine(Carpeta, "salida.csv"));
        var n = gestor.Exportar(ruta, lista);
        Console.WriteLine($"{n:N0} registros exportados en orden aleatorio desde '{lista.Nombre}' a {ruta}");
    }

    // 3) AGREGAR: pide los datos del nuevo registro (el Id propuesto es el
    // siguiente disponible), valida que el nombre no esté vacío y lo añade al
    // final de las 3 listas. Si el Id ya existe, no se agrega.
    private void Agregar()
    {
        var id = LeerEntero("ID (Enter = siguiente disponible)", gestor.SiguienteId());
        var nombre = Leer("Nombre");
        if (string.IsNullOrWhiteSpace(nombre))
        {
            Console.WriteLine("El nombre no puede estar vacío.");
            return;
        }

        var nuevo = new Registro(id, nombre, Leer("Correo"), Leer("Ciudad"), LeerDecimal("Monto", 0m));
        Console.WriteLine(gestor.Agregar(nuevo)
            ? $"Registro {id} agregado al final de las 3 listas."
            : $"El ID {id} ya existe; no se agregó.");
    }

    // 4) ELIMINAR: pide un Id y lo quita de las 3 listas. Informa en cuántas
    // lo encontró (0 significa que ese Id no existe).
    private void Eliminar()
    {
        var id = LeerEntero("ID a eliminar");
        var n = gestor.Eliminar(id);
        Console.WriteLine(n == 0 ? "No se encontró el ID." : $"Eliminado en {n} de {gestor.Listas.Count} listas.");
    }

    // 5) RECORRER: el usuario elige lista, sentido (adelante o atrás) y cuántos
    // registros ver. Se imprime una tabla y al final cuántos se mostraron.
    private void Recorrer()
    {
        var lista = ElegirLista();
        var adelante = LeerEntero("Dirección: 1) adelante  2) atrás", 1) != 2;
        var max = LeerEntero("Cuántos mostrar (0 = todos)", 20);

        Console.WriteLine($"{"Id",9} | {"Nombre",-24} | {"Correo",-34} | {"Ciudad",-16} | {"Monto",12}");
        Console.WriteLine(new string('-', 108));

        var datos = gestor.Recorrer(lista, adelante);
        if (max > 0) datos = datos.Take(max);   // "Take" se queda solo con los primeros "max".

        var mostrados = 0;
        foreach (var r in datos)
        {
            Console.WriteLine(r);
            mostrados++;
        }
        Console.WriteLine($"-- {mostrados:N0} de {lista.Count:N0} registros ({(adelante ? "adelante" : "atrás")}) --");
    }

    // 6) MODIFICAR: busca el registro por Id, muestra sus valores actuales y
    // pide los nuevos (Enter conserva el valor actual). Crea una copia
    // actualizada con "with" y la sustituye en las 3 listas.
    private void Modificar()
    {
        var id = LeerEntero("ID a modificar");
        if (gestor.Buscar(id) is not { } actual)
        {
            Console.WriteLine("No se encontró el ID.");
            return;
        }

        Console.WriteLine($"Actual: {actual}");
        Console.WriteLine("(Enter conserva el valor actual)");
        var nuevo = actual with
        {
            Nombre = Leer("Nombre", actual.Nombre),
            Correo = Leer("Correo", actual.Correo),
            Ciudad = Leer("Ciudad", actual.Ciudad),
            Monto = LeerDecimal("Monto", actual.Monto),
        };

        var n = gestor.Modificar(id, nuevo);
        Console.WriteLine($"Modificado en {n} de {gestor.Listas.Count} listas.");
    }

    // 7) ORDENAR: el usuario elige el criterio (1 a 5, en el mismo orden del
    // enum CriterioOrden) y se ordenan las 3 listas, mostrando el tiempo de
    // cada una.
    private void Ordenar()
    {
        if (gestor.Listas[0].Count == 0)
        {
            Console.WriteLine("No hay datos. Importe un CSV primero.");
            return;
        }

        Console.WriteLine("Criterio: 1) Id  2) Nombre  3) Correo  4) Ciudad  5) Monto");
        var opcion = Math.Clamp(LeerEntero("Criterio (1-5)", 1), 1, 5);
        var criterio = (CriterioOrden)(opcion - 1);   // 1 -> Id (posición 0), 2 -> Nombre (1), ...

        foreach (var (lista, ms) in gestor.Ordenar(criterio))
            Console.WriteLine($"  {lista,-22} {ms,12:N3} ms");

        Console.WriteLine($"Las 3 listas quedaron ordenadas por {criterio}. Use la opción 5 para verificar.");
    }

    // 9) GENERAR: crea un CSV de prueba con la cantidad pedida, ya mezclado.
    private static void Generar()
    {
        var ruta = Leer("Ruta de salida", Path.Combine(Carpeta, "datos.csv"));
        var n = LeerEntero("Cantidad de registros", 1000);
        CsvService.GenerarEjemplo(ruta, n);
        Console.WriteLine($"Generado {ruta} con {n:N0} registros en orden aleatorio.");
    }

    // 8) BENCHMARK: pide cuántos registros y cuántas rondas, ejecuta la
    // prueba (los mensajes de progreso salen en pantalla), imprime la tabla
    // comparativa y la guarda en un archivo de texto.
    private static void CorrerBenchmark()
    {
        var n = LeerEntero("Cantidad de registros para el benchmark", 100_000);
        if (n < 1) return;
        var rondas = LeerEntero("Rondas (se reporta la mediana)", 5);

        Console.WriteLine("Recuerde ejecutar en Release y sin depurador: dotnet run -c Release");
        var informe = Benchmark.Ejecutar(n, rondas, null, new ProgresoDirecto<string>(Console.WriteLine));
        var reporte = Reporte.Generar(informe);

        Console.WriteLine();
        Console.WriteLine(reporte);

        var ruta = Path.Combine(Carpeta, "reporte_benchmark.txt");
        File.WriteAllText(ruta, reporte);
        Console.WriteLine($"Reporte guardado en {ruta}");
    }
}
