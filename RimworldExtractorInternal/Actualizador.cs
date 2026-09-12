using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;

namespace RimworldExtractorInternal
{
    /// <summary>
    /// Pasar la aplicacion a la ultima version publicada, sin que el usuario tenga que elegir
    /// la descarga correcta ni descomprimir nada encima.
    ///
    /// Windows no deja sobrescribir ni borrar un archivo en uso, pero si renombrarlo: la imagen
    /// de un ejecutable o de una DLL cargada se abre con FILE_SHARE_DELETE, y por eso se puede
    /// correr de lugar mientras corre. De ahi sale todo el procedimiento, que es el mismo para
    /// el Portable y para el Standard y no necesita ningun proceso auxiliar: se renombra lo que
    /// hay a ".viejo", se pone lo nuevo en su lugar y se reinicia. Los ".viejo" se borran en el
    /// arranque siguiente, cuando ya nadie los tiene tomados.
    /// </summary>
    public static class Actualizador
    {
        /// <summary>Como esta instalada la aplicacion, que decide que se descarga.</summary>
        public enum Variante
        {
            /// <summary>Un unico ejecutable autoextraible.</summary>
            Portable,

            /// <summary>El ejecutable mas las DLL en "bin", como sale del zip.</summary>
            Standard
        }

        public const string RepoUrl = "https://github.com/LordMordelon/RimworldExtractor";

        private const string NombrePortable = "RimworldExtractor-Portable.exe";
        private const string NombreStandard = "RimworldExtractor-Standard.zip";

        /// <summary>El ejecutable que trae el zip del Standard.</summary>
        private const string EjecutableDelZip = "RimworldExtractorGUI.exe";

        /// <summary>Con lo que se renombra lo que se reemplaza, hasta poder borrarlo.</summary>
        public const string SufijoViejo = ".viejo";

        /// <summary>
        /// GitHub pide un User-Agent y devuelve 403 sin el. Vale tanto para la API como para
        /// las descargas.
        /// </summary>
        private const string UserAgent = "RimworldExtractor";

        /// <summary>
        /// Si la aplicacion corre como Portable o como Standard.
        ///
        /// El Portable se publica con PublishSingleFile e IncludeAllContentForSelfExtract, asi
        /// que se descomprime entero en una carpeta temporal antes de arrancar y su
        /// AppContext.BaseDirectory no es la carpeta donde esta el .exe. En el Standard son la
        /// misma. Es la misma distincion de la que depende <see cref="Prefabs.Carpeta"/>.
        /// </summary>
        public static Variante VarianteDe(string rutaDelEjecutable, string carpetaBase)
        {
            var carpetaDelExe = Path.GetDirectoryName(Path.GetFullPath(rutaDelEjecutable));
            if (carpetaDelExe == null)
                return Variante.Standard;

            return MismaCarpeta(carpetaDelExe, carpetaBase) ? Variante.Standard : Variante.Portable;
        }

        /// <summary>Como corre esta instancia.</summary>
        public static Variante VarianteActual() =>
            VarianteDe(Environment.ProcessPath ?? AppContext.BaseDirectory, AppContext.BaseDirectory);

        /// <summary>
        /// De donde se baja el paquete de una version.
        ///
        /// Se arma con la etiqueta y no se pregunta por la API: los nombres de los archivos
        /// publicados son fijos, asi que no hace falta gastar cuota para averiguarlos.
        /// </summary>
        public static string UrlDe(string etiqueta, Variante variante)
        {
            var nombre = variante == Variante.Portable ? NombrePortable : NombreStandard;
            return $"{RepoUrl}/releases/download/{etiqueta}/{nombre}";
        }

        /// <summary>
        /// Comprueba que lo descargado sea lo que se esperaba.
        ///
        /// No hay checksums publicados, asi que esto es el piso: que el Portable sea un
        /// ejecutable de Windows y que el Standard sea un zip que traiga la aplicacion adentro.
        /// Alcanza para el caso real, que es bajar una pagina de error en vez del archivo.
        /// </summary>
        public static void VerificarPaquete(string archivo, Variante variante)
        {
            var info = new FileInfo(archivo);
            if (!info.Exists || info.Length == 0)
                throw new InvalidDataException(Strings.UpdateDownloadEmpty(archivo));

            if (variante == Variante.Portable)
            {
                using var stream = File.OpenRead(archivo);
                if (stream.ReadByte() != 'M' || stream.ReadByte() != 'Z')
                    throw new InvalidDataException(Strings.UpdateDownloadNotExecutable(archivo));
                return;
            }

            try
            {
                using var zip = ZipFile.OpenRead(archivo);
                if (zip.Entries.Any(x => string.Equals(x.FullName, EjecutableDelZip,
                        StringComparison.OrdinalIgnoreCase)))
                    return;
            }
            catch (InvalidDataException)
            {
                throw new InvalidDataException(Strings.UpdateDownloadNotZip(archivo));
            }

            throw new InvalidDataException(Strings.UpdateDownloadIncomplete(archivo, EjecutableDelZip));
        }

        /// <summary>
        /// Deja el paquete descargado listo para copiar y devuelve de donde sale el contenido.
        ///
        /// El Standard se descomprime entero antes de tocar nada: asi un zip cortado falla aca
        /// y no a mitad del reemplazo, con la instalacion ya modificada. El Portable ya es el
        /// archivo final y no hay nada que hacer.
        /// </summary>
        public static string Desplegar(string paquete, Variante variante, string carpetaTemporal)
        {
            if (variante == Variante.Portable)
                return paquete;

            var contenido = Path.Combine(carpetaTemporal, "contenido");
            if (Directory.Exists(contenido))
                Directory.Delete(contenido, true);
            ZipFile.ExtractToDirectory(paquete, contenido);
            return contenido;
        }

        /// <summary>
        /// Que archivo de la instalacion reemplaza cada archivo del paquete descargado.
        ///
        /// En el Portable es uno solo, y conserva el nombre que tenga puesto: quien lo bajo
        /// pudo haberlo renombrado, y la instalacion es ese archivo y no el nombre publicado.
        /// En el Standard son todos los del zip, cada uno en su misma ruta relativa.
        /// </summary>
        public static List<(string Relativo, string Origen)> Reemplazos(
            string paqueteDesplegado, Variante variante, string nombreDelEjecutable)
        {
            if (variante == Variante.Portable)
                return new List<(string, string)> { (nombreDelEjecutable, paqueteDesplegado) };

            return Directory.EnumerateFiles(paqueteDesplegado, "*", SearchOption.AllDirectories)
                .Select(origen => (
                    Path.GetRelativePath(paqueteDesplegado, origen),
                    origen))
                .OrderBy(x => x.Item1, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Pone los archivos nuevos en la carpeta de la aplicacion, corriendo los que estan a
        /// ".viejo".
        ///
        /// Si algo falla a mitad deshace todo lo hecho y vuelve a tirar. Una actualizacion a
        /// medias deja la instalacion rota, que es el unico desenlace que no se puede aceptar:
        /// mas vale seguir con la version vieja entera.
        /// </summary>
        public static void Reemplazar(string carpeta, IEnumerable<(string Relativo, string Origen)> archivos)
        {
            var hechos = new List<(string Destino, string? Viejo)>();
            try
            {
                foreach (var (relativo, origen) in archivos)
                {
                    var destino = Path.Combine(carpeta, relativo);
                    var carpetaDestino = Path.GetDirectoryName(destino);
                    if (!string.IsNullOrEmpty(carpetaDestino))
                        Directory.CreateDirectory(carpetaDestino);

                    string? viejo = null;
                    if (File.Exists(destino))
                    {
                        viejo = destino + SufijoViejo;
                        // Un ".viejo" de una actualizacion anterior que no se pudo borrar: se
                        // saca ahora, que es cuando sirve saber si se puede.
                        if (File.Exists(viejo))
                            File.Delete(viejo);
                        File.Move(destino, viejo);
                    }

                    // Copia y no movimiento: asi lo descargado queda intacto y un fallo se
                    // puede reintentar sin volver a bajar 78 MB.
                    File.Copy(origen, destino);
                    hechos.Add((destino, viejo));
                }
            }
            catch
            {
                Deshacer(hechos);
                throw;
            }
        }

        /// <summary>
        /// Vuelve atras lo que se alcanzo a reemplazar, en orden inverso.
        ///
        /// Lo recien copiado no lo tiene tomado nadie, asi que se puede borrar; lo renombrado
        /// vuelve a su nombre. Se hace lo que se pueda con cada archivo: si uno no se deja, que
        /// el resto igual se restaure deja la instalacion mas cerca de servir que dejarla como
        /// quedo.
        /// </summary>
        private static void Deshacer(List<(string Destino, string? Viejo)> hechos)
        {
            for (var i = hechos.Count - 1; i >= 0; i--)
            {
                var (destino, viejo) = hechos[i];
                try
                {
                    if (File.Exists(destino))
                        File.Delete(destino);
                    if (viejo != null && File.Exists(viejo))
                        File.Move(viejo, destino);
                }
                catch
                {
                    // Ya se esta atendiendo un fallo: lo que importa es intentar con todos.
                }
            }
        }

        /// <summary>
        /// Borra los ".viejo" que dejo una actualizacion anterior y devuelve cuantos saco.
        ///
        /// Se llama al arrancar, que es el unico momento en que esos archivos ya no los tiene
        /// tomados nadie. No puede tirar: un archivo que no se deja borrar no es motivo para no
        /// abrir la aplicacion, y en la proxima vuelta se reintenta.
        /// </summary>
        public static int LimpiarRestos(string carpeta)
        {
            var borrados = 0;
            try
            {
                foreach (var archivo in Directory.EnumerateFiles(
                             carpeta, "*" + SufijoViejo, SearchOption.AllDirectories))
                {
                    try
                    {
                        File.Delete(archivo);
                        borrados++;
                    }
                    catch
                    {
                        // Sigue tomado. Se reintenta en el arranque siguiente.
                    }
                }
            }
            catch
            {
                // Ni siquiera se pudo recorrer la carpeta. No hay nada que hacer aca.
            }

            return borrados;
        }

        /// <summary>Baja el paquete a <paramref name="destino"/>.</summary>
        public static void Descargar(string url, string destino)
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", UserAgent);
            http.Timeout = TimeSpan.FromMinutes(10);

            using var respuesta = http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).Result;
            respuesta.EnsureSuccessStatusCode();

            using var entrada = respuesta.Content.ReadAsStream();
            using var salida = File.Create(destino);
            entrada.CopyTo(salida);
        }

        /// <summary>
        /// El cuerpo de la release, para poder decir que cambia antes de actualizar. Null si no
        /// se pudo leer: es informativo y no vale la pena frenar la actualizacion por eso.
        /// </summary>
        public static string? NotasDe(string etiqueta)
        {
            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.Add("User-Agent", UserAgent);
                http.Timeout = TimeSpan.FromSeconds(15);

                var json = http.GetStringAsync(
                    $"https://api.github.com/repos/{RutaDelRepo}/releases/tags/{etiqueta}").Result;
                using var doc = JsonDocument.Parse(json);
                var cuerpo = doc.RootElement.TryGetProperty("body", out var nodo) ? nodo.GetString() : null;
                return string.IsNullOrWhiteSpace(cuerpo) ? null : cuerpo;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>"LordMordelon/RimworldExtractor", sacado de la URL para no repetirlo.</summary>
        private static string RutaDelRepo => new Uri(RepoUrl).AbsolutePath.Trim('/');

        private static bool MismaCarpeta(string a, string b) =>
            string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
                StringComparison.OrdinalIgnoreCase);
    }
}
