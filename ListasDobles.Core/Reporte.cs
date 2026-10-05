using System.Text;

namespace ListasDobles.Core;

// ============================================================================
// CLASE: Reporte
// ----------------------------------------------------------------------------
// Convierte los números del benchmark en una TABLA de texto fácil de leer y
// declara quién ganó. No mide nada: solo presenta resultados.
// ============================================================================
public static class Reporte
{
    public static string Generar(InformeBenchmark inf)
    {
        // Anchos de columna (en caracteres) para que la tabla quede alineada.
        const int ancho1 = 32, ancho = 22;
        var res = inf.Resultados;
        var sb = new StringBuilder();   // "Libreta" donde se va armando el texto.
        var linea = new string('=', ancho1 + ancho * (res.Count + 1));

        // Encabezado: datos generales de la prueba.
        sb.AppendLine($"REPORTE COMPARATIVO - {inf.Cantidad:N0} registros - mediana de {inf.Rondas} ronda(s) - tiempos en ms (Stopwatch)");
        sb.AppendLine($"Lectura y parseo del CSV (común a las 3 listas, fuera de los totales): {inf.LecturaCsvMicrosegundos / 1000.0:N3} ms");
        foreach (var a in inf.Advertencias) sb.AppendLine($"[!] {a}");
        sb.AppendLine(linea);

        // Fila de títulos: una columna por lista.
        sb.Append("Operación".PadRight(ancho1));
        foreach (var r in res) sb.Append(r.Lista.PadLeft(ancho));
        sb.AppendLine("Más rápida".PadLeft(ancho));
        sb.AppendLine(new string('-', linea.Length));

        // Una fila por operación. Además se reparten PUNTOS: en cada operación
        // la lista más rápida recibe 1 punto, la segunda 2 y la tercera 3.
        // Al final gana quien tenga MENOS puntos (como un golf).
        var puntos = new int[res.Count];
        for (var i = 0; i < Benchmark.NombresPasos.Length; i++)
        {
            sb.Append(Benchmark.NombresPasos[i].PadRight(ancho1));
            foreach (var r in res)
                sb.Append((r.Pasos[i].Microsegundos / 1000.0).ToString("N3").PadLeft(ancho));

            // Ordenar las listas de la más rápida a la más lenta en ESTA operación.
            var paso = i;
            var orden = Enumerable.Range(0, res.Count)
                                  .OrderBy(k => res[k].Pasos[paso].Microsegundos)
                                  .ToArray();
            for (var pos = 0; pos < orden.Length; pos++) puntos[orden[pos]] += pos + 1;

            sb.AppendLine(res[orden[0]].Lista.PadLeft(ancho));
        }

        // Filas de totales: tiempo total, memoria usada y puntos.
        sb.AppendLine(new string('-', linea.Length));
        sb.Append("TOTAL (ms)".PadRight(ancho1));
        foreach (var r in res) sb.Append((r.TotalMicrosegundos / 1000.0).ToString("N3").PadLeft(ancho));
        sb.AppendLine();
        sb.Append("Memoria asignada (MB)".PadRight(ancho1));
        foreach (var r in res) sb.Append(r.MegabytesAsignados.ToString("N2").PadLeft(ancho));
        sb.AppendLine();
        sb.Append("Puntos (menor = mejor)".PadRight(ancho1));
        foreach (var p in puntos) sb.Append(p.ToString().PadLeft(ancho));
        sb.AppendLine();
        sb.AppendLine(linea);

        // CLASIFICACIÓN: por puntos; si hay empate, gana el de menor tiempo total.
        var ranking = Enumerable.Range(0, res.Count)
                                .OrderBy(k => puntos[k])
                                .ThenBy(k => res[k].TotalMicrosegundos)
                                .ToArray();
        var mejor = res[ranking[0]];

        sb.AppendLine();
        sb.AppendLine("CLASIFICACIÓN GENERAL (puntos por posición en cada operación; desempate por tiempo total):");
        for (var pos = 0; pos < ranking.Length; pos++)
        {
            var r = res[ranking[pos]];
            var relativo = r.TotalMicrosegundos / mejor.TotalMicrosegundos;   // "2.00x" = el doble de lento que el mejor.
            sb.AppendLine($"  {pos + 1}. {r.Lista,-22} puntos: {puntos[ranking[pos]],3}   total: {r.TotalMicrosegundos / 1000.0,12:N3} ms   ({relativo:N2}x del mejor)");
        }

        sb.AppendLine();
        sb.AppendLine($"==> MEJOR RENDIMIENTO GENERAL: {mejor.Lista}");

        // Ganadora específica en ordenamiento.
        var ordenar = res.OrderBy(r => r.Pasos[Benchmark.IndiceOrdenar].Microsegundos).First();
        sb.AppendLine($"==> MÁS RÁPIDA ORDENANDO DATOS ALEATORIOS: {ordenar.Lista} ({ordenar.Pasos[Benchmark.IndiceOrdenar].Microsegundos / 1000.0:N3} ms)");

        // Notas para que quien lea el reporte entienda por qué es confiable.
        sb.AppendLine();
        sb.AppendLine("Notas metodológicas:");
        sb.AppendLine(" - Calentamiento: 4 pasadas completas sobre 20 000 registros, con pausas para que termine el tiering del JIT.");
        sb.AppendLine(" - GC.Collect() compactante antes de CADA operación medida. El GC que ocurra durante la operación sí se cuenta (es costo real de la estructura).");
        sb.AppendLine(" - Las 3 listas reciben las mismas instancias de Registro ya parseadas, los mismos IDs y los mismos predicados/comparadores.");
        sb.AppendLine(" - Cada ronda crea listas nuevas y rota el orden de ejecución; se reporta la mediana por operación.");
        sb.AppendLine(" - Se verifica que las 3 terminen con la misma cantidad y que el ordenamiento sea correcto en ambos sentidos.");
        sb.AppendLine(" - List<T> guarda las referencias contiguas (mejor localidad de caché); las listas enlazadas dependen de nodos dispersos en el heap.");
        return sb.ToString();
    }
}
