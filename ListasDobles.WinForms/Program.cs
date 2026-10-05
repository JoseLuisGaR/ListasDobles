namespace ListasDobles.WinForms;

// ============================================================================
// PUNTO DE ENTRADA DE LA VERSIÓN WINDOWS FORMS
// ----------------------------------------------------------------------------
// Aquí arranca la aplicación con ventanas. Prepara la configuración visual
// estándar de Windows y abre la ventana principal (MainForm).
// [STAThread] es un requisito técnico de Windows para que funcionen los
// cuadros de diálogo de abrir/guardar archivos.
// ============================================================================
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();   // Configuración visual por defecto.
        Application.Run(new MainForm());         // Abre la ventana y espera a que se cierre.
    }
}
