using System.Text;
using ListasDobles.Consola;
using ListasDobles.Core;

// ============================================================================
// PUNTO DE ENTRADA DE LA VERSIÓN CONSOLA
// ----------------------------------------------------------------------------
// Aquí arranca el programa de texto. Solo hace tres cosas:
//   1) Pide a la consola que use UTF-8 (para mostrar tildes y la ñ bien).
//   2) Crea el GestorListas (el coordinador con las 3 listas).
//   3) Entrega el control al menú interactivo.
// ============================================================================
Console.OutputEncoding = Encoding.UTF8;
new Menu(new GestorListas()).Ejecutar();
