using System.Diagnostics;
using System.Reflection;

namespace ListasDobles.Core;

// Tiempo (en microsegundos) y memoria (en bytes) que gastó UNA operación.
public sealed record PasoBenchmark(string Nombre, double Microsegundos, double BytesAsignados);

// Todos los pasos medidos de UNA lista, más totales calculados.
public sealed record ResultadoBenchmark(string Lista, IReadOnlyList<PasoBenchmark> Pasos)
{
    public double TotalMicrosegundos => Pasos.Sum(p => p.Microsegundos);
    public double MegabytesAsignados => Pasos.Sum(p => p.BytesAsignados) / 1_048_576.0;
}

// El "paquete" final de la prueba: resultados de las 3 listas + datos de contexto.
public sealed record InformeBenchmark(
    int Cantidad,
    int Rondas,
    double LecturaCsvMicrosegundos,
    IReadOnlyList<ResultadoBenchmark> Resultados,
    IReadOnlyList<string> Advertencias);

// ============================================================================
// CLASE: Benchmark   (la "carrera" entre las tres listas)
// ----------------------------------------------------------------------------
// Mide cuánto tarda cada lista en: importar, recorrer (adelante y atrás),
// ordenar, agregar, modificar, eliminar y exportar.
//
// Para que la carrera sea JUSTA se toman estas precauciones:
//   1) CALENTAMIENTO (warm-up): se corre todo antes, sin medir.
//   2) MISMOS DATOS: el CSV se lee una sola vez y las 3 listas reciben los
//      mismos objetos y las mismas operaciones.
//   3) LIMPIEZA DE MEMORIA (GC.Collect) antes de cada medición.
//   4) VARIAS RONDAS con el orden de salida ROTADO, y se toma la MEDIANA.
//   5) VERIFICACIÓN: se comprueba que las 3 listas terminen igual.
// Cada una se explica en el lugar del código donde se aplica.
// ============================================================================
public static class Benchmark
{
    // Posición del paso "Ordenar" dentro de NombresPasos (la usa el reporte).
    public const int IndiceOrdenar = 3;

    // Las operaciones medidas, en el orden en que se ejecutan.
    public static readonly string[] NombresPasos =
    [
        "Importar CSV (inserción)",
        "Recorrer adelante",
        "Recorrer atrás",
        "Ordenar por ID (aleatorio)",
        "Agregar al final (muestra)",
        "Modificar masivo (Id par)",
        "Modificar por ID (muestra)",
        "Eliminar por ID (muestra)",
        "Eliminar masivo (Id x3)",
        "Exportar CSV (mezclado)",
    ];

    // Variable "sumidero": al recorrer sumamos los Id y guardamos el total aquí.
    // Así el compilador no puede decidir que el recorrido "no sirve para nada"
    // y eliminarlo (lo que haría que midiéramos un recorrido que nunca ocurrió).
    private static long _sumidero;

    // ------------------------------------------------------------------
    // EJECUTAR: orquesta toda la prueba.
    //   cantidad -> cuántos registros usar     rondas -> cuántas repeticiones
    // ------------------------------------------------------------------
    public static InformeBenchmark Ejecutar(
        int cantidad, int rondas = 5, string? directorio = null, IProgress<string>? progreso = null)
    {
        cantidad = Math.Max(cantidad, 100);
        rondas = Math.Clamp(rondas, 1, 50);
        directorio ??= Path.Combine(Path.GetTempPath(), "ListasDoblesBenchmark");
        Directory.CreateDirectory(directorio);

        // "Fábricas": cada ronda necesita listas NUEVAS y vacías. Cada fábrica
        // es una receta para crear una lista nueva de cada tipo.
        Func<ILista<Registro>>[] fabricas =
        [
            () => new CustomLinkedList<Registro>(),
            () => new NativeLinkedList<Registro>(),
            () => new NativeList<Registro>(),
        ];

        // ==============================================================
        // 1) CALENTAMIENTO (WARM-UP)
        // --------------------------------------------------------------
        // C# no se convierte a lenguaje de máquina de una vez: el JIT (el
        // "traductor en vivo") compila cada método LA PRIMERA VEZ que se usa,
        // y después lo vuelve a optimizar si ve que se usa mucho. Por eso las
        // primeras ejecuciones son más lentas.
        // Analogía: un atleta que NO calienta antes de la carrera.
        // Si midiéramos sin calentar, la primera lista parecería peor solo por
        // pagar esa traducción, no por ser peor estructura.
        // Solución: correr todo el escenario 4 veces con pocos datos y
        // DESCARTAR esos tiempos. Las pausas (Sleep) dejan que el JIT termine
        // de optimizar en segundo plano.
        // ==============================================================
        progreso?.Report("Calentamiento del JIT...");
        var csvCal = Path.Combine(directorio, "calentamiento.csv");
        var salidaCal = Path.Combine(directorio, "calentamiento_salida.csv");
        CsvService.GenerarEjemplo(csvCal, 20_000, 42);
        var datosCal = CsvService.Leer(csvCal).ToArray();
        for (var i = 0; i < 4; i++)
        {
            foreach (var f in fabricas) EjecutarEscenario(f(), datosCal, salidaCal);
            if (i == 1) Thread.Sleep(300);
        }
        Thread.Sleep(300);

        // ==============================================================
        // 2) DATOS REALES, LEÍDOS UNA SOLA VEZ
        // --------------------------------------------------------------
        // El CSV se genera ya mezclado (para que "Ordenar" tenga trabajo real)
        // con semilla fija (42) para que la prueba sea repetible. Leerlo y
        // convertirlo a Registro cuesta lo mismo para todas, así que se hace
        // UNA vez y se reporta aparte. Las 3 listas reciben los MISMOS objetos.
        // ==============================================================
        progreso?.Report($"Generando CSV aleatorio con {cantidad:N0} registros...");
        var csv = Path.Combine(directorio, "benchmark_entrada.csv");
        var salida = Path.Combine(directorio, "benchmark_salida.csv");
        CsvService.GenerarEjemplo(csv, cantidad, 42);

        LimpiarMemoria();
        var sw = Stopwatch.StartNew();                       // Cronómetro: lectura del CSV.
        var datos = CsvService.Leer(csv).ToArray();
        sw.Stop();
        var lecturaUs = sw.Elapsed.TotalMicroseconds;

        // ==============================================================
        // 3) RONDAS MEDIDAS
        // --------------------------------------------------------------
        // Se guardan los tiempos en "cajones": tiempos[lista][paso][ronda].
        // ==============================================================
        var nPasos = NombresPasos.Length;
        var tiempos = new double[fabricas.Length][][];
        var bytes = new double[fabricas.Length][][];
        var nombres = new string[fabricas.Length];
        var cuentas = new int[fabricas.Length];

        for (var l = 0; l < fabricas.Length; l++)
        {
            tiempos[l] = new double[nPasos][];
            bytes[l] = new double[nPasos][];
            for (var p = 0; p < nPasos; p++)
            {
                tiempos[l][p] = new double[rondas];
                bytes[l][p] = new double[rondas];
            }
        }

        for (var ronda = 0; ronda < rondas; ronda++)
        {
            for (var k = 0; k < fabricas.Length; k++)
            {
                // ORDEN ROTADO: en la ronda 0 sale primero la lista 0; en la 1
                // sale primero la 1; etc. Así ninguna lista tiene siempre la
                // "ventaja" o "desventaja" de ir primera (memoria más limpia,
                // procesador más fresco o más caliente...).
                var l = (k + ronda) % fabricas.Length;
                var lista = fabricas[l]();                 // Lista NUEVA y vacía en cada ronda.
                nombres[l] = lista.Nombre;
                progreso?.Report($"Ronda {ronda + 1}/{rondas}: {lista.Nombre}...");

                var (pasos, cuentaFinal) = EjecutarEscenario(lista, datos, salida);
                for (var p = 0; p < nPasos; p++)
                {
                    tiempos[l][p][ronda] = pasos[p].Microsegundos;
                    bytes[l][p][ronda] = pasos[p].BytesAsignados;
                }
                cuentas[l] = cuentaFinal;
            }

            // VERIFICACIÓN DE EQUIVALENCIA: tras hacer lo mismo, las tres
            // listas deben tener la misma cantidad de elementos. Si no, algo
            // falló y la comparación no sería válida.
            if (cuentas.Distinct().Count() != 1)
                throw new InvalidOperationException(
                    "Las listas terminaron con cantidades distintas: las operaciones no fueron equivalentes.");
        }

        // ==============================================================
        // 4) MEDIANA
        // --------------------------------------------------------------
        // De los tiempos de las rondas se toma el del MEDIO (la mediana).
        // Si una ronda salió rarísima (el antivirus, otro programa...), la
        // mediana la ignora; el promedio, en cambio, se dejaría arrastrar.
        // ==============================================================
        var resultados = new List<ResultadoBenchmark>();
        for (var l = 0; l < fabricas.Length; l++)
        {
            var pasosMediana = Enumerable.Range(0, nPasos)
                .Select(p => new PasoBenchmark(NombresPasos[p], Mediana(tiempos[l][p]), Mediana(bytes[l][p])))
                .ToList();
            resultados.Add(new ResultadoBenchmark(nombres[l], pasosMediana));
        }

        // Avisos para el usuario si las condiciones no son buenas para medir.
        var advertencias = new List<string>();
        if (typeof(Benchmark).Assembly.GetCustomAttribute<DebuggableAttribute>() is { IsJITOptimizerDisabled: true })
            advertencias.Add("Compilación Debug (optimizaciones desactivadas): ejecute con 'dotnet run -c Release'.");
        if (Debugger.IsAttached)
            advertencias.Add("Hay un depurador adjunto: ejecute sin depurar (Ctrl+F5) para tiempos fiables.");

        progreso?.Report("Benchmark finalizado.");
        return new InformeBenchmark(cantidad, rondas, lecturaUs, resultados, advertencias);
    }

    // ------------------------------------------------------------------
    // EJECUTAR ESCENARIO: la "carrera" completa de UNA lista.
    // Hace, en orden, las 10 operaciones y mide cada una por separado.
    // Es EXACTAMENTE el mismo código para las 3 listas (solo cambia la lista
    // que recibe), y eso es lo que garantiza la equidad.
    // ------------------------------------------------------------------
    private static (PasoBenchmark[] Pasos, int CuentaFinal) EjecutarEscenario(
        ILista<Registro> lista, Registro[] datos, string salida)
    {
        var n = datos.Length;
        var pasos = new List<PasoBenchmark>(NombresPasos.Length);

        // PREPARACIÓN FUERA DEL CRONÓMETRO: los Ids a modificar/eliminar y los
        // registros nuevos se calculan ANTES de medir y son los mismos para las
        // 3 listas. Así el cronómetro solo mide a la lista, no nuestra preparación.
        var muestra = Math.Min(500, n);
        var salto = Math.Max(1, n / muestra);
        var idsModificar = Enumerable.Range(0, muestra).Select(i => datos[i * salto].Id).ToArray();
        var idsEliminar = Enumerable.Range(0, muestra).Select(i => datos[(i * salto + 1) % n].Id).ToArray();
        var nuevos = Enumerable.Range(0, muestra)
            .Select(i => new Registro(n + i + 1, $"Nuevo {i}", $"nuevo{i}@ejemplo.com", "Monclova", i))
            .ToArray();
        var porId = Criterios.Comparador(CriterioOrden.Id);

        // ==============================================================
        // PASO (el "cronometrador"): mide UNA operación.
        //   1) LimpiarMemoria(): GC.Collect antes de medir (ver abajo).
        //   2) Anota cuánta memoria se había pedido hasta ahora.
        //   3) Stopwatch.StartNew(): arranca el cronómetro de alta precisión.
        //   4) Ejecuta la operación ("accion").
        //   5) sw.Stop(): detiene el cronómetro; Elapsed trae el tiempo.
        //   6) Calcula cuánta memoria nueva se pidió durante la operación.
        // ==============================================================
        void Paso(string nombre, Action accion)
        {
            LimpiarMemoria();   // El GC pendiente de pasos anteriores no contamina esta medición.
            var antes = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            accion();
            sw.Stop();
            var asignado = GC.GetAllocatedBytesForCurrentThread() - antes;
            pasos.Add(new PasoBenchmark(nombre, sw.Elapsed.TotalMicroseconds, asignado));
        }

        // --- IMPORTAR: se mide solo la INSERCIÓN en la lista (el CSV ya se
        // leyó una vez, fuera de aquí). Las 3 reciben los mismos objetos.
        Paso(NombresPasos[0], () =>
        {
            foreach (var r in datos) lista.Agregar(r);
        });

        // --- RECORRER ADELANTE: visitar los n elementos del primero al último,
        // sumando los Id (la suma evita que el compilador elimine el recorrido).
        Paso(NombresPasos[1], () =>
        {
            long s = 0;
            foreach (var r in lista.Adelante()) s += r.Id;
            _sumidero += s;
        });

        // --- RECORRER ATRÁS: lo mismo, del último al primero.
        Paso(NombresPasos[2], () =>
        {
            long s = 0;
            foreach (var r in lista.Atras()) s += r.Id;
            _sumidero += s;
        });

        // --- ORDENAR: los datos vienen mezclados del CSV, así que hay trabajo
        // real. Después se VERIFICA el resultado (fuera del cronómetro).
        Paso(NombresPasos[IndiceOrdenar], () => lista.Ordenar(porId));
        VerificarOrden(lista, n);

        // --- AGREGAR: 500 registros nuevos al final de la lista.
        Paso(NombresPasos[4], () =>
        {
            foreach (var r in nuevos) lista.Agregar(r);
        });

        // --- MODIFICAR MASIVO: a todos los de Id par se les sube el monto 10 %.
        Paso(NombresPasos[5], () =>
            lista.ModificarDonde(r => r.Id % 2 == 0, r => r with { Monto = r.Monto * 1.10m }));

        // --- MODIFICAR POR ID: 500 búsquedas individuales (cada una recorre
        // la lista hasta encontrar su Id).
        Paso(NombresPasos[6], () =>
        {
            foreach (var id in idsModificar)
                lista.Modificar(r => r.Id == id, r => r with { Nombre = r.Nombre + " (mod)" });
        });

        // --- ELIMINAR POR ID: 500 eliminaciones individuales (buscar + quitar).
        Paso(NombresPasos[7], () =>
        {
            foreach (var id in idsEliminar) lista.Eliminar(r => r.Id == id);
        });

        // --- ELIMINAR MASIVO: se borran todos los Id múltiplos de 3 en una pasada.
        Paso(NombresPasos[8], () => lista.EliminarDonde(r => r.Id % 3 == 0));

        // --- EXPORTAR: escribir el CSV mezclado (semilla fija 7 para que las
        // 3 listas produzcan exactamente la misma mezcla).
        Paso(NombresPasos[9], () =>
            CsvService.Escribir(salida, lista.Adelante(), mezclar: true, azar: new Random(7)));

        return ([.. pasos], lista.Count);
    }

    // ------------------------------------------------------------------
    // LIMPIAR MEMORIA (GC.Collect)
    // El GC (Garbage Collector, "recolector de basura") libera la memoria de
    // los objetos que ya nadie usa, pero lo hace CUANDO ÉL DECIDE y al hacerlo
    // pausa el programa un instante.
    // Problema: si decidiera limpiar justo en medio de la medición de una
    // lista, esa lista "pagaría" por la basura que dejó la prueba anterior.
    // Solución: forzamos la limpieza ANTES de cada medición (fuera del
    // cronómetro) para que todas empiecen desde una "mesa limpia".
    //   - Se hace dos veces: la 1.ª libera, la espera permite terminar
    //     tareas de limpieza pendientes (finalizadores), la 2.ª termina el trabajo.
    //   - "compacting: true" además reacomoda los objetos juntos en memoria.
    // Ojo: si el GC se activa DURANTE una operación (porque ella misma pide
    // mucha memoria), sí se cuenta: eso es un costo real de esa estructura.
    // ------------------------------------------------------------------
    private static void LimpiarMemoria()
    {
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
    }

    // MEDIANA: ordena los valores y toma el del centro (o el promedio de los
    // dos centrales si hay cantidad par). Ignora valores extremos raros.
    private static double Mediana(double[] valores)
    {
        var s = (double[])valores.Clone();
        Array.Sort(s);
        var m = s.Length / 2;
        return s.Length % 2 == 1 ? s[m] : (s[m - 1] + s[m]) / 2.0;
    }

    // ------------------------------------------------------------------
    // VERIFICAR ORDEN (control de calidad, NO se cronometra)
    // Comprueba que, tras ordenar, la lista quedó bien:
    //   - hacia adelante los Id suben de uno en uno,
    //   - hacia atrás los Id bajan (esto prueba que las manos "Previous"
    //     quedaron bien reconectadas),
    //   - y la cantidad de elementos es la esperada.
    // Si algo falla se detiene la prueba con un error claro.
    // ------------------------------------------------------------------
    private static void VerificarOrden(ILista<Registro> lista, int esperado)
    {
        var previo = int.MinValue;
        var cuenta = 0;
        foreach (var r in lista.Adelante())
        {
            if (r.Id <= previo)
                throw new InvalidOperationException($"{lista.Nombre}: orden incorrecto hacia adelante.");
            previo = r.Id;
            cuenta++;
        }

        var siguiente = int.MaxValue;
        var cuentaAtras = 0;
        foreach (var r in lista.Atras())
        {
            if (r.Id >= siguiente)
                throw new InvalidOperationException($"{lista.Nombre}: enlaces 'anterior' inconsistentes tras ordenar.");
            siguiente = r.Id;
            cuentaAtras++;
        }

        if (cuenta != esperado || cuentaAtras != esperado || lista.Count != esperado)
            throw new InvalidOperationException($"{lista.Nombre}: cantidad inconsistente tras ordenar.");
    }
}
