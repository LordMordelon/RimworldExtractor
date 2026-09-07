using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using RimworldExtractorInternal.Compats;
using RimworldExtractorInternal.DataTypes;

namespace RimworldExtractorInternal
{
    public static partial class Extractor
    {
        internal static XmlDocument? CombinedDefs;

        /// <summary>
        /// La base de defs tal como quedo al terminar de extraer los defs, antes de que el
        /// paso de patches la reemplace. Ver <see cref="ConLaBaseCompleta{T}"/>.
        /// </summary>
        private static XmlDocument? _defsCompletos;

        /// <summary>
        /// Toma nota de la base completa. Se llama al terminar de extraer los defs, que es el
        /// unico momento en que CombinedDefs los tiene todos.
        /// </summary>
        internal static void RegistrarBaseCompleta(XmlDocument? defs) => _defsCompletos = defs;

        /// <summary>
        /// Corre algo con la base de defs completa puesta, y despues deja todo como estaba.
        ///
        /// Hace falta para releer los Patches ya traducidos de RML. Esa lectura no parsea los
        /// archivos: corre ExtractPatches, que evalua cada xpath contra CombinedDefs. Y cuando
        /// se la llama —despues de extraer el mod— CombinedDefs ya no es la base completa, sino
        /// el documento reducido que deja DoXmlInheritance con los defs que tocaron los patches
        /// del propio mod. Los xpath de RML no encuentran nada ahi, asi que la traduccion vieja
        /// se vuelve invisible: sale todo como TODO y ni siquiera queda apartada en UNUSED.
        ///
        /// Andaba o no andaba segun si los defs que RML parchea caian por casualidad entre los
        /// que tocaron los patches del mod.
        /// </summary>
        internal static T ConLaBaseCompleta<T>(Func<T> leer)
        {
            if (_defsCompletos == null)
                return leer();

            var previo = CombinedDefs;
            var xpathsPrevios = PatchOperations.XpathsSinObjetivo.ToList();

            // Se clona: PatchOperations aplica las operaciones sobre el documento, asi que sin
            // copia la primera lectura ensuciaria la base y la siguiente ya no serviria.
            CombinedDefs = (XmlDocument)_defsCompletos.CloneNode(true);
            try
            {
                return leer();
            }
            finally
            {
                CombinedDefs = previo;

                // Los xpath de RML no son fallos del mod: no tienen que entrar en el aviso de
                // «patches sin objetivo».
                PatchOperations.XpathsSinObjetivo.Clear();
                PatchOperations.XpathsSinObjetivo.AddRange(xpathsPrevios);
            }
        }
        public static readonly Dictionary<string, XmlNode> ParentNodeLookUp = new();

        private static bool _isOfficialContent = false;

        public static List<TranslationEntry> ExtractTranslationData(ModMetadata modMetadata, List<ExtractableFolder> selectedFolders, List<ModMetadata>? referenceMods)
        {
            if (modMetadata.IsOfficialContent)
                _isOfficialContent = true;

            var refDefs = new List<ReferenceDefsRoot>();
            var prePatches = new List<ExtractableFolder>();
            if (referenceMods != null)
            {
                foreach (var referenceMod in referenceMods)
                {
                    refDefs.AddRange(from extractableFolder in ModLister.GetExtractableFolders(referenceMod)
                        where (extractableFolder.VersionInfo == "default" ||
                               extractableFolder.VersionInfo == "Common" ||
                               extractableFolder.VersionInfo == Prefabs.CurrentVersion)
                              && Path.GetFileName(extractableFolder.FolderName) == "Defs"
                        select new ReferenceDefsRoot(referenceMod.ModName.Trim(),
                            Path.Combine(referenceMod.RootDir, extractableFolder.FolderName)));
                    prePatches.AddRange(ModLister.GetExtractableFolders(referenceMod).Where(x =>
                        (x.VersionInfo == "default" || x.VersionInfo == "Common" ||
                         x.VersionInfo == Prefabs.CurrentVersion) && Path.GetFileName(x.FolderName) == "Patches"));
                }
            }

            var extraction = new List<TranslationEntry>();
            Reset();
            var defs = selectedFolders.Where(x => Path.GetFileName(x.FolderName) == "Defs").ToList();
            if (defs.Count > 0)
            {
                prePatches.AddRange(selectedFolders.Where(x => Path.GetFileName(x.FolderName) == "Patches").ToList());
                PrepareDefs(defs, refDefs, prePatches);
                extraction.AddRange(ExtractDefs());

                // Aca la base de defs esta completa. Mas adelante, al procesar los patches,
                // se reemplaza por una que solo tiene los defs que estos agregaron.
                PatchesSinObjetivo.RegistrarDefsCargados(CombinedDefs);
                RegistrarBaseCompleta(CombinedDefs);
            }
            foreach (var extractableFolder in selectedFolders)
            {
                switch (Path.GetFileName(extractableFolder.FolderName))
                {
                    case "Defs":
                        break;
                    case "Keyed":
                        extraction.AddRange(ExtractKeyed(extractableFolder));
                        break;
                    case "Strings":
                        extraction.AddRange(ExtractStrings(extractableFolder));
                        break;
                    case "Patches":
                        extraction.AddRange(ExtractPatches(extractableFolder));
                        break;
                    default:
                        Log.Wrn(Strings.UnsupportedFolder(extractableFolder.FolderName));
                        continue;
                }
            }

            var set = new HashSet<(string, string)>();
            foreach (var entry in extraction)
            {
                var tuple = (entry.ClassName + "+" + entry.Node, entry.Original);
                var pair = set.FirstOrDefault(x => x.Item1 == tuple.Item1);
                if (pair != default)
                {
                    if (pair.Item2 != entry.Original)
                    {
                        Log.Err(
                            Strings.DuplicateNodeWithDifferentOriginal(entry.ClassName, entry.Node, pair.Item2, entry.Original));
                    }
                }

                set.Add(tuple);
            }

            _isOfficialContent = false;

            // Si algun patch apunto a un def que no estaba cargado, se dice: la extraccion
            // termina bien igual, solo que con menos texto del que deberia.
            PatchesSinObjetivo.Informar(modMetadata);

            // Si el mod ya viene traducido a este idioma, se dice. No cambia la extraccion
            // —el original sigue saliendo del ingles—, pero puede haber ahi trabajo hecho
            // del que partir.
            TraduccionPropia.Informar(modMetadata);

            return extraction.DistinctBy(x => $"{x.ClassName}+{x.Node}").ToList();
        }

        private static void Reset()
        {
            // Una vez por extraccion, no por carpeta de patches: un mod puede tener varias
            // y solo quedarian registrados los fallos de la ultima.
            PatchOperations.XpathsSinObjetivo.Clear();
            DuenioPorDefName.Clear();

            // La base completa es de la extraccion que arranca, no de la anterior: releer los
            // patches contra los defs de otro mod daria cualquier cosa.
            _defsCompletos = null;

            CombinedDefs = new XmlDocument();
            CombinedDefs.AppendElement("Defs");
            ParentNodeLookUp.Clear();
        }

        private static void PrepareDefs(List<ExtractableFolder> extracableFolders, List<ReferenceDefsRoot>? referenceDefsRoots, List<ExtractableFolder> prePatches)
        {

            if (CombinedDefs == null)
                Reset();

            if (referenceDefsRoots != null) LoadReferenceDefs(referenceDefsRoots);

            extracableFolders.ForEach(extractableFolder =>
            {
                var defsRoot = extractableFolder.FullPath;
                var requiredPackageId = extractableFolder.RequiredPackageId;
                foreach (var filePath in IO.DescendantFiles(defsRoot).Where(x => x.ToLower().EndsWith(".xml")))
                {
                    try
                    {
                        var fileName = Path.GetFileNameWithoutExtension(filePath);
                        var childDoc = IO.ReadXml(filePath);

                        foreach (XmlNode node in childDoc.DocumentElement!.ChildNodes)
                        {
                            var newNode = CombinedDefs!.ImportNode(node, true);

                            if (requiredPackageId != null)
                            {
                                newNode.AppendAttribute("RequiredPackageId", requiredPackageId);
                            }

                            if (_isOfficialContent)
                            {
                                newNode.AppendAttribute("SourceFile", fileName);
                            }
                            CombinedDefs.DocumentElement!.AppendChild(newNode);
                            var attributeName = node.Attributes?["Name"]?.Value;
                            if (attributeName != null)
                            {
                                ParentNodeLookUp[attributeName] = newNode;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Err(Strings.ErrorReadingFile(filePath, e.Message));
                    }
                }
            });

            DoPrePatch(prePatches);

            DoXmlInheritance();
        }

        private static void DoPrePatch(List<ExtractableFolder> prePatches)
        {
            foreach (var patchDir in prePatches)
            {
                foreach (var entry in ExtractPatches(patchDir, true))
                {
                    // do nothing
                }
            }

            foreach (XmlNode node in CombinedDefs!.DocumentElement!.ChildNodes)
            {
                var attributeName = node.Attributes?["Name"]?.Value;
                if (attributeName != null)
                {
                    ParentNodeLookUp[attributeName] = node;
                }
            }
        }

        internal static IEnumerable<TranslationEntry> ExtractDefs()
        {
            var rawExtraction = ExtractDefsInternal().ToList();

            // Aca habia dos residuos de depuracion. Un Console.WriteLine por cada entrada
            // extraida, que en la ventana no se ve —es WinExe, no hay consola— y en una corrida
            // por lotes son cientos de miles de lineas a stdout; y un CombinedDefs.Save a
            // "test.xml", que en cada mod escribia la base de defs entera al directorio actual.
            // Son 15 MB por mod: casi cuatro gigas en una corrida de doscientos cincuenta, en un
            // archivo sin seguimiento que ademas casi termina commiteado. Para inspeccionar la
            // base conviene un breakpoint, que no deja nada atras.

            foreach (var entry in CompatManager.DoPostProcessing(rawExtraction))
            {
                yield return entry;
            }
        }
        private static IEnumerable<TranslationEntry> ExtractDefsInternal()
        {
            if (CombinedDefs == null)
            {
                throw new InvalidOperationException("You need to call PrepareDefs first");
            }

            CompatManager.DoPreProcessing(CombinedDefs);

            foreach (XmlNode node in CombinedDefs.DocumentElement!.ChildNodes.OfType<XmlNode>()
                         .Where(x => x.Attributes?["Reference"]?.Value.ToLower() != "true"))
            {
                var defName = node["defName"]?.InnerText;
                if (defName == null)
                {
                    if (node.Name != "SongDef")
                        Log.Wrn(Strings.DefNameTagNotFound(node.Name, node.InnerXml));
                    continue;
                }


                var requiredMods = new RequiredMods();
                var requiredPackageIds = node.Attributes?["RequiredPackageId"]?.Value.Split(',');
                if (requiredPackageIds != null)
                {
                    requiredMods.AddAllowedByPackageIds(requiredPackageIds);
                }


                // Los motes son los iconos que flotan sobre el colono. Su etiqueta no se le
                // muestra nunca al jugador —la categoria Mote no es seleccionable, no va a
                // inventarios ni al menu de construccion—, asi que traducirla no cambia nada.
                //
                // Salian igual porque casi ningun mote declara etiqueta: la heredan de MoteBase,
                // que trae <label>Mote</label>, y la extraccion resuelve la herencia. Eran 248
                // entradas en 43 mods de RML, todas diciendo "Mote".
                //
                // Ni Ludeon las traduce: su propio SpanishLatin trae las 62 del juego base con
                // el valor en ingles.
                //
                // Se mira la categoria y no el ParentName porque las cadenas de herencia varian
                // —MoteBase, MoteGlowDistorted, InteractionMoteBase, y las bases que cada mod se
                // arma—, pero la categoria la heredan todas.
                if (node["category"]?.InnerText.Trim() == "Mote")
                    continue;

                var className = node.Attributes?["Class"]?.Value ?? node.Name;
                className = className[..1].ToUpper() + className[1..];

                foreach (var translationEntry in FindExtractableNodes(defName, className, node))
                {
                    yield return translationEntry with
                    {
                        RequiredMods = translationEntry.RequiredMods + requiredMods
                    };
                }
            }
        }

        internal static IEnumerable<TranslationEntry> ExtractKeyed(ExtractableFolder keyed)
        {
            var keyedRoot = keyed.FullPath;
            RequiredMods? requiredMods = null;
            if (keyed.RequiredPackageId == null)
            {
                requiredMods = null;
            }
            else
            {
                requiredMods = new RequiredMods();
                requiredMods.AddAllowedByPackageIds(keyed.RequiredPackageId.Split(','));
            }
            
            foreach (var filePath in IO.DescendantFiles(keyedRoot).Where(x => x.ToLower().EndsWith(".xml")))
            {
                var fileName = Path.GetFileNameWithoutExtension(filePath);

                var doc = IO.ReadXml(filePath);
                foreach (XmlNode node in doc.DocumentElement!.ChildNodes)
                {
                    yield return new TranslationEntry("Keyed", node.Name, node.InnerText, null, requiredMods,
                        _isOfficialContent ? fileName : null);
                }
            }
        }

        internal static IEnumerable<TranslationEntry> ExtractStrings(ExtractableFolder strings)
        {
            var stringsRoot = strings.FullPath;
            RequiredMods? requiredMods = null;
            if (strings.RequiredPackageId == null)
            {
                requiredMods = null;
            }
            else
            {
                requiredMods = new RequiredMods();
                requiredMods.AddAllowedByPackageIds(strings.RequiredPackageId.Split(','));
            }
            foreach (var filePath in IO.DescendantFiles(stringsRoot).Where(x => x.ToLower().EndsWith(".txt")))
            {
                var nodeName = Path.GetRelativePath(stringsRoot, filePath);
                nodeName = Path.GetFileNameWithoutExtension(nodeName.Replace('\\', '.'));

                var lines = File.ReadAllLines(filePath);
                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    yield return new TranslationEntry("Strings", $"{nodeName}.{i}", line, null, requiredMods, null);
                }
            }
        }


        internal static IEnumerable<TranslationEntry> ExtractPatches(ExtractableFolder patches, bool prePatchMode = false)
        {
            var rawExtraction = ExtractPatchesInternal(patches, prePatchMode).ToList();
            foreach (var entry in CompatManager.DoPostProcessing(rawExtraction))
            {
                yield return entry;
            }
        }

        private static IEnumerable<TranslationEntry> ExtractPatchesInternal(ExtractableFolder patches, bool prePatchMode = false)
        {
            if (CombinedDefs == null)
            {
                Log.Err($"{nameof(CombinedDefs)} is null. should call ExtractDefs() first before call ExtractPatches().");
                yield break;
            }

            PatchOperations.DefsAddedByPatches.Clear();

            var patchesRoot = patches.FullPath;

            RequiredMods? requiredMods = null;
            if (patches.RequiredPackageId == null)
            {
                requiredMods = null;
            }
            else
            {
                requiredMods = new RequiredMods();
                requiredMods.AddAllowedByPackageIds(patches.RequiredPackageId.Split(','));
            }

            var doc = new XmlDocument();
            doc.AppendElement("Patch");
            foreach (var filePath in IO.DescendantFiles(patchesRoot).Where(x => x.ToLower().EndsWith(".xml")))
            {
                var childDoc = IO.ReadXml(filePath);
                foreach (XmlNode node in childDoc.DocumentElement!.ChildNodes)
                {
                    if (node.Name != "Operation")
                        continue;

                    var newNode = doc.ImportNode(node, true);
                    doc.DocumentElement!.AppendChild(newNode);
                }
            }

            foreach (XmlNode node in doc.DocumentElement!.ChildNodes)
            {
                foreach (var translationEntry in PatchOperations.PatchOperationRecursive(node, null, prePatchMode))
                {
                    yield return translationEntry with
                    {
                        RequiredMods = translationEntry.RequiredMods + requiredMods
                    };
                }
            }


            if (PatchOperations.DefsAddedByPatches.Count == 0)
                yield break;
            CompatManager.DoPreProcessing(doc);
            foreach (var (requiredModsPatches, node) in PatchOperations.DefsAddedByPatches)
            {
                var name = node.Attributes?["Name"]?.Value;
                if (requiredModsPatches != null)
                {
                    node.AppendElement("REQUIREDMODS", requiredModsPatches.ToString());
                }
                if (name != null)
                {
                    ParentNodeLookUp[name] = node;
                }
            }
            DoXmlInheritance(PatchOperations.DefsAddedByPatches.Select(x => x.Item2));

            foreach (var translation in ExtractDefs())
            {
                yield return translation with
                {
                    ClassName = $"Patches.{translation.ClassName}",
                    RequiredMods = translation.RequiredMods + requiredMods
                };
            }
            yield break;
        }
    }
}
