using System.Globalization;
using ListasDobles.Core;

namespace ListasDobles.WinForms;

// ============================================================================
// CLASE: MainForm   (la ventana principal)
// ----------------------------------------------------------------------------
// Dibuja la interfaz gráfica: botones, cajas de texto, tabla y pestañas. Igual
// que el menú de la consola, NO contiene lógica de listas: cada botón le pide
// el trabajo al GestorListas y después refresca lo que se ve en pantalla.
//
// La ventana tiene dos pestañas:
//   - "Datos":     importar/exportar, ver la tabla, agregar/modificar/eliminar, ordenar.
//   - "Benchmark": ejecutar la prueba de rendimiento y ver el reporte.
// ============================================================================
public sealed class MainForm : Form
{
    // El coordinador con las 3 listas (el mismo que usa la consola).
    private readonly GestorListas _gestor = new();

    // ---- Controles de la pestaña "Datos" ----
    private readonly ComboBox _cmbLista = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };      // Qué lista ver.
    private readonly ComboBox _cmbCriterio = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };    // Por qué campo ordenar.
    private readonly RadioButton _rbAdelante = new() { Text = "Adelante", Checked = true, AutoSize = true };     // Sentido del recorrido.
    private readonly RadioButton _rbAtras = new() { Text = "Atrás", AutoSize = true };
    private readonly NumericUpDown _numMax = new() { Minimum = 1, Maximum = 2_000_000, Value = 5000, Width = 80 };       // Máx. filas a mostrar.
    private readonly NumericUpDown _numGenerar = new() { Minimum = 1, Maximum = 5_000_000, Value = 1000, Width = 90 };   // Cuántos registros generar.

    // La tabla donde se ven los registros (solo lectura; se llena con DataSource).
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        MultiSelect = false,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor = SystemColors.Window,
    };

    // Cajas de texto del formulario de un registro (se usan para buscar, agregar y modificar).
    private readonly TextBox _txtId = new() { Width = 80 };
    private readonly TextBox _txtNombre = new() { Width = 170 };
    private readonly TextBox _txtCorreo = new() { Width = 230 };
    private readonly TextBox _txtCiudad = new() { Width = 120 };
    private readonly TextBox _txtMonto = new() { Width = 90 };

    // Texto de la barra inferior (cuántos registros hay en cada lista).
    private readonly ToolStripStatusLabel _lblEstado = new() { Text = "Importe un CSV para comenzar." };

    // ---- Controles de la pestaña "Benchmark" ----
    private readonly NumericUpDown _numBench = new() { Minimum = 1000, Maximum = 2_000_000, Value = 100_000, Increment = 10_000, Width = 100 };
    private readonly NumericUpDown _numRondas = new() { Minimum = 1, Maximum = 20, Value = 5, Width = 55 };
    private readonly ProgressBar _progreso = new() { Style = ProgressBarStyle.Marquee, Width = 120, Visible = false };   // Barra "ocupado".
    private readonly Label _lblBench = new() { AutoSize = true, Margin = new Padding(8, 8, 0, 0) };                      // Mensaje de avance.
    private readonly TextBox _txtReporte = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        WordWrap = false,
        ScrollBars = ScrollBars.Both,
        Font = new Font("Consolas", 10f),   // Letra de ancho fijo: así la tabla del reporte queda alineada.
        BackColor = Color.White,
    };

    // La lista que el usuario tiene elegida en el desplegable.
    private ILista<Registro> ListaSeleccionada => _gestor.Listas[Math.Max(0, _cmbLista.SelectedIndex)];

    // ------------------------------------------------------------------
    // CONSTRUCTOR: arma la ventana una sola vez al abrirse.
    //   1) Configura título y tamaño.
    //   2) Llena los desplegables (nombres de las listas y criterios de orden).
    //   3) Crea las dos pestañas y la barra de estado.
    //   4) Conecta eventos: si cambia la lista, el sentido o la fila
    //      seleccionada, la pantalla se actualiza sola.
    // ------------------------------------------------------------------
    public MainForm()
    {
        Text = "CustomLinkedList<T> vs LinkedList<T> vs List<T> — Windows Forms (.NET 10)";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1200, 720);
        MinimumSize = new Size(960, 560);
        Font = new Font("Segoe UI", 9.5f);

        foreach (var l in _gestor.Listas) _cmbLista.Items.Add(l.Nombre);
        _cmbLista.SelectedIndex = 0;
        _cmbCriterio.DataSource = Enum.GetValues<CriterioOrden>();

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(CrearTabDatos());
        tabs.TabPages.Add(CrearTabBenchmark());

        var estado = new StatusStrip();
        estado.Items.Add(_lblEstado);

        Controls.Add(tabs);
        Controls.Add(estado);

        // Eventos: "(_, _) => ..." significa "cuando ocurra esto, haz aquello".
        _grid.SelectionChanged += (_, _) => CargarSeleccion();
        _cmbLista.SelectedIndexChanged += (_, _) => Refrescar();
        _rbAdelante.CheckedChanged += (_, _) => Refrescar();
    }

    // ---------- Construcción de la interfaz ----------

    // Arma la pestaña "Datos" con tres franjas: barra de botones (arriba),
    // tabla (centro) y formulario del registro (abajo).
    private TabPage CrearTabDatos()
    {
        var tab = new TabPage("Datos") { Padding = new Padding(6) };

        // Barra superior con los botones principales y los filtros de vista.
        var barra = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 0, 0, 6) };
        barra.Controls.AddRange([
            Boton("Importar CSV", Importar),
            Boton("Exportar CSV", Exportar),
            Etiqueta("Generar:"), _numGenerar,
            Boton("Generar CSV ejemplo", GenerarEjemplo),
            Etiqueta("Criterio:"), _cmbCriterio,
            Boton("Ordenar", OrdenarListas),
            Etiqueta("Lista:"), _cmbLista,
            _rbAdelante, _rbAtras,
            Etiqueta("Máx. filas:"), _numMax,
            Boton("Recorrer", (_, _) => Refrescar()),
        ]);

        // Recuadro inferior: datos de UN registro y sus botones.
        var grupo = new GroupBox
        {
            Text = "Registro seleccionado / búsqueda por ID",
            Dock = DockStyle.Bottom,
            Height = 100,
        };
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(6) };
        panel.Controls.AddRange([
            Etiqueta("ID:"), _txtId,
            Etiqueta("Nombre:"), _txtNombre,
            Etiqueta("Correo:"), _txtCorreo,
            Etiqueta("Ciudad:"), _txtCiudad,
            Etiqueta("Monto:"), _txtMonto,
            Boton("Buscar", Buscar),
            Boton("Agregar", AgregarRegistro),
            Boton("Modificar", ModificarRegistro),
            Boton("Eliminar", EliminarRegistro),
        ]);
        grupo.Controls.Add(panel);

        tab.Controls.Add(_grid);   // El control que llena todo el centro se agrega primero, luego los bordes.
        tab.Controls.Add(grupo);
        tab.Controls.Add(barra);
        return tab;
    }

    // Arma la pestaña "Benchmark": barra con parámetros y botón de ejecutar,
    // y debajo el cuadro de texto donde aparece el reporte.
    private TabPage CrearTabBenchmark()
    {
        var tab = new TabPage("Benchmark") { Padding = new Padding(6) };

        var barra = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 0, 0, 6) };
        barra.Controls.AddRange([
            Etiqueta("Registros:"), _numBench,
            Etiqueta("Rondas:"), _numRondas,
            Boton("Ejecutar benchmark", EjecutarBenchmark),
            _progreso, _lblBench,
        ]);

        tab.Controls.Add(_txtReporte);
        tab.Controls.Add(barra);
        return tab;
    }

    // Fábrica de botones: crea uno con su texto y le conecta la acción a ejecutar al hacer clic.
    private static Button Boton(string texto, EventHandler click)
    {
        var b = new Button { Text = texto, AutoSize = true, Padding = new Padding(8, 2, 8, 2), Margin = new Padding(4) };
        b.Click += click;
        return b;
    }

    // Fábrica de etiquetas (los textos pequeños que rotulan cada caja).
    private static Label Etiqueta(string texto) =>
        new() { Text = texto, AutoSize = true, Margin = new Padding(8, 8, 2, 0) };

    // ---------- Acciones (lo que hace cada botón) ----------

    // IMPORTAR: abre el diálogo de "elegir archivo" y carga el CSV en las 3
    // listas. Se hace en segundo plano (Task.Run) para que la ventana no se
    // congele con archivos grandes. Al terminar refresca la tabla.
    private async void Importar(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog { Title = "Importar CSV", Filter = "CSV (*.csv)|*.csv|Todos los archivos|*.*" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var ruta = dlg.FileName;

        await EjecutarAsync(async () =>
        {
            var n = await Task.Run(() => _gestor.Importar(ruta));
            Refrescar();
            MessageBox.Show(this, $"{n:N0} registros cargados en las 3 listas.", "Importar", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }

    // EXPORTAR: abre el diálogo de "guardar como" y escribe la lista elegida
    // en el desplegable, en orden aleatorio, también en segundo plano.
    private async void Exportar(object? sender, EventArgs e)
    {
        using var dlg = new SaveFileDialog { Title = "Exportar CSV", Filter = "CSV (*.csv)|*.csv", FileName = "salida.csv" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var ruta = dlg.FileName;
        var lista = ListaSeleccionada;

        await EjecutarAsync(async () =>
        {
            var n = await Task.Run(() => _gestor.Exportar(ruta, lista));
            MessageBox.Show(this, $"{n:N0} registros exportados en orden aleatorio desde '{lista.Nombre}'.", "Exportar", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }

    // GENERAR EJEMPLO: crea un CSV de prueba (ya mezclado) con la cantidad
    // indicada en la cajita "Generar". Luego se puede cargar con "Importar".
    private async void GenerarEjemplo(object? sender, EventArgs e)
    {
        using var dlg = new SaveFileDialog { Title = "Generar CSV de ejemplo", Filter = "CSV (*.csv)|*.csv", FileName = "datos.csv" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var ruta = dlg.FileName;
        var cantidad = (int)_numGenerar.Value;

        await EjecutarAsync(async () =>
        {
            await Task.Run(() => CsvService.GenerarEjemplo(ruta, cantidad));
            MessageBox.Show(this, $"Archivo generado con {cantidad:N0} registros en orden aleatorio. Ahora puede importarlo.", "Generar", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }

    // ORDENAR: ordena las 3 listas por el criterio elegido, refresca la tabla
    // y muestra cuánto tardó cada lista (medido con Stopwatch en el Gestor).
    private async void OrdenarListas(object? sender, EventArgs e)
    {
        if (_gestor.Listas[0].Count == 0)
        {
            Aviso("No hay datos. Importe un CSV primero.");
            return;
        }

        var criterio = (CriterioOrden)_cmbCriterio.SelectedItem!;

        await EjecutarAsync(async () =>
        {
            var tiempos = await Task.Run(() => _gestor.Ordenar(criterio));
            Refrescar();
            Aviso($"Listas ordenadas por {criterio}:\n\n" +
                  string.Join("\n", tiempos.Select(t => $"  {t.Lista}: {t.Milisegundos:N3} ms")));
        });
    }

    // RECORRER / REFRESCAR: vuelve a llenar la tabla.
    //   1) Pide los registros de la lista elegida, en el sentido elegido.
    //   2) Se queda solo con los primeros "Máx. filas" (para no saturar la tabla).
    //   3) Los entrega a la tabla y actualiza la barra de estado.
    private void Refrescar()
    {
        var adelante = _rbAdelante.Checked;
        var filas = _gestor.Recorrer(ListaSeleccionada, adelante).Take((int)_numMax.Value).ToList();

        _grid.DataSource = filas;
        if (_grid.Columns["Monto"] is { } columna) columna.DefaultCellStyle.Format = "N2";

        _lblEstado.Text = string.Join("  |  ", _gestor.Listas.Select(l => $"{l.Nombre}: {l.Count:N0}"))
                          + $"   —   mostrando {filas.Count:N0} filas ({(adelante ? "adelante" : "atrás")})";
    }

    // Al hacer clic en una fila de la tabla, sus datos se copian a las cajas
    // de texto de abajo para poder modificarla o eliminarla.
    private void CargarSeleccion()
    {
        if (_grid.CurrentRow?.DataBoundItem is not Registro r) return;
        MostrarRegistro(r);
    }

    // Escribe los campos de un registro en las cajas de texto.
    private void MostrarRegistro(Registro r)
    {
        _txtId.Text = r.Id.ToString(CultureInfo.InvariantCulture);
        _txtNombre.Text = r.Nombre;
        _txtCorreo.Text = r.Correo;
        _txtCiudad.Text = r.Ciudad;
        _txtMonto.Text = r.Monto.ToString("0.00", CultureInfo.CurrentCulture);
    }

    // BUSCAR: toma el Id escrito, lo busca y, si existe, muestra sus datos.
    private void Buscar(object? sender, EventArgs e)
    {
        if (!int.TryParse(_txtId.Text, out var id))
        {
            Aviso("Escriba un ID numérico válido.");
            return;
        }
        if (_gestor.Buscar(id) is { } r) MostrarRegistro(r);
        else Aviso($"No se encontró el ID {id}.");
    }

    // AGREGAR: toma los datos de las cajas y agrega un registro nuevo al final
    // de las 3 listas. Si el Id está vacío se asigna el siguiente disponible;
    // si el Id ya existe, se avisa y no se agrega.
    private void AgregarRegistro(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtId.Text))
            _txtId.Text = _gestor.SiguienteId().ToString(CultureInfo.InvariantCulture);

        if (!TryLeerFormulario(out var nuevo)) return;

        if (!_gestor.Agregar(nuevo))
        {
            Aviso($"El ID {nuevo.Id} ya existe. Deje el campo ID vacío para asignar el siguiente disponible.");
            return;
        }

        Refrescar();
        Aviso($"Registro {nuevo.Id} agregado al final de las 3 listas (use el sentido «Atrás» para verlo primero).");
    }

    // MODIFICAR: toma los datos de las cajas y sustituye el registro que tenga
    // ese Id en las 3 listas.
    private void ModificarRegistro(object? sender, EventArgs e)
    {
        if (!TryLeerFormulario(out var nuevo)) return;

        var n = _gestor.Modificar(nuevo.Id, nuevo);
        if (n == 0) { Aviso($"No se encontró el ID {nuevo.Id}."); return; }

        Refrescar();
        Aviso($"Registro {nuevo.Id} modificado en {n} de {_gestor.Listas.Count} listas.");
    }

    // ELIMINAR: pide confirmación y quita el registro con ese Id de las 3 listas.
    private void EliminarRegistro(object? sender, EventArgs e)
    {
        if (!int.TryParse(_txtId.Text, out var id))
        {
            Aviso("Escriba un ID numérico válido.");
            return;
        }
        if (MessageBox.Show(this, $"¿Eliminar el registro {id} de las tres listas?", "Confirmar",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        var n = _gestor.Eliminar(id);
        if (n == 0) { Aviso($"No se encontró el ID {id}."); return; }

        Refrescar();
        Aviso($"Registro {id} eliminado en {n} de {_gestor.Listas.Count} listas.");
    }

    // Lee y VALIDA las cajas de texto: Id numérico, monto válido y nombre no
    // vacío. Si todo está bien devuelve true y entrega el Registro armado; si
    // algo falla, avisa qué corregir y devuelve false.
    private bool TryLeerFormulario(out Registro registro)
    {
        registro = null!;
        if (!int.TryParse(_txtId.Text, out var id))
        {
            Aviso("El ID debe ser numérico.");
            return false;
        }
        if (!decimal.TryParse(_txtMonto.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var monto))
        {
            Aviso("El monto no es válido.");
            return false;
        }
        if (string.IsNullOrWhiteSpace(_txtNombre.Text))
        {
            Aviso("El nombre no puede estar vacío.");
            return false;
        }
        registro = new Registro(id, _txtNombre.Text.Trim(), _txtCorreo.Text.Trim(), _txtCiudad.Text.Trim(), monto);
        return true;
    }

    // BENCHMARK: ejecuta la prueba de rendimiento en segundo plano (así la
    // ventana sigue viva), muestra una barra "ocupado" y mensajes de avance, y
    // al final escribe el reporte en el cuadro de texto. El botón se
    // deshabilita mientras corre para no lanzar dos pruebas a la vez.
    private async void EjecutarBenchmark(object? sender, EventArgs e)
    {
        var boton = (Button)sender!;
        var n = (int)_numBench.Value;
        var rondas = (int)_numRondas.Value;

        boton.Enabled = false;
        _progreso.Visible = true;
        _txtReporte.Clear();

        // Progress<T> entrega los mensajes de avance de vuelta al hilo de la ventana.
        var progreso = new Progress<string>(m => _lblBench.Text = m);
        try
        {
            var informe = await Task.Run(() => Benchmark.Ejecutar(n, rondas, null, progreso));
            _txtReporte.Text = Reporte.Generar(informe);
            _lblBench.Text = "Completado.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OutOfMemoryException)
        {
            MessageBox.Show(this, ex.Message, "Error en el benchmark", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            boton.Enabled = true;
            _progreso.Visible = false;
        }
    }

    // ---------- Utilidades ----------

    // Ejecuta una tarea larga mostrando el cursor de "espera" y atrapando los
    // errores comunes (archivo inexistente, formato incorrecto...) para mostrarlos
    // en un cuadro de mensaje en lugar de cerrar el programa.
    private async Task EjecutarAsync(Func<Task> accion)
    {
        UseWaitCursor = true;
        try
        {
            await accion();
        }
        catch (Exception ex) when (ex is IOException or FormatException or OverflowException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    // Atajo para mostrar un mensaje informativo.
    private void Aviso(string mensaje) =>
        MessageBox.Show(this, mensaje, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
}
