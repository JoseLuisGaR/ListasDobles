using System.Globalization;
using System.Text;

namespace ListasDobles.Core;

// ============================================================================
// CLASE: CsvService
// ----------------------------------------------------------------------------
// Un archivo CSV es un texto donde cada línea es un registro y sus campos
// van separados por comas:   1,Ana López,ana@correo.com,Monclova,1500.50
//
// Esta clase hace tres trabajos:
//   - Leer:          del archivo a objetos Registro.
//   - Escribir:      de objetos Registro al archivo (opcionalmente mezclados).
//   - GenerarEjemplo: crear un archivo de prueba con datos inventados.
// ============================================================================
public static class CsvService
{
    // Cultura "invariante": reglas neutras para números (punto decimal),
    // para que el archivo se lea igual en cualquier computadora/idioma.
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ------------------------------------------------------------------
    // LEER (base de "Importar")
    // Problema: convertir un archivo de texto en objetos Registro.
    // Pasos:
    //   1) Abrir el archivo y leerlo LÍNEA POR LÍNEA (no se carga todo de golpe).
    //   2) Si la primera línea es el encabezado ("Id,Nombre,..."), saltarla.
    //   3) Ignorar líneas vacías.
    //   4) Dividir la línea en campos (cuidando las comillas).
    //   5) Convertir los textos a sus tipos (Id a entero, Monto a decimal) y
    //      entregar el Registro. "yield return" lo entrega uno a la vez.
    // ------------------------------------------------------------------
    public static IEnumerable<Registro> Leer(string ruta)
    {
        using var lector = new StreamReader(ruta, Encoding.UTF8);
        var primera = true;
        var numeroLinea = 0;

        while (lector.ReadLine() is { } linea)
        {
            numeroLinea++;
            if (primera)
            {
                primera = false;
                if (linea.StartsWith("Id", StringComparison.OrdinalIgnoreCase)) continue;   // Paso 2
            }
            if (string.IsNullOrWhiteSpace(linea)) continue;                                  // Paso 3

            var c = Dividir(linea);                                                          // Paso 4
            if (c.Count < 5)
                throw new FormatException($"Línea {numeroLinea}: se esperaban 5 columnas (Id,Nombre,Correo,Ciudad,Monto).");

            yield return new Registro(                                                       // Paso 5
                int.Parse(c[0], Inv),
                c[1], c[2], c[3],
                decimal.Parse(c[4], NumberStyles.Number, Inv));
        }
    }

    // ------------------------------------------------------------------
    // ESCRIBIR (base de "Exportar")
    // Problema: guardar los registros en un archivo CSV, opcionalmente en
    // ORDEN ALEATORIO.
    //
    // SHUFFLE (mezclar) - algoritmo Fisher-Yates, como barajar un mazo:
    //   se recorren las posiciones y en cada una se intercambia la carta con
    //   otra elegida al azar entre las que quedan. Todos los órdenes posibles
    //   tienen la misma probabilidad. En el código lo hace Random.Shuffle.
    //
    // Pasos:
    //   1) Si se pidió mezclar: copiar los registros a un arreglo y barajarlo.
    //   2) Escribir el encabezado.
    //   3) Escribir cada registro como una línea de campos separados por comas.
    // "azar" permite pasar un generador con semilla fija (resultados
    // repetibles, útil en el benchmark); si es null se usa uno aleatorio.
    // ------------------------------------------------------------------
    public static int Escribir(string ruta, IEnumerable<Registro> registros, bool mezclar = false, Random? azar = null)
    {
        if (mezclar)
        {
            var arreglo = registros.ToArray();                // Paso 1: copia a un arreglo...
            (azar ?? Random.Shared).Shuffle(arreglo);         // ...y se baraja.
            registros = arreglo;
        }

        using var escritor = new StreamWriter(ruta, false, new UTF8Encoding(false));
        escritor.WriteLine(Registro.Encabezado);              // Paso 2

        var total = 0;
        foreach (var r in registros)                          // Paso 3
        {
            escritor.Write(r.Id.ToString(Inv));
            escritor.Write(',');
            escritor.Write(Escapar(r.Nombre));
            escritor.Write(',');
            escritor.Write(Escapar(r.Correo));
            escritor.Write(',');
            escritor.Write(Escapar(r.Ciudad));
            escritor.Write(',');
            escritor.WriteLine(r.Monto.ToString("0.00", Inv));
            total++;
        }
        return total;
    }

    // ------------------------------------------------------------------
    // GENERAR EJEMPLO
    // Problema: tener datos de prueba sin escribirlos a mano.
    // Se inventan "cantidad" registros combinando nombres, apellidos y
    // ciudades al azar, con Id del 1 al N, y se guardan YA MEZCLADOS (así los
    // datos nunca vienen ordenados). Si se da una "semilla", el azar es
    // repetible: la misma semilla produce siempre el mismo archivo.
    // ------------------------------------------------------------------
    public static void GenerarEjemplo(string ruta, int cantidad, int? semilla = null)
    {
        string[] nombres = ["Ana", "Luis", "María", "Carlos", "Sofía", "Jorge", "Lucía", "Pedro", "Elena", "Diego", "Valeria", "Andrés"];
        string[] apellidos = ["García", "Martínez", "López", "Hernández", "González", "Pérez", "Rodríguez", "Sánchez", "Ramírez", "Flores"];
        string[] ciudades = ["Monclova", "Monterrey", "Saltillo", "Torreón", "CDMX", "Guadalajara", "Puebla", "Mérida", "Tijuana", "Querétaro"];

        var rnd = semilla is { } valor ? new Random(valor) : new Random();
        var registros = Enumerable.Range(1, cantidad).Select(i =>
        {
            var n = nombres[rnd.Next(nombres.Length)];
            var a = apellidos[rnd.Next(apellidos.Length)];
            return new Registro(
                i,
                $"{n} {a}",
                $"{n}.{a}.{i}@ejemplo.com".ToLowerInvariant().Replace('á', 'a').Replace('é', 'e').Replace('í', 'i').Replace('ó', 'o').Replace('ú', 'u'),
                ciudades[rnd.Next(ciudades.Length)],
                Math.Round((decimal)rnd.NextDouble() * 10_000m, 2));
        });

        Escribir(ruta, registros, mezclar: true, azar: rnd);
    }

    // ESCAPAR: si un texto contiene coma, comillas o salto de línea, se
    // encierra entre comillas (y las comillas internas se duplican) para que
    // la coma del texto no se confunda con un separador de campos.
    private static string Escapar(string valor) =>
        valor.AsSpan().IndexOfAny(",\"\n\r") >= 0
            ? $"\"{valor.Replace("\"", "\"\"")}\""
            : valor;

    // ------------------------------------------------------------------
    // DIVIDIR: separa una línea en sus campos recorriéndola letra por letra.
    // Regla: una coma separa campos SOLO si no estamos dentro de comillas.
    // La variable "comillas" es un interruptor: se enciende al abrir comillas
    // y se apaga al cerrarlas. Dos comillas seguidas dentro de un campo
    // significan una comilla literal.
    // ------------------------------------------------------------------
    private static List<string> Dividir(string linea)
    {
        var campos = new List<string>(5);
        var sb = new StringBuilder();   // Acumula las letras del campo actual.
        var comillas = false;

        for (var i = 0; i < linea.Length; i++)
        {
            var ch = linea[i];
            if (comillas)
            {
                if (ch == '"')
                {
                    if (i + 1 < linea.Length && linea[i + 1] == '"') { sb.Append('"'); i++; }
                    else comillas = false;
                }
                else sb.Append(ch);
            }
            else if (ch == '"') comillas = true;
            else if (ch == ',') { campos.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(ch);
        }
        campos.Add(sb.ToString());
        return campos;
    }
}
