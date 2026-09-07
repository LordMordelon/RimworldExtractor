using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RimworldExtractorInternal.DataTypes;

namespace RimworldExtractorInternal
{
    /// <summary>
    /// Vuelve a extraer y actualizar todos los mods que RML ya tiene traducidos.
    ///
    /// Sirve despues de una actualizacion del juego o de una tanda de mods: cada carpeta se
    /// re-extrae contra la version instalada y se cruza con lo que ya estaba traducido, asi
    /// que sale solo, marcado como TODO, lo que el mod agrego desde la ultima vez.
    /// </summary>
    public static class ActualizacionPorLotes
    {
        /// <summary>De un LoadFolders.Build.yaml saca el primer packageId de la lista.</summary>
        private static readonly Regex PackageIdEnYaml =
            new("PackageID:\\s*\\[\\s*\"([^\"]+)\"", RegexOptions.Compiled);

        /// <summary>Por que se salteo un mod.</summary>
        public enum Motivo
        {
            Actualizado,
            SinPackageId,
            NoInstalado,
            NadaQueExtraer,
            Fallo
        }

        /// <summary>Como le fue a un mod.</summary>
        public readonly record struct Renglon(
            string Carpeta, Motivo Motivo, string Detalle,
            int Conservadas, int Pendientes, int SinUso,
            List<TranslationEntry> Rescatadas);

        /// <summary>
        /// Recorre Data/ y actualiza cada carpeta que tenga su LoadFolders.Build.yaml.
        ///
        /// Un mod que falla no corta la corrida: se anota y se sigue. Al terminar, quien llama
        /// decide si regenera el indice, que conviene hacer una sola vez.
        /// </summary>
        /// <param name="rmlPath">Raiz del clon de RML.</param>
        /// <param name="avisar">Se llama antes de cada mod: numero, total y nombre.</param>
        /// <param name="filtro">
        /// Si se pasa, solo se actualizan las carpetas para las que devuelve true. Sirve para
        /// correr una tanda en vez de todo.
        /// </param>
        public static List<Renglon> Correr(string rmlPath, Action<int, int, string>? avisar = null,
            Func<string, bool>? filtro = null)
        {
            var renglones = new List<Renglon>();
            var data = Path.Combine(rmlPath, "Data");
            if (!Directory.Exists(data))
                return renglones;

            // Se busca en todo el arbol, no solo en el primer nivel: las carpetas de Data se
            // pueden agrupar en subcarpetas y el builder de RML las encuentra igual. Es lo
            // mismo que hace Statics.FindAndValidatePaths de su lado.
            var carpetas = Directory
                .GetFiles(data, LoadFoldersBuild.FileName, SearchOption.AllDirectories)
                .Select(x => Path.GetDirectoryName(x)!)
                .Where(x => filtro is null || filtro(Path.GetFileName(x)))
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // El indice de packageId se arma una sola vez: resolverlo por carpeta significaria
            // releer cientos de About.xml doscientas veces.
            var porPackageId = new Dictionary<string, ModMetadata>(StringComparer.OrdinalIgnoreCase);
            foreach (var mod in ModLister.AllMods)
            {
                if (!string.IsNullOrWhiteSpace(mod.PackageId))
                    porPackageId.TryAdd(mod.PackageId.Trim(), mod);
            }

            for (var i = 0; i < carpetas.Count; i++)
            {
                var carpeta = carpetas[i];
                var nombre = Path.GetFileName(carpeta);
                avisar?.Invoke(i + 1, carpetas.Count, nombre);

                try
                {
                    renglones.Add(Actualizar(carpeta, nombre, rmlPath, porPackageId));
                }
                catch (Exception e)
                {
                    // Un mod roto no puede llevarse por delante las otras doscientas.
                    renglones.Add(new Renglon(nombre, Motivo.Fallo, e.Message, 0, 0, 0, new List<TranslationEntry>()));
                    Log.Wrn(Strings.BatchModFailed(nombre, e.Message));
                }
            }

            return renglones;
        }

        private static Renglon Actualizar(string carpeta, string nombre, string rmlPath,
            Dictionary<string, ModMetadata> porPackageId)
        {
            var yaml = File.ReadAllText(Path.Combine(carpeta, LoadFoldersBuild.FileName));
            var match = PackageIdEnYaml.Match(yaml);
            if (!match.Success)
                return new Renglon(nombre, Motivo.SinPackageId, string.Empty, 0, 0, 0, new List<TranslationEntry>());

            var packageId = match.Groups[1].Value.Trim();
            if (!porPackageId.TryGetValue(packageId, out var mod))
                return new Renglon(nombre, Motivo.NoInstalado, packageId, 0, 0, 0, new List<TranslationEntry>());

            var extraibles = ModLister.GetExtractableFolders(mod).Where(x => x.IsAutoSelectable()).ToList();
            if (extraibles.Count == 0)
                return new Renglon(nombre, Motivo.NadaQueExtraer, string.Empty, 0, 0, 0, new List<TranslationEntry>());

            // Con las referencias resueltas, como hace «Extraccion completa». Sin esto, un mod
            // que se apoya en un framework sale con la mitad del texto sin resolver.
            var referencias = ModLister.FindAllReferenceMods(mod)
                .Concat(ModLister.FindModsNamedInPatches(extraibles))
                .Distinct()
                .ToList();

            var extraccion = Extractor.ExtractTranslationData(mod, extraibles, referencias);
            if (extraccion.Count == 0)
                return new Renglon(nombre, Motivo.NadaQueExtraer, string.Empty, 0, 0, 0, new List<TranslationEntry>());

            var r = ActualizacionRml.Escribir(mod, extraccion, rmlPath);
            return new Renglon(nombre, Motivo.Actualizado, string.Empty,
                r.Conservadas, r.Pendientes, r.SinUso, r.Rescatadas);
        }
    }
}
