using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace RimworldExtractorInternal
{
    public static class Prefabs
    {
        /// <summary>
        /// Existe por compatibilidad de Prefabs.dat. Al modificar los campos de Prefabs se incrementa
        /// este numero en 1, y si el guardado en Prefabs.dat no coincide, esos datos no se leen.
        /// </summary>
        private static readonly string Version = "10";

        // Funcion provisoria TODO: REMOVE THIS AFTER
        public static bool EnableTkey = false;

        public static string PathRimworld = string.Empty;
        public static string PathWorkshop = string.Empty;
        public static string PathBaseRefList = "";
        public static string CurrentVersion = string.Empty;
        public static string PatternVersion = string.Empty;
        public static string PatternVersionWithV = string.Empty;
        public static string OriginalLanguage = string.Empty;
        public static string TranslationLanguage = string.Empty;
        public static bool CommentOriginal = false;

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

        public static bool CanExtract(string tagName, string defName)
        {
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

        public static DuplicatesPolicy Policy = default;
        public static ExtractionMethod Method = default;


        public static Action<XLWorkbook, string>? StopCallbackXlsx = null;
        public static Action<XmlDocument, string>? StopCallbackXml = null; 
        public static Action<IEnumerable<string>, string>? StopCallbackTxt = null;


        public const string ExtensionKeyExtraCommentTranslated = "ExtraCommentTranslated";

        public static void Init()
        {
            // Funcion provisoria TODO: REMOVE THIS AFTER
            EnableTkey = false;
            PathRimworld = "D:\\SteamLibrary\\steamapps\\common\\RimWorld";
            PathWorkshop = "D:\\SteamLibrary\\steamapps\\workshop\\content\\294100";
            PathBaseRefList = "";
            CurrentVersion = "1.6";
            PatternVersion = @"^[1]\.\d+";
            PatternVersionWithV = @"^v[1]\.\d+";
            OriginalLanguage = "English";
            TranslationLanguage = "SpanishLatin (Español(Latinoamérica))";
            CommentOriginal = false;
            ExtractableTags = new(
                "label/rulesStrings/description/baseDesc/title/titleShort/customLabel/symbol/jobString/reportString/labelNoun/slateRef/verb/gerund/adjective/member/tips/ideoName/thoughtStageDescriptions/jobReportString/theme/labelShortAdj/labelPlural/letterText/deathMessage/labelShort/letterLabel/helpText/text/baseInspectLine/labelFemale/descriptionShort/beginLetter/ingestCommandString/ingestReportString/titleShortFemale/titleFemale/gerundLabel/pawnLabel/stageName/shortDescription/customEffectDescriptions/endMessage/leaderTitle/pawnSingular/pawnsPlural/desc/recoveryMessage/chargeNoun/cooldownGerund/type/potentialExtraOutcomeDesc/labelNounPretty/headerTip/rejectInputMessage/spectatorGerund/spectatorsLabel/fuelLabel/formatString/useLabel/RMBLabel/permanentLabel/name/missingDesc/worshipRoomLabel/labelAbstract/fuelGizmoLabel/destroyedLabel/outOfFuelMessage/summary/ritualExpectedDesc/customSummary/meatLabel/labelForFullStatList/tooltip/gizmoLabel/onMapInstruction/letterTitle/textEnemy/destroyedOutLabel/beginLetterLabel/labelMale/groupName/gizmoDescription/names/arrivalTextEnemy/letterLabelEnemy/arrivedLetter/calledOffMessage/finishedMessage/approachingReportString/approachOrderString/expectedThingLabelTip/skillLabel/extraPredictedOutcomeDescriptions/modNameReadable/descriptionFuture/textWillArrive/arrivalTextFriendly/letterLabelFriendly/helpTextController/successfullyRemovedHediffMessage/textFriendly/eventLabel/textController/descOverride/shortDescOverride/content/discoveredLetterText/discoveredLetterTitle/beginLetterContinue/resourceLabel/message/overrideLabel/extraTooltip/offMessage/successMessage/effectDesc/letterInfoText/categoryLabel/groupLabel/battleStateLabel/customizationTitle/fixedName/noun/lockedReason/descriptionExtra/labelPrefix/labelMechanoids/ingestReportStringEat/failMessage/valueFormat/structureLabel/labelSocial/labelInBracketsExtraForHediff/ChooseDesc/ChooseLabel/ritualExplanation/resourceDescription/discoverLetterText/countdownLabel/inspectString/completedLetterText/completedLetterTitle/leaderDescription/formatStringUnfinalized/jobReportOverride/discoverLetterLabel/instantlyPermanentLabel/notifyMessage/onCooldownString/invalidTargetPawn/noAssignablePawnsDesc/reportText/statLabel/visualLabel/commandDescriptions/successMessageNoNegativeThought/tipLabelOverride/mainPartAllThreatsLabel/customChildDisallowMessage/ritualExpectedDescNoAdjective/loweredName/cancelLabel/texName/labelOverride/messageText/proficiencyAdjective/stuffAdjective/unit/labelTendedWell/labelTendedWellInner/labelSolidTendedWell/overrideTooltip/royalFavorLabel/extraReportString/spawnInBackstories/customLetterLabel/customLetterText/confirmationDialogText/tip/outcomeDescription/generalDescription/generalTitle/dialogue/activateDescString/activateLabelString/completedLetter/completedLetterLabel/guiLabelString/gizmoDesc/activatedMessageKey/appendString/gizmoDesc1/gizmoDesc2/gizmoLabel1/gizmoLabel"
                    .Split('/'));
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
            Policy = DuplicatesPolicy.Overwrite;
            Method = ExtractionMethod.Languages;
        }

        public static void Save(string fileName = "Prefabs.dat")
        {
            List<string> lines = new List<string>
            {
                "DO NOT EDIT THIS MANUALLY",
                Version,
                EnableTkey.ToString(), // TODO: REMOVE THIS AFTER
                PathRimworld,
                PathWorkshop,
                PathBaseRefList,
                CurrentVersion,
                PatternVersion,
                PatternVersionWithV,
                OriginalLanguage,
                TranslationLanguage,
                CommentOriginal.ToString(),
                string.Join('/', ExtractableTags),
                string.Join('/', FullListTranslationTags),
                string.Join('/', NodeReplacement.Select(x => $"{x.Key}|{x.Value}")),
                string.Join('/', TranslationHandles),
                Policy.ToString(),
                Method.ToString()
            };
            File.WriteAllLines(fileName, lines);
        }


        /// <exception cref="SerializationException">El campo Version no coincide</exception>
        public static void Load(string fileName = "Prefabs.dat")
        {
            var lines = File.ReadAllLines(fileName);
            var idx = 1;
            if (Version != lines[idx++])
            {
                throw new SerializationException($"wrong version of {fileName}");
            }

            EnableTkey = bool.Parse(lines[idx++]); // TODO: REMOVE THIS AFTER
            PathRimworld = lines[idx++];
            PathWorkshop = lines[idx++];
            PathBaseRefList = lines[idx++];
            CurrentVersion = lines[idx++];
            PatternVersion = lines[idx++];
            PatternVersionWithV = lines[idx++];
            OriginalLanguage = lines[idx++];
            TranslationLanguage = lines[idx++];
            CommentOriginal = bool.Parse(lines[idx++]);
            ExtractableTags = new(lines[idx++].Split('/'));
            FullListTranslationTags = new(lines[idx++].Split("/"));
            NodeReplacement = new(lines[idx++].Split('/').Select(x =>
            {
                var splited = x.Split('|');
                return new KeyValuePair<string, string>(splited[0], splited[1]);
            }));
            TranslationHandles = new(lines[idx++].Split('/'));
            Policy = Enum.Parse<DuplicatesPolicy>(lines[idx++]);
            Method = Enum.Parse<ExtractionMethod>(lines[idx++]);
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

        public enum DuplicatesPolicy
        {
            Stop = 0,
            Overwrite,
            KeepOriginal
        }

        public enum ExtractionMethod
        {
            // Los valores se guardan por nombre en Prefabs.dat, pero el combo de
            // Ajustes se lee por indice: agregar siempre al final.
            Excel = 0, Languages, LanguagesWithComments, LanguagesToTranslate
        }
    }
}
