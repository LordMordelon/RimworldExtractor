using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using RimworldExtractorInternal.DataTypes;

namespace RimworldExtractorInternal
{
    /// <summary>
    /// Con que nombre conoce el juego a cada comp: el de la clase que le asigna su CompProperties.
    ///
    /// RimWorld nombra un elemento de lista por su TranslationHandle, y el de un comp es su
    /// compClass: DMS_Mech_Noctula.comps.CompCamouflage.detectedLetterDesc. La traduccion oficial
    /// esta escrita asi. El extractor lo nombraba por su posicion, comps.11, y la posicion cambia
    /// con los mods activos: The Dead Man's Switch II le agrega dos comps a sus automatroides
    /// cuando esta Combat Extended, el juego no encontraba el campo y la carta salia en ingles,
    /// sin avisar.
    ///
    /// El compClass casi nunca esta en el XML. Lo fija el constructor de cada CompProperties
    /// (compClass = typeof(CompCamouflage)), asi que se lee del IL de los .dll del juego y de los
    /// mods. Se leen como metadatos, sin cargarlos: no hace falta Unity ni nada de lo que usan.
    /// Lo que no se puede averiguar devuelve null, y ese comp sigue yendo por posicion.
    /// </summary>
    public static class ClasesDeComp
    {
        /// <summary>
        /// Las clases base con un compClass marcado como TranslationHandle. Se miraron en el
        /// Assembly-CSharp de la 1.7: solo cuentan los comps que descienden de una de estas.
        /// </summary>
        private static readonly HashSet<string> Raices = new()
        {
            "Verse.CompProperties", "Verse.HediffCompProperties", "Verse.AbilityCompProperties"
        };

        /// <summary>
        /// Donde busca el juego una clase escrita sin namespace, y en este orden
        /// (GenTypes.IgnoredNamespaceNames).
        /// </summary>
        private static readonly string[] NamespacesImplicitos =
        {
            "RimWorld", "Verse", "LudeonTK", "Verse.AI", "Verse.AI.Group", "Verse.Sound", "Verse.Grammar",
            "RimWorld.Planet", "RimWorld.BaseGen", "RimWorld.QuestGen", "RimWorld.SketchGen", "System"
        };

        /// <summary>De cada tipo, su clase base y la clase que su constructor le asigna a compClass.</summary>
        private sealed record Tipo(string? Base, string? Asignada);

        /// <summary>
        /// Cada .dll leido, por ruta. Queda para toda la sesion: en «Actualizar todo RML» casi
        /// todos los mods comparten el juego y unos pocos frameworks.
        /// </summary>
        private static readonly Dictionary<string, (DateTime Fecha, Dictionary<string, Tipo> Tipos)> Leidos =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Los de la extraccion en curso, en orden de busqueda. Se busca solo en estos y no en
        /// todo lo leido, para que el nombre no dependa de que otros mods entraron en la corrida.
        /// </summary>
        private static List<Dictionary<string, Tipo>> _activos = new();

        private static readonly Dictionary<string, string?> Resueltos = new();

        private static readonly object Cerrojo = new();

        /// <summary>Pone los .dll con los que se nombra a partir de ahora. Vacio, apaga los nombres.</summary>
        public static void Usar(IEnumerable<string> dlls)
        {
            lock (Cerrojo)
            {
                _activos = dlls.Distinct(StringComparer.OrdinalIgnoreCase).Select(Leer).ToList();
                Resueltos.Clear();
            }
        }

        /// <summary>
        /// El nombre del comp para la clase que dice el atributo Class del XML, o null si no es
        /// un comp o no se sabe.
        /// </summary>
        public static string? Nombre(string claseDelXml)
        {
            lock (Cerrojo)
            {
                if (_activos.Count == 0)
                    return null;
                if (!Resueltos.TryGetValue(claseDelXml, out var nombre))
                    Resueltos[claseDelXml] = nombre = Resolver(claseDelXml.Trim());
                return nombre;
            }
        }

        /// <summary>
        /// Los .dll que carga el juego con ese mod: el del juego primero, despues los del mod y
        /// despues los de sus referencias.
        /// </summary>
        public static List<string> DllsDe(ModMetadata mod, IEnumerable<ModMetadata>? referencias)
        {
            var dlls = new List<string>();
            foreach (var juego in new[]
                     {
                         Path.Combine(Prefabs.PathRimworld, "Assembly-CSharp.dll"),
                         Path.Combine(Prefabs.PathRimworld, "RimWorldWin64_Data", "Managed", "Assembly-CSharp.dll")
                     })
            {
                if (File.Exists(juego))
                    dlls.Add(juego);
            }

            foreach (var m in new[] { mod }.Concat(referencias ?? Enumerable.Empty<ModMetadata>()))
            {
                if (!m.IsOfficialContent)
                    dlls.AddRange(DllsDelMod(m.RootDir));
            }

            return dlls;
        }

        /// <summary>
        /// Las carpetas Assemblies del mod, salvo las de otra version. Se buscan en el disco y no
        /// en las carpetas extraibles: un mod puede tener los Defs en la raiz y los .dll en 1.7/.
        /// </summary>
        private static IEnumerable<string> DllsDelMod(string raiz)
        {
            if (!Directory.Exists(raiz))
                return Enumerable.Empty<string>();

            Version.TryParse(Prefabs.CurrentVersion, out var actual);
            var version = Directory.EnumerateDirectories(raiz)
                .Select(Path.GetFileName)
                .Where(x => x != null && Regex.IsMatch(x, Prefabs.PatternVersion))
                .Select(x => (Texto: x!, Valor: Version.TryParse(x, out var v) ? v : null))
                .Where(x => x.Valor != null && (actual == null || x.Valor <= actual))
                .OrderByDescending(x => x.Valor)
                .Select(x => x.Texto)
                .FirstOrDefault();

            try
            {
                return Directory.EnumerateDirectories(raiz, "Assemblies", SearchOption.AllDirectories)
                    .Where(carpeta =>
                    {
                        var primero = Path.GetRelativePath(raiz, carpeta)
                            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
                        return !Regex.IsMatch(primero, Prefabs.PatternVersion) || primero == version;
                    })
                    .SelectMany(carpeta => Directory.EnumerateFiles(carpeta, "*.dll"))
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Log.WrnDet(Strings.ErrorReadingFile(raiz, e.Message));
                return Enumerable.Empty<string>();
            }
        }

        private static string? Resolver(string clase)
        {
            var completa = Buscar(clase) != null
                ? clase
                : NamespacesImplicitos.Select(ns => $"{ns}.{clase}").FirstOrDefault(x => Buscar(x) != null);
            if (completa == null)
                return null;

            // Desde la clase hacia arriba: la primera asignacion que aparece es la que queda,
            // porque el constructor de la derivada corre despues del de su base.
            string? asignada = null;
            var actual = completa;
            for (var pasos = 0; actual != null && pasos < 64; pasos++)
            {
                var tipo = Buscar(actual);
                if (tipo == null)
                    return null;
                asignada ??= tipo.Asignada;
                if (Raices.Contains(actual))
                    return asignada;
                actual = tipo.Base;
            }

            return null;
        }

        private static Tipo? Buscar(string nombreCompleto)
        {
            foreach (var tipos in _activos)
            {
                if (tipos.TryGetValue(nombreCompleto, out var tipo))
                    return tipo;
            }

            return null;
        }

        private static Dictionary<string, Tipo> Leer(string dll)
        {
            DateTime fecha;
            try
            {
                fecha = File.GetLastWriteTimeUtc(dll);
            }
            catch (Exception)
            {
                return new Dictionary<string, Tipo>();
            }

            if (Leidos.TryGetValue(dll, out var leido) && leido.Fecha == fecha)
                return leido.Tipos;

            Dictionary<string, Tipo> tipos;
            try
            {
                tipos = LeerTipos(dll);
            }
            catch (Exception e)
            {
                // Un .dll que no se puede leer deja sus comps por posicion, como antes. No es
                // motivo para cortar la extraccion.
                Log.WrnDet(Strings.ErrorReadingFile(dll, e.Message));
                tipos = new Dictionary<string, Tipo>();
            }

            Leidos[dll] = (fecha, tipos);
            return tipos;
        }

        private static Dictionary<string, Tipo> LeerTipos(string dll)
        {
            var tipos = new Dictionary<string, Tipo>();

            // Se lee entero a memoria para no dejar el archivo tomado.
            using var pe = new PEReader(new MemoryStream(File.ReadAllBytes(dll)));
            if (!pe.HasMetadata)
                return tipos;

            var md = pe.GetMetadataReader();
            foreach (var handle in md.TypeDefinitions)
            {
                var definicion = md.GetTypeDefinition(handle);
                var nombre = NombreCompleto(md, handle);
                if (nombre == null)
                    continue;

                tipos[nombre] = new Tipo(NombreCompleto(md, definicion.BaseType), AsignadaEnElConstructor(pe, md, definicion));
            }

            return tipos;
        }

        /// <summary>
        /// La clase que el constructor sin parametros le pone a compClass, si se la pone. Es el
        /// constructor que usa el juego al leer el XML.
        ///
        /// Busca las dos formas que deja el compilador: compClass = typeof(X), que es ldtoken X,
        /// GetTypeFromHandle y stfld compClass; y : base(typeof(X)), que termina en la llamada
        /// al constructor de la base en lugar del stfld.
        /// </summary>
        private static string? AsignadaEnElConstructor(PEReader pe, MetadataReader md, TypeDefinition tipo)
        {
            string? asignada = null;
            foreach (var handle in tipo.GetMethods())
            {
                var metodo = md.GetMethodDefinition(handle);
                if (md.GetString(metodo.Name) != ".ctor" || metodo.RelativeVirtualAddress == 0)
                    continue;

                var firma = md.GetBlobReader(metodo.Signature);
                var encabezado = firma.ReadSignatureHeader();
                if (encabezado.IsGeneric)
                    firma.ReadCompressedInteger();
                if (firma.ReadCompressedInteger() != 0)
                    continue;

                asignada = BuscarAsignacion(md, pe.GetMethodBody(metodo.RelativeVirtualAddress).GetILReader());
            }

            return asignada;
        }

        private static string? BuscarAsignacion(MetadataReader md, BlobReader il)
        {
            string? asignada = null;
            string? pendiente = null;
            var paso = 0; // 1: vino un ldtoken; 2: y despues GetTypeFromHandle

            while (il.RemainingBytes > 0)
            {
                int codigo = il.ReadByte();
                if (codigo == 0xFE)
                    codigo = 0xFE00 | il.ReadByte();

                if (!Operandos.TryGetValue(codigo, out var operando))
                    return asignada;

                if (codigo == OpCodes.Ldtoken.Value)
                {
                    pendiente = NombreCorto(md, MetadataTokens.EntityHandle(il.ReadInt32()));
                    paso = pendiente != null ? 1 : 0;
                    continue;
                }

                if (codigo == OpCodes.Call.Value || codigo == OpCodes.Callvirt.Value)
                {
                    var llamado = NombreDeMiembro(md, MetadataTokens.EntityHandle(il.ReadInt32()));
                    if (paso == 1 && llamado == "GetTypeFromHandle")
                    {
                        paso = 2;
                        continue;
                    }

                    if (paso == 2 && llamado == ".ctor")
                        asignada = pendiente;
                    paso = 0;
                    continue;
                }

                if (codigo == OpCodes.Stfld.Value)
                {
                    var campo = NombreDeMiembro(md, MetadataTokens.EntityHandle(il.ReadInt32()));
                    if (paso == 2 && campo == "compClass")
                        asignada = pendiente;
                    paso = 0;
                    continue;
                }

                Saltar(ref il, operando);
                paso = 0;
            }

            return asignada;
        }

        /// <summary>Que operando lleva cada instruccion de IL, para poder saltearlo.</summary>
        private static readonly Dictionary<int, OperandType> Operandos = typeof(OpCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(x => (OpCode)x.GetValue(null)!)
            .ToDictionary(x => (int)(ushort)x.Value, x => x.OperandType);

        private static void Saltar(ref BlobReader il, OperandType operando)
        {
            switch (operando)
            {
                case OperandType.InlineNone:
                    break;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    il.Offset += 1;
                    break;
                case OperandType.InlineVar:
                    il.Offset += 2;
                    break;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    il.Offset += 8;
                    break;
                case OperandType.InlineSwitch:
                    il.Offset += 4 * il.ReadInt32();
                    break;
                default:
                    il.Offset += 4;
                    break;
            }
        }

        /// <summary>Type.Name: el nombre sin namespace, que es el que usa el juego como handle.</summary>
        private static string? NombreCorto(MetadataReader md, EntityHandle handle) => handle.Kind switch
        {
            HandleKind.TypeDefinition => md.GetString(md.GetTypeDefinition((TypeDefinitionHandle)handle).Name),
            HandleKind.TypeReference => md.GetString(md.GetTypeReference((TypeReferenceHandle)handle).Name),
            _ => null
        };

        private static string? NombreCompleto(MetadataReader md, EntityHandle handle)
        {
            if (handle.IsNil)
                return null;

            switch (handle.Kind)
            {
                case HandleKind.TypeDefinition:
                {
                    var tipo = md.GetTypeDefinition((TypeDefinitionHandle)handle);
                    var declarante = tipo.GetDeclaringType();
                    if (!declarante.IsNil)
                        return $"{NombreCompleto(md, declarante)}+{md.GetString(tipo.Name)}";
                    return Juntar(md.GetString(tipo.Namespace), md.GetString(tipo.Name));
                }
                case HandleKind.TypeReference:
                {
                    var tipo = md.GetTypeReference((TypeReferenceHandle)handle);
                    if (tipo.ResolutionScope.Kind == HandleKind.TypeReference)
                        return $"{NombreCompleto(md, tipo.ResolutionScope)}+{md.GetString(tipo.Name)}";
                    return Juntar(md.GetString(tipo.Namespace), md.GetString(tipo.Name));
                }
                default:
                    return null;
            }
        }

        private static string Juntar(string ns, string nombre) => ns.Length == 0 ? nombre : $"{ns}.{nombre}";

        private static string? NombreDeMiembro(MetadataReader md, EntityHandle handle) => handle.Kind switch
        {
            HandleKind.MethodDefinition => md.GetString(md.GetMethodDefinition((MethodDefinitionHandle)handle).Name),
            HandleKind.FieldDefinition => md.GetString(md.GetFieldDefinition((FieldDefinitionHandle)handle).Name),
            HandleKind.MemberReference => md.GetString(md.GetMemberReference((MemberReferenceHandle)handle).Name),
            _ => null
        };
    }
}
