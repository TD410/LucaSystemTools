using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace LucaSystem.Utils
{
    public class CSVRecord
    {
        [Name("File")]
        public string File { get; set; }
        [Name("ID")]
        public string ID { get; set; }
        [Name("Japanese")]
        public string Japanese { get; set; }
        [Name("Vietnamese")]
        public string Vietnamese { get; set; }
        [Name("English")]
        public string English { get; set; }
    }

    internal class CSVParser : AbstractFileParser
    {
        public override void FileExport(string path, string outPath = null)
        {
            Console.WriteLine("Split CSV: {0}", path);
            var currentFile = "";
            var currentContent = "";
            if (outPath == null) outPath = path;
            using (var reader = new StreamReader(path))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Read();
                csv.ReadHeader();
                while (csv.Read())
                {
                    var row = csv.GetRecord<CSVRecord>();
                    if (row.File != currentFile)
                    {
                        if (currentFile.Length > 0)
                        {
                            Console.WriteLine(currentFile);
                            var fileOutPath = Path.Combine(outPath, currentFile);
                            StreamWriter sw = new StreamWriter(fileOutPath);
                            sw.Write(currentContent);
                            sw.Close();
                            currentContent = "";
                        }
                        currentFile = row.File;
                    }
                    var newLine = string.Format("{0},\"{1}\",\"{2}\",\"{3}\"\n", row.ID, row.Japanese, row.Vietnamese.Replace("\n", "$n").Replace("\"",""), row.English);
                    currentContent += newLine;
                }
            }
        }

        public override void FileImport(string path, string outpath = null)
        {
            throw new System.NotImplementedException();
        }
    }
}