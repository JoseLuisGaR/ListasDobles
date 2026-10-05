namespace ListasDobles.Core;

// Opciones por las que el usuario puede ordenar los registros.
public enum CriterioOrden { Id, Nombre, Correo, Ciudad, Monto }

// ============================================================================
// CLASE: Criterios
// ----------------------------------------------------------------------------
// Fabrica la "regla de comparación" para cada criterio. Una regla recibe dos
// registros A y B y responde con un número:
//   negativo -> A va antes que B
//   cero     -> están empatados
//   positivo -> A va después que B
// Esa misma regla se le entrega a las tres listas, así todas ordenan igual.
// ============================================================================
public static class Criterios
{
    public static Comparison<Registro> Comparador(CriterioOrden criterio) => criterio switch
    {
        // Texto: se compara ignorando mayúsculas/minúsculas; si empatan, decide el Id.
        CriterioOrden.Nombre => (a, b) => Desempatar(string.Compare(a.Nombre, b.Nombre, StringComparison.OrdinalIgnoreCase), a, b),
        CriterioOrden.Correo => (a, b) => Desempatar(string.Compare(a.Correo, b.Correo, StringComparison.OrdinalIgnoreCase), a, b),
        CriterioOrden.Ciudad => (a, b) => Desempatar(string.Compare(a.Ciudad, b.Ciudad, StringComparison.OrdinalIgnoreCase), a, b),
        // Número: se compara el monto; si empatan, decide el Id.
        CriterioOrden.Monto => (a, b) => Desempatar(a.Monto.CompareTo(b.Monto), a, b),
        // Caso por defecto: ordenar por Id.
        _ => (a, b) => a.Id.CompareTo(b.Id),
    };

    // DESEMPATE por Id: si dos registros son "iguales" en el criterio elegido
    // (dos personas de la misma ciudad), se usa el Id para decidir. Así el
    // orden final es SIEMPRE el mismo en las tres listas.
    private static int Desempatar(int resultado, Registro a, Registro b) =>
        resultado != 0 ? resultado : a.Id.CompareTo(b.Id);
}
