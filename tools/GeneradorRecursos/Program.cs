// Genera los recursos embebidos de PasheCPE desde el Excel de reglas de SUNAT.
// Uso: dotnet run --project tools/GeneradorRecursos [-- <raíz del repositorio>]
using PasheCPE.GeneradorRecursos;

var raiz = args.Length > 0 ? Path.GetFullPath(args[0]) : Generador.BuscarRaiz(Directory.GetCurrentDirectory());
var resultado = Generador.Ejecutar(raiz);

var actualizados = 0;
foreach (var (ruta, contenido) in resultado.Archivos)
{
    var destino = Path.Combine(raiz, ruta);
    if (File.Exists(destino) && File.ReadAllBytes(destino).AsSpan().SequenceEqual(contenido))
    {
        continue;
    }

    Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
    File.WriteAllBytes(destino, contenido);
    Console.WriteLine($"Actualizado: {ruta}");
    actualizados++;
}

// Catálogos XML que ya no existen en el Excel.
var carpetaXml = Path.Combine(raiz, Generador.CarpetaRecursos, "catalogos");
foreach (var archivo in Directory.EnumerateFiles(carpetaXml, "cat_*.xml"))
{
    var ruta = Path.GetRelativePath(raiz, archivo).Replace('\\', '/');
    if (!resultado.Archivos.ContainsKey(ruta))
    {
        File.Delete(archivo);
        Console.WriteLine($"Eliminado: {ruta}");
        actualizados++;
    }
}

foreach (var linea in resultado.Resumen)
{
    Console.WriteLine(linea);
}

Console.WriteLine(actualizados == 0 ? "Sin cambios." : $"Archivos actualizados: {actualizados}.");
return 0;
