using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RimworldExtractorInternal
{
    public class LibreExcelFixer : IDisposable
    {
        private readonly string _path;
        private readonly string _tmpRoot;
        private readonly string _tmpFilePath;

        public LibreExcelFixer(string path)
        {
            _path = path;
            _tmpRoot = Path.Combine(Path.GetTempPath(), nameof(LibreExcelFixer), Guid.NewGuid().ToString());
            _tmpFilePath = MakeCopy();
        }

        public string DoFix()
        {
            try
            {
                using var zip = ZipFile.Open(_tmpFilePath, ZipArchiveMode.Update);

                // Borra todos los archivos llamados comments##.xml
                List<ZipArchiveEntry> entryToRemove = new List<ZipArchiveEntry>();
                foreach (ZipArchiveEntry entry in zip.Entries)
                {
                    if (Regex.IsMatch(entry.Name, "^comments\\d+\\.xml$"))
                    {
                        entryToRemove.Add(entry);
                    }
                }
                foreach (ZipArchiveEntry entry in entryToRemove)
                {
                    entry.Delete();
                }
                
                // Abre los sheet##.xml.rels y borra todo nodo cuyo atributo Target sea un archivo comments
                for (int i = zip.Entries.Count -1; i >= 0; i--)
                {
                    ZipArchiveEntry entry = zip.Entries[i];
                    string fileName = entry.Name;

                    XDocument relsXDoc = new XDocument();
                    bool isEdited = false;
                    if (Regex.IsMatch(fileName, "^sheet\\d+\\.xml\\.rels$"))
                    {
                        using Stream relsStream = entry.Open();
                        relsXDoc = XDocument.Load(relsStream);

                        // Busca y borra ese nodo en el arbol xml
                        IEnumerable<XElement> elementsEnum = relsXDoc.Descendants();
                        for (int j = elementsEnum.Count() -1 ; j >= 0; j--)
                        {
                            XElement element = elementsEnum.ElementAt(j);
                            
                            string? attributeValue = element.Attribute("Target")?.Value;
                            
                            if (attributeValue != null &&
                                Regex.IsMatch(attributeValue, "./comments\\d+\\.xml$"))
                            {
                                element.Remove();
                                isEdited = true;
                            }
                        }
                    }

                    /*Borra la entrada original y guarda en su reemplazo el XDocument de arriba.
                      Nota del autor original: intento hacerlo directo sobre el stream, no le salio, y lo dejo asi.
                    */
                    if (isEdited)
                    {
                        string filePath = entry.FullName;
                        entry.Delete();
                        
                        ZipArchiveEntry newEntry = zip.CreateEntry(filePath);
                        Stream newStream = newEntry.Open();
                        relsXDoc.Save(newStream);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Err($"Failed to fix LibreOffice Excel file {_path}: {ex.Message}");
                return _tmpRoot;
            }

            return _tmpFilePath;
        }

        private string MakeCopy()
        {
            if (!Directory.Exists(_tmpRoot)) Directory.CreateDirectory(_tmpRoot);

            var fileName = Path.GetFileName(_path);
            var tmpFilePath = Path.Combine(_tmpRoot, fileName);
            if (!File.Exists(tmpFilePath))
            {
                File.Copy(_path, tmpFilePath);
            }

            return tmpFilePath;
        }

        public void Dispose()
        {
            if (Directory.Exists(_tmpRoot))
            {
                try
                {
                    Directory.Delete(_tmpRoot, true);
                }
                catch (Exception ex)
                {
                    Log.Err($"Failed to delete temporary directory {_tmpRoot}: {ex.Message}");
                }
            }
        }
    }
}
