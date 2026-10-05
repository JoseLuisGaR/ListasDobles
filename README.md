# CustomLinkedList<T> vs LinkedList<T> vs List<T> — .NET 10

Proyectos: `ListasDobles.Core` (lógica, CSV, benchmark), `ListasDobles.Consola`, `ListasDobles.WinForms`.

```
dotnet run -c Release --project ListasDobles.Consola
dotnet run -c Release --project ListasDobles.WinForms     # requiere Windows
```
**Ejecute siempre en Release y sin depurador** (el reporte avisa si detecta Debug o depurador).

CSV: `Id,Nombre,Correo,Ciudad,Monto`. Exportar y generar guardan los registros en orden aleatorio.

## Las tres listas
| Clase | Estructura |
|---|---|
| `CustomLinkedList<T>` | Doblemente enlazada propia: `CustomNode<T>` con `Next`/`Previous`; ordenamiento MergeSort sobre punteros |
| `NativeLinkedList<T>` | Adaptador de `System.Collections.Generic.LinkedList<T>` |
| `NativeList<T>` | Adaptador de `System.Collections.Generic.List<T>` |

## Ventajas y desventajas
| | CustomLinkedList<T> | LinkedList<T> | List<T> |
|---|---|---|---|
| Memoria | 1 objeto nodo por elemento (~40 B + referencia) | 1 nodo por elemento (~48 B: incluye referencia a la lista) | 8 B por elemento en un arreglo contiguo; capacidad sobrante (hasta 2x) |
| GC | Muchos objetos pequeños | Igual | Pocos objetos grandes (el arreglo grande va al LOH) |
| Caché | Baja | Baja | Alta |
| Recorrido / búsqueda | O(n), saltos de puntero | O(n), saltos + comprobación circular `Next`/`Previous` | O(n), muy rápido por contigüidad |
| Insertar al final | O(1), 1 alocación | O(1), 1 alocación | O(1) amortizado; copia al crecer |
| Eliminar un elemento ya localizado | O(1) | O(1) | O(n) (desplaza el arreglo) |
| Ordenar | MergeSort propio sobre nodos | Arreglo + Array.Sort | `Sort` in-place |
| Mantenimiento | Propio (riesgo de errores) | Ninguno | Ninguno |

## Correcciones al benchmark
1. La lectura/parseo del CSV se hace **una sola vez** y se reporta aparte; las 3 listas reciben las **mismas instancias** de `Registro` (antes cada lista parseaba su propio CSV: distinta disposición en memoria y el costo de parseo diluía la comparación).
2. Calentamiento: 4 pasadas completas sobre 20 000 registros con pausas para que termine el tiering del JIT (`TieredPGO` desactivado en los ejecutables).
3. `GC.Collect()` compactante (+ finalizers) antes de **cada operación** medida, no solo entre listas; GC concurrente desactivado.
4. Varias rondas con listas nuevas y **orden de ejecución rotado**; se reporta la **mediana**.
5. Operaciones equivalentes: mismos IDs, predicados y comparadores; el material de prueba se prepara fuera del cronómetro; se verifica que las 3 terminen con la misma cantidad y que el orden sea correcto en ambos sentidos.
6. Se reporta también la memoria asignada por lista.
