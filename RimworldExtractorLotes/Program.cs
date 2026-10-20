using RimworldExtractorInternal;

// «Actualizar todo RML» sin la ventana. Cada argumento suelto es el nombre de una carpeta de
// Data/ o su comienzo; sin ninguno se actualizan todas. Es lo mismo que hace el boton, con
// ActualizacionPorLotes.Correr, y por eso no reimplementa nada: si algo cambia ahi, cambia aca.
//
//   dotnet run --project RimworldExtractorLotes -c Release -- [--rml <ruta>] [--indice] [carpeta...]
//
// --rml     la carpeta de RML. Si no se da, ../RML desde donde se corre, que es el clon
//           hermano cuando se corre desde RimworldExtractor/.
// --indice  al terminar agrupa por autor y regenera el indice, como el boton. Sin esto queda
//           para despues, y el LoadFolders.xml no ve una carpeta que se haya renombrado.

var rml = Path.GetFullPath(Path.Combine("..", "RML"));
var indice = false;
var carpetas = new List<string>();

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--rml" when i + 1 < args.Length:
            rml = Path.GetFullPath(args[++i]);
            break;
        case "--indice":
            indice = true;
            break;
        case "-h" or "--help" or "--rml":
            Console.WriteLine(Strings.LotesUso);
            return 2;
        default:
            carpetas.Add(args[i]);
            break;
    }
}

if (!Directory.Exists(Path.Combine(rml, "Data")))
{
    Console.WriteLine(Strings.LotesSinRml(rml));
    return 2;
}

// Prefabs.dat se busca junto al ejecutable, y aca no hay uno: quedan los valores por defecto
// (RimWorld y el Workshop en D:\SteamLibrary), que son los de esta maquina.
Prefabs.Init();
Prefabs.PathRml = rml;

Func<string, bool>? filtro = carpetas.Count == 0
    ? null
    : nombre => carpetas.Any(x => nombre.StartsWith(x, StringComparison.OrdinalIgnoreCase));

var renglones = ActualizacionPorLotes.Correr(rml,
    (i, n, nombre) => Log.Msg(Strings.BatchProgress(i, n, nombre)), filtro);

if (renglones.Count == 0)
{
    Console.WriteLine(Strings.LotesNingunaCarpeta);
    return 1;
}

if (indice)
{
    Agrupador.Reagrupar(rml);
    LoadFoldersBuild.Regenerar(rml);
}

foreach (var r in renglones)
    Console.WriteLine(Strings.LotesRenglon(r.Carpeta, r.Motivo.ToString(), r.Detalle,
        r.Conservadas, r.Pendientes, r.SinUso, r.Rescatadas.Count));

return renglones.Any(x => x.Motivo == ActualizacionPorLotes.Motivo.Fallo) ? 1 : 0;
