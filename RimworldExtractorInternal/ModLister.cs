using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using RimworldExtractorInternal.DataTypes;

namespace RimworldExtractorInternal
{
    public static class ModLister
    {
        public static IEnumerable<string> ModRootsOfficial
        {
            get
            {
                var dirOfficial = Path.Combine(Prefabs.PathRimworld, "Data");
                if (Directory.Exists(dirOfficial))
                    foreach (var dir in Directory.EnumerateDirectories(dirOfficial))
                        yield return dir;
            }
        }

        public static IEnumerable<string> ModRootsLocal
        {
            get
            {
                var dirLocalMods = Path.Combine(Prefabs.PathRimworld, "Mods");
                if (Directory.Exists(dirLocalMods))
                    foreach (var dir in Directory.EnumerateDirectories(dirLocalMods))
                        yield return dir;
            }
        }

        public static IEnumerable<string> ModRootsWorkshop
        {
            get
            {
                var dirWorkshopMods = Prefabs.PathWorkshop;
                if (Directory.Exists(dirWorkshopMods))
                    foreach (var dir in Directory.EnumerateDirectories(dirWorkshopMods))
                        yield return dir;
            }
        }

        public static IEnumerable<string> ModRootsAll => ModRootsOfficial.Concat(ModRootsLocal).Concat(ModRootsWorkshop);
        public static IEnumerable<ModMetadata> OfficialMods => ModRootsOfficial.Select(GetModMetadataByModRoot).OrderBy(x => x.ModName);

        public static IEnumerable<ModMetadata> LocalMods
        {
            get
            {
                if (LocalModsCache == null)
                {
                    LocalModsCache = ModRootsLocal.Select(GetModMetadataByModRoot).OrderBy(x => x.ModName).ToList();
                }

                return LocalModsCache;
            }
        }

        public static IEnumerable<ModMetadata> WorkshopMods
        {
            get
            {
                if (WorkshopModsCache == null)
                {
                    WorkshopModsCache = ModRootsWorkshop.Select(GetModMetadataByModRoot).OrderBy(x => x.ModName)
                        .ToList();
                }

                return WorkshopModsCache;
            }
        }
        public static IEnumerable<ModMetadata> AllMods => OfficialMods.Concat(LocalMods).Concat(WorkshopMods);

        public static void ResetCache()
        {
            WorkshopModsCache = null;
            LocalModsCache = null;
        }

        public static ModMetadata GetModMetadataByModRoot(string modRoot)
        {
            var pathAbout = Path.Combine(modRoot, "About", "About.xml");
            string name = "UNKNOWN";
            string packageId = "UNKNOWN";
            string author = "";
            var modDependencies = new List<string>();
            if (File.Exists(pathAbout))
            {
                try
                {
                    var doc = new XmlDocument();
                    doc.LoadXml(File.ReadAllText(pathAbout));
                    packageId = doc.DocumentElement?["packageId"]?.InnerText ?? "UNKNOWN";
                    name = doc.DocumentElement?["name"]?.InnerText ?? "UNKNOWN";
                    author = LeerAutor(doc);
                    if (name == "UNKNOWN")
                    {
                        // Official Contents
                        if (doc.DocumentElement?["author"]?.InnerText == "Ludeon Studios")
                        {
                            name = Path.GetFileName(modRoot).Trim();
                            return new ModMetadata(modRoot, "Official", name, packageId, true);
                        }
                    }

                    if (doc.DocumentElement?["modDependencies"] != null)
                    {
                        foreach (XmlNode childNode in doc.DocumentElement["modDependencies"]!.ChildNodes)
                        {
                            var packageIdModDependencies = childNode["packageId"];
                            if (packageIdModDependencies != null)
                                modDependencies.Add(packageIdModDependencies.InnerText);
                        }
                    }

                    if (doc.DocumentElement?["modDependenciesByVersion"] != null)
                    {
                        var nodes = doc.DocumentElement["modDependenciesByVersion"]?["v" + Prefabs.CurrentVersion]
                            ?.ChildNodes;
                        nodes ??= doc.DocumentElement["modDependenciesByVersion"]?.LastChild?.ChildNodes;
                        if (nodes != null)
                        {
                            foreach (XmlNode childNode in nodes)
                            {
                                var packageIdModDependencies = childNode["packageId"];
                                if (packageIdModDependencies != null)
                                    modDependencies.Add(packageIdModDependencies.InnerText);
                            }
                        }
                    }


                }
                catch (Exception e)
                {
                    Log.Err(Strings.CouldNotReadAboutXml(pathAbout, e.Message));
                }
            }

            var pathPublishedFileId = Path.Combine(modRoot, "About", "PublishedFileId.txt");
            var id = "???";
            if (File.Exists(pathPublishedFileId))
            {
                id = File.ReadAllText(pathPublishedFileId).Trim();
            }
            else if (modRoot.Contains("workshop\\content\\294100"))
            {
                id = Path.GetFileName(modRoot);
            }

            modDependencies = modDependencies.Distinct().ToList();
            return new ModMetadata(modRoot, id, name, packageId, false, modDependencies) { Author = author };
        }

        /// <summary>
        /// El autor declarado en un About.xml. RimWorld acepta las dos formas —&lt;author&gt; con
        /// uno solo, o &lt;authors&gt; con una lista— y hay mods que usan cada una, asi que leer
        /// solo la primera dejaria sin autor a una parte del Workshop.
        ///
        /// Devuelve el texto crudo, incluida la lista entera cuando son varios: normalizarlo
        /// es tarea de <see cref="Agrupador.AutorPrincipal"/>, que es quien decide como se
        /// agrupa.
        /// </summary>
        private static string LeerAutor(XmlDocument doc)
        {
            var uno = doc.DocumentElement?["author"]?.InnerText.Trim();
            if (!string.IsNullOrWhiteSpace(uno))
                return uno;

            var varios = doc.DocumentElement?["authors"];
            if (varios == null)
                return "";

            var nombres = varios.ChildNodes
                .Cast<XmlNode>()
                .Select(x => x.InnerText.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x));

            return string.Join(", ", nombres);
        }

        public static List<ExtractableFolder> GetExtractableFolders(ModMetadata modMetadata)
        {
            var root = modMetadata.RootDir;

            var sets = new HashSet<ExtractableFolder>(new ExtractableFolderComparer());
            var pathLoadFolders = Path.Combine(root, "LoadFolders.xml");
            string[] targetFolders = {
                "Defs", "Patches", "Keyed", 
                Path.Combine("Languages", Prefabs.OriginalLanguage, "Keyed"),
                Path.Combine("Languages", Prefabs.OriginalLanguage.Split(' ').First(), "Keyed"),
                Path.Combine("Languages", Prefabs.OriginalLanguage, "Strings"),
                Path.Combine("Languages", Prefabs.OriginalLanguage.Split(' ').First(), "Strings")
            };

            IEnumerable<string> GetExtractableFoldersInternal(string path)
            {
                foreach (var folder in targetFolders)
                {
                    var subDir = Path.Combine(path, folder);
                    if (Directory.Exists(subDir))
                    {
                        yield return Path.GetRelativePath(root, subDir);
                    }
                }
            }

            foreach (var extractableFolder in GetExtractableFoldersInternal(root).Select(x => new ExtractableFolder(modMetadata, x, null)))
            {
                sets.Add(extractableFolder);
            }

            if (File.Exists(pathLoadFolders))
            {
                var doc = new XmlDocument();
                doc.LoadXml(File.ReadAllText(pathLoadFolders));
                foreach (XmlNode node in doc.DocumentElement!.ChildNodes)
                {
                    var name = node.Name;
                    foreach (XmlNode li in node.ChildNodes)
                    {
                        var requiredPackageIds = li.Attributes?["IfModActive"]?.Value;
                        foreach (var extractableFolder in GetExtractableFoldersInternal(Path.Combine(root, li.InnerText))
                                     .Select(x => new ExtractableFolder(modMetadata, x, requiredPackageIds, name[1..])))
                        {
                            sets.Add(extractableFolder);
                        }
                    }
                }
            }
            else
            {
                foreach (var directory in Directory.EnumerateDirectories(root))
                {
                    var lastDir = Path.GetFileName(directory);
                    if (Regex.IsMatch(lastDir, Prefabs.PatternVersion))
                    {
                        foreach (var extractableFolder in GetExtractableFoldersInternal(directory)
                                     .Select(x => new ExtractableFolder(modMetadata, x, null, lastDir)))
                        {
                            sets.Add(extractableFolder);
                        }
                    }
                }

                var commonDir = Path.Combine(root, "Common");
                if (Directory.Exists(commonDir))
                {
                    foreach (var extractableFolder in GetExtractableFoldersInternal(commonDir).Select(x => new ExtractableFolder(modMetadata, x, null, "Common")))
                    {
                        sets.Add(extractableFolder);
                    }
                }
            }

            return sets.ToList();
        }

        public static IEnumerable<ModMetadata> FindAllReferenceMods(ModMetadata target)
        {
            foreach (var officialMod in OfficialMods.Where(official => official != target))
            {
                yield return officialMod;
            }

            var set = new HashSet<ModMetadata>();
            ModMetadataByPackageIdLookUp.Clear();

            IEnumerable<ModMetadata> FindAllReferenceModsInternal(ModMetadata modMetadata)
            {
                if (modMetadata.ModDependencies != null)
                {
                    foreach (var modDependency in modMetadata.ModDependencies)
                    {
                        var b = TryGetModMetadataByPackageId(modDependency, out var possible);
                        if (possible != null)
                            yield return possible;
                    }
                }

                foreach (var extractableFolder in GetExtractableFolders(modMetadata))
                {
                    if (extractableFolder.RequiredPackageId != null)
                    {
                        foreach (var modDependency in extractableFolder.RequiredPackageId.Split(','))
                        {
                            var b = TryGetModMetadataByPackageId(modDependency, out var possible);
                            if (possible != null)
                                yield return possible;
                        }
                    }
                }
            }

            IEnumerable<ModMetadata> FindAllReferenceModsRecursive(ModMetadata modMetadata)
            {
                foreach (var child in FindAllReferenceModsInternal(modMetadata))
                {
                    if (set.Contains(child))
                        continue;
                    yield return child;
                    set.Add(child);
                    foreach (var childchild in FindAllReferenceModsRecursive(child))
                    {
                        yield return childchild;
                        set.Add(childchild);
                    }
                }
            }

            foreach (var modMetadata in FindAllReferenceModsRecursive(target))
            {
                yield return modMetadata;
            }

            ModMetadataByPackageIdLookUp.Clear();
        }

        public static bool IsAutoSelectable(this ExtractableFolder extractableFolder)
        {
            return extractableFolder.VersionInfo is "default" or "Common" ||
                   extractableFolder.VersionInfo == Prefabs.CurrentVersion;
        }

        internal static bool TryGetModMetadataByPackageId(string? packageId, out ModMetadata? modMetadata)
        {
            if (packageId == null)
            {
                modMetadata = null;
                return false;
            }
            if (ModMetadataByPackageIdLookUp.TryGetValue(packageId, out var value))
            {
                modMetadata = value;
                return value != null;
            }


            var matches = AllMods.Where(x => string.Equals(x.PackageId, packageId, StringComparison.CurrentCultureIgnoreCase)).ToList();
            switch (matches.Count)
            {
                case < 1:
                    modMetadata = null;
                    ModMetadataByPackageIdLookUp[packageId] = null;
                    Log.Wrn(Strings.ModNotFoundByPackageId(packageId));
                    return false;
                case 1:
                    modMetadata = matches[0];
                    ModMetadataByPackageIdLookUp[packageId] = modMetadata;
                    return true;
                case > 1:
                    modMetadata = matches[0];
                    ModMetadataByPackageIdLookUp[packageId] = modMetadata;
                    Log.Msg(Strings.DuplicatePackageId(packageId, matches.Count));
                    return true;
            }
        }

        /// <summary>
        /// Los mods a los que apuntan los patches de estas carpetas.
        ///
        /// Un patch que modifica algo de otro mod se envuelve en PatchOperationFindMod, que
        /// nombra a ese mod. O sea que el propio mod declara de que necesita los defs para
        /// que sus patches se puedan extraer, y no hace falta adivinarlo.
        ///
        /// Los nombres que no correspondan a un mod instalado se descartan: no hay nada que
        /// cargar y no es un error, simplemente ese patch no aplica en esta maquina.
        /// </summary>
        public static IEnumerable<ModMetadata> FindModsNamedInPatches(IEnumerable<ExtractableFolder> patchFolders)
        {
            var nombres = new HashSet<string>();

            foreach (var carpeta in patchFolders.Where(x => Path.GetFileName(x.FolderName) == "Patches"))
            {
                foreach (var filePath in IO.DescendantFiles(carpeta.FullPath)
                             .Where(x => x.ToLower().EndsWith(".xml")))
                {
                    try
                    {
                        var doc = IO.ReadXml(filePath);
                        foreach (XmlNode mods in doc.SelectNodes("//Operation[@Class='PatchOperationFindMod']/mods")!)
                        {
                            foreach (XmlNode li in mods.ChildNodes)
                            {
                                var nombre = li.InnerText.Trim();
                                if (!string.IsNullOrEmpty(nombre))
                                    nombres.Add(nombre);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Wrn(Strings.ErrorReadingFileColon(filePath, e.Message));
                    }
                }
            }

            foreach (var nombre in nombres)
            {
                var mod = GetModMetadataByModName(nombre);
                if (mod != null)
                    yield return mod;
            }
        }

        internal static ModMetadata? GetModMetadataByModName(string modName)
        {
            if (ModMetadataByModNameLookUp.TryGetValue(modName, out var value))
            {
                return value;
            }
            var result = AllMods.FirstOrDefault(x => string.Equals(x.ModName, modName, StringComparison.CurrentCultureIgnoreCase));
            ModMetadataByModNameLookUp.Add(modName, result);
            return result;
        }

        private static readonly Dictionary<string, ModMetadata?> ModMetadataByPackageIdLookUp = new();
        private static readonly Dictionary<string, ModMetadata?> ModMetadataByModNameLookUp = new();
        private static List<ModMetadata>? LocalModsCache = null;
        private static List<ModMetadata>? WorkshopModsCache = null;
    }
}
