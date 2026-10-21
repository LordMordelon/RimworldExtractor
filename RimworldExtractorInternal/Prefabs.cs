using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;

namespace RimworldExtractorInternal
{
    public static class Prefabs
    {
        /// <summary>
        /// Existe por compatibilidad de Prefabs.dat. Al modificar los campos de Prefabs se incrementa
        /// este numero en 1, y si el guardado en Prefabs.dat no coincide, esos datos no se leen.
        /// </summary>
        private static readonly string Version = "10";

        public static string PathRimworld = string.Empty;
        public static string PathWorkshop = string.Empty;
        public static string CurrentVersion = string.Empty;
        public static string PatternVersion = string.Empty;
        public static string OriginalLanguage = string.Empty;
        public static string TranslationLanguage = string.Empty;

        /// <summary>Tema de la interfaz. No afecta a la extraccion, solo a como se ve.</summary>
        public static ColorTheme Theme = ColorTheme.System;

        /// <summary>
        /// Carpeta del mod RML, la que contiene Data/. Solo la usa la traduccion rapida,
        /// para encontrar lo que ya esta traducido y escribir el resultado ahi.
        /// </summary>
        public static string PathRml = string.Empty;

        private static Dictionary<string, ExtractionRule> _extractionRules = new();

        public static HashSet<string> ExtractableTags
        {
            get
            {
                return _extractionRules.Values.Select(x => x.ToString()).ToHashSet();
            }
            set
            {
                _extractionRules.Clear();
                foreach (var item in value)
                {
                    var rule = new ExtractionRule(item);
                    _extractionRules[rule.Tag] = rule;
                }
            }
        }

        /// <summary>
        /// Campos que estan en la lista de traducibles pero que, en cierta clase de def, no
        /// son texto para el jugador sino una ruta a un recurso del juego.
        ///
        /// El caso que lo motivo: "symbol" es traducible casi siempre —en GeneDef y RuleDef
        /// son palabras sueltas con las que se arman nombres, como "tabby" o "kitty"— pero en
        /// InteractionDef es la textura del globo de dialogo, del estilo
        /// "Motes/Speech/science". Traducir eso no queda mal: rompe el mod, porque el juego
        /// busca una textura con ese nombre y no la encuentra.
        ///
        /// Medido sobre las 525 entradas de "symbol" de RML: las 32 de InteractionDef son
        /// rutas, y ninguna de las otras 493 lo es.
        /// </summary>
        private static readonly HashSet<(string Clase, string Tag)> NoTraducibles = new()
        {
            ("InteractionDef", "symbol")
        };

        /// <summary>
        /// Etiquetas que se sumaron a la lista por defecto cuando ya habia Prefabs.dat
        /// guardados. La lista se lee del archivo, asi que sin esto solo las veria quien
        /// borrara su configuracion: Load las agrega si faltan.
        ///
        /// Las tres son de la municion generica de Combat Extended, la opcion que renombra
        /// las balas sin el calibre («mortar shell (HE)» en vez de «81mm mortar shell (HE)»).
        /// Viven en las modExtensions de cada municion y receta, y genericLabelOverride en el
        /// projectile de las balas de .45 ACP.
        /// </summary>
        private static readonly string[] EtiquetasAgregadas =
        {
            "genericLabel", "genericDescription", "genericLabelOverride"
        };

        private static void AgregarEtiquetasFaltantes()
        {
            foreach (var tag in EtiquetasAgregadas)
                _extractionRules.TryAdd(tag, new ExtractionRule(tag));
        }

        public static bool CanExtract(string tagName, string defName, string? className = null)
        {
            if (className != null && NoTraducibles.Contains((className, tagName)))
                return false;

            if (!_extractionRules.TryGetValue(tagName, out var rule))
                return false;
            return rule.CanExtract(defName);
        }

        public class ExtractionRule
        {
            public string Tag;
            public HashSet<string> Whitelist = new();
            public HashSet<string> Blacklist = new();

            public ExtractionRule(string raw)
            {
                var plusIndex = raw.IndexOf('+');
                var minusIndex = raw.IndexOf('-');

                if (plusIndex == -1 && minusIndex == -1)
                {
                    Tag = raw;
                    return;
                }

                var firstSepIndex = (plusIndex != -1 && minusIndex != -1)
                    ? Math.Min(plusIndex, minusIndex)
                    : Math.Max(plusIndex, minusIndex);

                Tag = raw.Substring(0, firstSepIndex);
                var remain = raw.Substring(firstSepIndex);

                int i = 0;
                while (i < remain.Length)
                {
                    char mode = remain[i];
                    int nextPlus = remain.IndexOf('+', i + 1);
                    int nextMinus = remain.IndexOf('-', i + 1);
                    int nextSep = (nextPlus == -1 && nextMinus == -1) ? remain.Length :
                                  (nextPlus == -1) ? nextMinus :
                                  (nextMinus == -1) ? nextPlus :
                                  Math.Min(nextPlus, nextMinus);

                    var content = remain.Substring(i + 1, nextSep - (i + 1));
                    var items = content.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                    var targetSet = (mode == '+') ? Whitelist : Blacklist;
                    foreach (var item in items) targetSet.Add(item.Trim());

                    i = nextSep;
                }
            }

            public bool CanExtract(string defName)
            {
                if (Whitelist.Count > 0)
                {
                    if (!Whitelist.Contains(defName)) return false;
                }

                if (Blacklist.Contains(defName)) return false;

                return true;
            }

            public override string ToString()
            {
                var sb = new StringBuilder(Tag);
                if (Whitelist.Count > 0)
                {
                    sb.Append("+");
                    sb.Append(string.Join(",", Whitelist));
                }
                if (Blacklist.Count > 0)
                {
                    sb.Append("-");
                    sb.Append(string.Join(",", Blacklist));
                }
                return sb.ToString();
            }
        }

        public static HashSet<string> FullListTranslationTags = new();

        public static Dictionary<string, string> NodeReplacement = new();

        /// <summary>
        /// TranslationHandle ordered by Priority
        /// </summary>
        public static List<string> TranslationHandles = new();

        public const string ExtensionKeyExtraCommentTranslated = "ExtraCommentTranslated";

        public static void Init()
        {
            PathRimworld = "D:\\SteamLibrary\\steamapps\\common\\RimWorld";
            PathWorkshop = "D:\\SteamLibrary\\steamapps\\workshop\\content\\294100";
            CurrentVersion = "1.7";
            PatternVersion = @"^[1]\.\d+";
            OriginalLanguage = "English";
            TranslationLanguage = "SpanishLatin (Español(Latinoamérica))";
            ExtractableTags = new(
                "label/rulesStrings/description/baseDesc/title/titleShort/customLabel/symbol/jobString/reportString/labelNoun/slateRef/verb/gerund/adjective/member/tips/ideoName/thoughtStageDescriptions/jobReportString/theme/labelShortAdj/labelPlural/letterText/deathMessage/labelShort/letterLabel/helpText/text/baseInspectLine/labelFemale/descriptionShort/beginLetter/ingestCommandString/ingestReportString/titleShortFemale/titleFemale/gerundLabel/pawnLabel/stageName/shortDescription/customEffectDescriptions/endMessage/leaderTitle/pawnSingular/pawnsPlural/desc/recoveryMessage/chargeNoun/cooldownGerund/type/potentialExtraOutcomeDesc/labelNounPretty/headerTip/rejectInputMessage/spectatorGerund/spectatorsLabel/fuelLabel/formatString/useLabel/RMBLabel/permanentLabel/name/missingDesc/worshipRoomLabel/labelAbstract/fuelGizmoLabel/destroyedLabel/outOfFuelMessage/summary/ritualExpectedDesc/customSummary/meatLabel/labelForFullStatList/tooltip/gizmoLabel/onMapInstruction/letterTitle/textEnemy/destroyedOutLabel/beginLetterLabel/labelMale/groupName/gizmoDescription/names/arrivalTextEnemy/letterLabelEnemy/arrivedLetter/calledOffMessage/finishedMessage/approachingReportString/approachOrderString/expectedThingLabelTip/skillLabel/extraPredictedOutcomeDescriptions/modNameReadable/descriptionFuture/textWillArrive/arrivalTextFriendly/letterLabelFriendly/helpTextController/successfullyRemovedHediffMessage/textFriendly/eventLabel/textController/descOverride/shortDescOverride/content/discoveredLetterText/discoveredLetterTitle/beginLetterContinue/resourceLabel/message/overrideLabel/extraTooltip/offMessage/successMessage/effectDesc/letterInfoText/categoryLabel/groupLabel/battleStateLabel/customizationTitle/fixedName/noun/lockedReason/descriptionExtra/labelPrefix/labelMechanoids/ingestReportStringEat/failMessage/valueFormat/structureLabel/labelSocial/labelInBracketsExtraForHediff/ChooseDesc/ChooseLabel/ritualExplanation/resourceDescription/discoverLetterText/countdownLabel/inspectString/completedLetterText/completedLetterTitle/leaderDescription/formatStringUnfinalized/jobReportOverride/discoverLetterLabel/instantlyPermanentLabel/notifyMessage/onCooldownString/invalidTargetPawn/noAssignablePawnsDesc/reportText/statLabel/visualLabel/commandDescriptions/successMessageNoNegativeThought/tipLabelOverride/mainPartAllThreatsLabel/customChildDisallowMessage/ritualExpectedDescNoAdjective/loweredName/cancelLabel/texName/labelOverride/messageText/proficiencyAdjective/stuffAdjective/unit/labelTendedWell/labelTendedWellInner/labelSolidTendedWell/overrideTooltip/royalFavorLabel/extraReportString/spawnInBackstories/customLetterLabel/customLetterText/confirmationDialogText/tip/outcomeDescription/generalDescription/generalTitle/dialogue/activateDescString/activateLabelString/completedLetter/completedLetterLabel/guiLabelString/gizmoDesc/activatedMessageKey/appendString/gizmoDesc1/gizmoDesc2/gizmoLabel1/gizmoLabel"
                    .Split('/'));
            AgregarEtiquetasFaltantes();
            FullListTranslationTags = new()
            {
                "rulesFiles", "rulesStrings", "pathList"
            };
            NodeReplacement = new Dictionary<string, string>(
                "CombatExtended.AmmoDef+*|ThingDef+*/VFECore.ExpandableProjectileDef+*|ThingDef+*/AbilityUser.ProjectileDef_AbilityLaser+*|ThingDef+*/AbilityUser.ProjectileDef_Ability+*|ThingDef+*/NewRatkin.CustomThingDef+*|ThingDef+*/AlienRace.AlienBackstoryDef+*|BackstoryDef+*/RatkinGeneExpanded.FactionDefExtended+*|FactionDef+*/RatkinGeneExpanded.ThingDefExtended+*|ThingDef+*/AlienRace.ThingDef_AlienRace+*|ThingDef+*/Rimlaser.Building_LaserGunDef+*|ThingDef+*/Rimlaser.LaserBeamDef+*|ThingDef+*/Rimlaser.LaserGunDef+*|ThingDef+*/Rimlaser.SpinningLaserGunDef+*|ThingDef+*/JecsTools.BackstoryDef+baseDesc|JescTools.BackstoryDef+description/AnestheticGunMod2.AnestheticBulletDef+*|ThingDef+*/BackstoryDef+baseDesc|BackstoryDef+description/DubsBadHygiene.WashingJobDef+*|JobDef+*/DubsBadHygiene.Needy+*|NeedDef+*/VarietyMatters.FoodVariety_NeedDef+*|NeedDef+*/Kiiro.StorytellerDef_Custom+*|StorytellerDef+*/Vehicles.SkinDef+*|Vehicles.PatternDef+*/Vehicles.AntiAircraftDef+*|WorldObjectDef+*/Vehicles.AirdropDef+*|ThingDef+*/Meow.FactionDefExtended+*|FactionDef+*"
                    .Split("/").Select(
                        x =>
                        {
                            var tokens = x.Split('|');
                            return new KeyValuePair<string, string>(tokens[0].Trim(), tokens[1].Trim());
                        }));
            TranslationHandles = new()
            {
                // "label", // 200
                // "customLabel", "name", "def",
                // "inSignal", "labelMale", // 100
                // "labelFemale", 
                "*verbClass", "*compClass", 
                // "hediff"
            };
            Theme = ColorTheme.System;
            PathRml = string.Empty;
        }

        /// <summary>
        /// La carpeta del ejecutable, que es donde viven Prefabs.dat y el log detallado.
        ///
        /// No es el directorio de trabajo: con un acceso directo o lanzando la aplicacion desde
        /// otra carpeta, la configuracion no aparecia, volvia a pedir las rutas y dejaba otro
        /// Prefabs.dat suelto ahi. Tampoco es AppContext.BaseDirectory: en el Portable, que se
        /// descomprime entero antes de arrancar, apunta a esa carpeta temporal.
        /// </summary>
        public static string Carpeta => Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

        /// <summary>Donde se guarda y se lee la configuracion si no se indica otro archivo.</summary>
        public static string RutaPorDefecto => Path.Combine(Carpeta, "Prefabs.dat");

        public static void Save(string? fileName = null)
        {
            fileName ??= RutaPorDefecto;
            List<string> lines = new List<string>
            {
                "DO NOT EDIT THIS MANUALLY",
                Version,

                // Reservada: aca iba EnableTkey, que nombraba las claves de un nodo con TKey
                // como "def.TKey.slateRef". Estaba apagada y prenderla cambiaba el nombre de
                // claves ya traducidas en RML, asi que se fue apagada. Se escribe el valor que
                // tenia, por lo mismo que CommentOriginal.
                "False",
                PathRimworld,
                PathWorkshop,

                // Reservada: aca iba PathBaseRefList, la lista de mods que la seleccion marcaba
                // como referencia al abrirse. Desde la 1.0 las referencias se agregan solas.
                "",
                CurrentVersion,
                PatternVersion,
                // Reservada: aca iba PatternVersionWithV, que nunca se uso. Por lo mismo que
                // CommentOriginal, se sigue escribiendo, con el valor que tenia.
                @"^v[1]\.\d+",
                OriginalLanguage,
                TranslationLanguage,

                // Reservada: aca iba CommentOriginal, que se fue con el boton XLSX -> XML.
                // La linea se sigue escribiendo porque el archivo se lee por posicion:
                // sacarla corre todo lo que viene despues y obliga a subir Version, y eso
                // descarta el archivo entero y le borra la configuracion a todo el mundo.
                // Se escribe un valor que una version anterior tambien sepa leer.
                "False",
                string.Join('/', ExtractableTags),
                string.Join('/', FullListTranslationTags),
                string.Join('/', NodeReplacement.Select(x => $"{x.Key}|{x.Value}")),
                string.Join('/', TranslationHandles),

                // Reservada: aca iba Policy, que decidia que hacer con un archivo que ya
                // existia al extraer en carpeta. Ahora se sobrescribe siempre, que es el valor
                // que se sigue escribiendo.
                "Overwrite",

                // Reservada, por lo mismo: aca iba Method, cuando habia cuatro formatos
                // de extraccion a elegir.
                "LanguagesToTranslate",

                // Los campos nuevos van al final y se leen solo si estan. Asi un
                // Prefabs.dat viejo sigue sirviendo: si se agregaran en el medio habria
                // que subir Version, y eso descarta el archivo entero y le borra la
                // configuracion a todo el mundo.
                Theme.ToString(),
                PathRml
            };
            File.WriteAllLines(fileName, lines);
        }


        /// <exception cref="SerializationException">El campo Version no coincide</exception>
        public static void Load(string? fileName = null)
        {
            fileName ??= RutaPorDefecto;
            var lines = File.ReadAllLines(fileName);
            var idx = 1;
            if (Version != lines[idx++])
            {
                throw new SerializationException($"wrong version of {fileName}");
            }

            idx++; // Reservada: EnableTkey. Ver el comentario de Save().
            PathRimworld = lines[idx++];
            PathWorkshop = lines[idx++];
            idx++; // Reservada: PathBaseRefList. Ver el comentario de Save().
            CurrentVersion = lines[idx++];
            PatternVersion = lines[idx++];
            idx++; // Reservada: PatternVersionWithV. Ver el comentario de Save().
            OriginalLanguage = lines[idx++];
            TranslationLanguage = lines[idx++];
            idx++; // Reservada: CommentOriginal. Ver el comentario de Save().
            ExtractableTags = new(lines[idx++].Split('/'));
            AgregarEtiquetasFaltantes();
            FullListTranslationTags = new(lines[idx++].Split("/"));
            NodeReplacement = new(lines[idx++].Split('/').Select(x =>
            {
                var splited = x.Split('|');
                return new KeyValuePair<string, string>(splited[0], splited[1]);
            }));
            TranslationHandles = new(lines[idx++].Split('/'));
            idx++; // Reservada: Policy. Ver el comentario de Save().
            idx++; // Reservada: Method. Ver el comentario de Save().

            // Un archivo guardado por una version anterior no llega hasta aca: se queda
            // con el valor por defecto en vez de fallar.
            Theme = idx < lines.Length && Enum.TryParse<ColorTheme>(lines[idx++], out var tema)
                ? tema
                : ColorTheme.System;
            PathRml = idx < lines.Length ? lines[idx++] : string.Empty;
        }

        public static string AutoDetectRimworldVersion()
        {
            try
            {
                var pathVersion = Path.Combine(PathRimworld, "Version.txt");
                var context = File.ReadAllText(pathVersion).Trim();
                var match = Regex.Match(context, PatternVersion);
                if (match.Success)
                {
                    var version = match.Groups[0].Value;
                    return version;
                }
            }
            catch (Exception e)
            {
                Log.Err(Strings.ErrorAutoDetectingVersion(e.Message));
            }

            return CurrentVersion;
        }

        /// <summary>
        /// Tema de la interfaz. "System" sigue lo que tenga configurado Windows.
        /// Se guarda por nombre, asi que el orden no importa.
        /// </summary>
        public enum ColorTheme
        {
            System = 0, Light, Dark
        }

    }
}
