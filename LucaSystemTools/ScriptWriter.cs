using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using LucaSystemTools;
using ProtScript.Entity;
using Microsoft.VisualBasic.FileIO;
using CsvHelper;
using System.Globalization;
using CsvHelper.Configuration.Attributes;
using CsvHelper.Configuration;

namespace ProtScript
{
    public class ScriptWriter
    {
        // private FileStream fs;
        // private BinaryWriter bw;
        private string outpath;

        private Dictionary<string, byte> opcodeDict = new Dictionary<string, byte>();

        /// <summary>
        /// 第一遍写入生成dictLabel<label, pos>
        /// </summary>
        private Dictionary<string, uint> dictLabel = new Dictionary<string, uint>();
        /// <summary>
        /// 写入完后，遍历dictGoto<pos, label>在pos写入dictLabel[label]
        /// </summary>
        private Dictionary<int, string> dictGoto = new Dictionary<int, string>();

        private ScriptEntity script = new ScriptEntity();

        public ScriptWriter(string outpath, Dictionary<string, byte> dict)
        {
            // fs = new FileStream(outpath, FileMode.Create);
            this.outpath = outpath;
            opcodeDict = dict;
        }
        public void Close()
        {
            // bw.Close();
            // fs.Close();
        }
        public void WriteScript()
        {
            WriteParamData();

            using (BinaryWriter bw = new BinaryWriter(File.Open(outpath, FileMode.OpenOrCreate)))
            {
                foreach (var code in script.lines)
                {
                    if (Program.debug)
                    {
                        Console.WriteLine(bw.BaseStream.Position);
                        Console.WriteLine(code.ToString());
                    }
                    if (code.isLabel)
                    {
                        dictLabel.Add(code.label, (uint)bw.BaseStream.Position);
                    }
                    int codeLen = (int)bw.BaseStream.Position;

                    if (script.version == 2)
                    {
                        bw.Write(opcodeDict[code.opcode]);
                        bw.Write((byte)0x00);//长度填充
                        bw.Write(code.info.ToBytes(2));
                    }
                    else if (script.version == 3)
                    {
                        bw.Write(new byte[2]);//长度填充
                        bw.Write(opcodeDict[code.opcode]);
                        if (code.opcode == "END")
                        {
                            code.info.count++;
                        }
                        bw.Write(code.info.ToBytes());
                    }
                    foreach (var param in code.paramDatas)
                    {
                        if (param.bytes == null)
                        {
                            throw new Exception("语句解析错误！" + code.ToString() + "  参数为null！" + param.valueString);
                        }
                        if (code.isPosition && param.type == DataType.Position)
                        {
                            dictGoto.Add((int)bw.BaseStream.Position, param.valueString);
                            bw.Write(param.bytes);
                        }
                        else
                        {
                            bw.Write(param.bytes);
                        }
                    }
                    codeLen = (int)bw.BaseStream.Position - codeLen;
                    if (script.version == 2)
                    {
                        bw.BaseStream.Seek(-codeLen + 1, SeekOrigin.Current);
                        bw.Write((byte)Math.Ceiling(codeLen / 2.0));
                    }
                    else if (script.version == 3)
                    {
                        bw.BaseStream.Seek(-codeLen, SeekOrigin.Current);
                        bw.Write(BitConverter.GetBytes((UInt16)codeLen));
                    }
                    bw.BaseStream.Seek(codeLen - 2, SeekOrigin.Current);
                    if (codeLen % 2 != 0)
                    {
                        bw.Write((byte)0x00);
                    }
                }
                foreach (KeyValuePair<int, string> gotokv in dictGoto)
                {
                    bw.BaseStream.Seek(gotokv.Key, SeekOrigin.Begin);
                    bw.Write(BitConverter.GetBytes(dictLabel[gotokv.Value]));
                }
            }

        }
        private void WriteParamData()
        {
            for (int line = 0; line < script.lines.Count; line++)
            {
                for (int index = 0; index < script.lines[line].paramDatas.Count; index++)
                {
                    var paramData = script.lines[line].paramDatas[index];
                    script.lines[line].paramDatas[index] = ScriptEntity.ToParamData(paramData.valueString, paramData.type);
                }
            }
        }

        private void ReadJson(string path)
        {
            var jsonPath = path.Replace(".csv", ".json");
            JsonSerializerSettings jsetting = new JsonSerializerSettings();
            jsetting.DefaultValueHandling = DefaultValueHandling.Ignore;
            StreamReader sr = new StreamReader(jsonPath, Encoding.UTF8);
            script = JsonConvert.DeserializeObject<ScriptEntity>(sr.ReadToEnd(), jsetting);
            sr.Close();
        }

        private List<(string from, string to)> ReadTable(string path)
        {
            var table = new List<(string from, string to)>();
            var tablePath = Path.Combine(Path.GetDirectoryName(path), "TABLE.txt");
            if (!File.Exists(tablePath)) throw new Exception("TABLE.txt missing");
            StreamReader sr2 = new StreamReader(tablePath, Encoding.UTF8);
            var tableLines = sr2.ReadToEnd().Split("\n");
            sr2.Close();
            foreach (var line in tableLines)
            {
                var parts = line.Split("=");
                table.Add((parts[0].Trim(), parts[1].Trim()));
            }
            return table;
        }

        private Dictionary<string, string> ReadTableName(string path)
        {
            var tableName = new Dictionary<string, string>();
            var tableNamePath = Path.Combine(Path.GetDirectoryName(path), "TABLE_NAME.csv");
            if (!File.Exists(tableNamePath)) throw new Exception("TABLE_NAME.csv missing");
            StreamReader sr3 = new StreamReader(tableNamePath, Encoding.UTF8);
            var tableNameLines = sr3.ReadToEnd().Split("\n");
            sr3.Close();
            foreach (var line in tableNameLines)
            {
                var parts = line.Split(",");
                if (parts.Length == 2)
                {
                    tableName.Add(parts[0].Trim(), parts[1].Trim());
                }
            }
            return tableName;
        }

        private Dictionary<char, int> ReadFontWidth(string path)
        {
            var fontWidth = new Dictionary<char, int>();
            var fontWidthPath = Path.Combine(Path.GetDirectoryName(path), "FONT_WIDTH.txt");
            if (!File.Exists(fontWidthPath)) throw new Exception("FONT_WIDTH.txt missing");
            StreamReader sr4 = new StreamReader(fontWidthPath, Encoding.UTF8);
            var fontWidthLines = sr4.ReadToEnd().Split("\n");
            sr4.Close();
            foreach (var line in fontWidthLines)
            {
                var parts = line.Split("\t");
                if (parts.Length == 5)
                {
                    var widthHex = parts[4].Substring(2, 2);
                    var width = int.Parse(widthHex, System.Globalization.NumberStyles.HexNumber);
                    var character = char.Parse(parts[1]);
                    if (!fontWidth.ContainsKey(character))
                    {
                        fontWidth[character] = width;
                    }
                }
            }
            return fontWidth;
        }

        private int carryOverLineWidth = 0;

        private void ProcessCSVRow(
            List<(string from, string to)> table, 
            Dictionary<string, string> tableName, 
            Dictionary<char, int> fontWidth, 
            List<string> translatedLines,
            List<string> originalLines,
            string[] fields)
        {
            string ID = fields[0];
            string japanese = fields[1];
            string vietnamese = fields[2];
            string english = fields[3];
            var IDParts = ID.Split("_");
            var countAll = IDParts[0];
            var linePos = IDParts[1].Replace("[", "").Replace("]", "");
            var prefix = IDParts[2];
            var nameJp = IDParts[3];
            var nameEn = IDParts[4];

            // Read name
            string nameTranslated;
            tableName.TryGetValue(nameJp, out nameTranslated);
            nameJp = string.IsNullOrEmpty(nameJp) ? "" : nameJp;
            nameTranslated = string.IsNullOrEmpty(nameTranslated) ? nameJp : nameTranslated;

            // Replace char table
            foreach (var tableReplace in table)
            {
                vietnamese = vietnamese.Replace(tableReplace.from, tableReplace.to);
                nameTranslated = nameTranslated.Replace(tableReplace.from, tableReplace.to);
            }

            // Clean up wrong closing brackets
            if (vietnamese.EndsWith("' ")) vietnamese = vietnamese.Substring(0, vietnamese.Length - 2) + "”";

            // Clear new lines
            vietnamese = vietnamese.Replace("\n", " ").Replace("\r", "").Replace("$n", " ").Replace("  ", " ");

            // Calculate new line
            vietnamese = ProcessCSVRow_CaculateNewLine(vietnamese, fields[2], fontWidth);

            // Reduce font size
            // nameTranslated = "$S022" + nameTranslated;
            // vietnamese = "$S022" + vietnamese;

            // Construct string
            string fullLineOriginal = "";
            string fullLine = "";

            // Fix special cases
            if (prefix == "`" && !string.IsNullOrWhiteSpace(nameJp))
            {
                fullLineOriginal = string.Format("{0}{1}@{2}", prefix, nameJp, japanese);
                fullLine = vietnamese.Length > 0 ? string.Format("{0}{1}@{2}", prefix, nameTranslated, vietnamese) : fullLineOriginal;
            }
            else
            {
                fullLineOriginal = string.Format("{0}{1}", prefix, japanese);
                fullLine = vietnamese.Length > 0 ? string.Format("{0}{1}", prefix, vietnamese) : fullLineOriginal;
            }

            // Multiline fullscreen middle
            if (prefix == "$A1" && (japanese.Contains("\n") || vietnamese.Contains("\n") || vietnamese.Contains("$n")))
            {
                fullLine = fullLine.Replace("\n", "\n$A1").Replace("$n", "$n$A1");
                fullLineOriginal = fullLineOriginal.Replace("\n", "\n$A1").Replace("$n", "$n$A1");
            }
            // Both prefix and empty name
            else if (prefix == "$A1" && japanese.Contains('`') && japanese.Contains("@"))
            {
                // $A1`　@「ごめんなさい」 ==>  `　@$A1「ごめんなさい」
                var parts = japanese.Replace("$A1", "").Split("@");
                fullLineOriginal = parts[0] + "@" + "$A1" + parts[1];
                var parts2 = vietnamese.Replace("$A1", "").Split("@");
                if (parts2.Length == 2)
                {
                    fullLine = parts2[0] + "@" + "$A1" + parts2[1];
                }
            }
            // Has speaker prefix but no name
            else if (prefix == "`" && japanese.Contains("@") && string.IsNullOrWhiteSpace(nameJp))
            {
                fullLineOriginal = fullLineOriginal.Replace("`@", "`　@");
                fullLine = fullLine.Replace("`@", "`　@");
            }

            translatedLines.Add(fullLine);
            originalLines.Add(fullLineOriginal);
        }

        private string ProcessCSVRow_CaculateNewLine(string vietnamese, string vnBeforeReplace, Dictionary<char, int> fontWidth)
        {
            var lengthWidth = vnBeforeReplace.StartsWith(" ") ? carryOverLineWidth : 0;
            if (vietnamese.Contains("$3") || !vietnamese.Contains("$d"))
            {
                const int MAX_WIDTH = 1150; // 1300 //font20; // 
                var spaceIdx = -1;
                var newlineIndexes = new List<int>();
                for (var i = 0; i < vietnamese.Length; i++)
                {
                    var character = vietnamese[i];
                    // Ten Yurika = 75, lay 100 cho an toan
                    if (character.Equals("$") && vietnamese[i + 1].Equals("3"))
                    {
                        lengthWidth += 100;
                    }
                    // Char khac
                    else
                    {
                        var charWidth = fontWidth.ContainsKey(character) ? fontWidth[character] : 0;
                        lengthWidth += charWidth;
                    }
                    if (lengthWidth > MAX_WIDTH)
                    {
                        lengthWidth = 0;
                        if (character.Equals(' ')) spaceIdx = i;
                        if (spaceIdx > 0)
                        {
                            newlineIndexes.Add(spaceIdx);
                            i = spaceIdx + 1;
                            spaceIdx = -1;
                        }
                    }
                    else if (character.Equals(' '))
                    {
                        spaceIdx = i;
                    }

                }
                foreach (var newlineIdx in newlineIndexes)
                {
                    vietnamese = vietnamese.Remove(newlineIdx, 1).Insert(newlineIdx, "\n");
                    vnBeforeReplace = vnBeforeReplace.Remove(newlineIdx, 1).Insert(newlineIdx, "\n");
                }
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                if (newlineIndexes.Count > 2) Console.WriteLine("Warning: Line overflow at:\n{0}", vnBeforeReplace);
            }
            carryOverLineWidth = lengthWidth;
            return vietnamese;
        }

        private void WriteCsvToJson(List<(int lineIdx, int paramIdx)> indexes, List<string> translatedLines, List<string> originalLines, string outPath)
        {
            JsonSerializerSettings jsetting = new JsonSerializerSettings();
            jsetting.DefaultValueHandling = DefaultValueHandling.Ignore;

            var addCodeLines = new List<(int index, CodeLine line)>();

            for (var i = 0; i < indexes.Count; i++)
            {
                var index = indexes[i];
                var codeLine = script.lines[index.lineIdx];
                var translatedLine = translatedLines[i];
                var originalLine = originalLines[i];

                WriteCsvToJson_UpdateCodeLine(index, codeLine, translatedLine, originalLine);

                // Detect line overflow and add new line
                if (translatedLine.Split("\n").Length > 3)
                {
                    WriteCsvToJson_AddNewCodeLine(index, jsetting, translatedLine, codeLine, addCodeLines);
                }
            }

            // Add new Code line to script
            for (var i = 0; i < addCodeLines.Count; i++)
            {
                var insertIdx = addCodeLines[i].index + i;
                script.lines.Insert(insertIdx, addCodeLines[i].line);
            }

            // Write to file
            StreamWriter sw = new StreamWriter(outPath + ".json", false, Encoding.UTF8);
            string line = JsonConvert.SerializeObject(script, Formatting.Indented, jsetting);
            sw.WriteLine(line);
            sw.Close();
        }

        private void WriteCsvToJson_UpdateCodeLine((int lineIdx, int paramIdx) index, CodeLine codeLine, string translatedLine, string originalLine)
        {
            var textParam = codeLine.paramDatas[index.paramIdx];
            var lengthParam = codeLine.paramDatas[index.paramIdx - 1];

            // if (!textParam.valueString.Trim().Equals(originalLine.Trim()) && false) throw new Exception("ParamData doesn't contain original japanese text");

            byte[] bytes = Encoding.Unicode.GetBytes(translatedLine);
            textParam.valueString = translatedLine;
            textParam.valueOp = translatedLine;
            textParam.value = translatedLine;
            textParam.bytes = bytes;

            byte[] bytesLength = BitConverter.GetBytes(translatedLine.Length);
            lengthParam.valueString = translatedLine.Length.ToString();
            lengthParam.valueOp = (UInt16)translatedLine.Length;
            lengthParam.value = (UInt16)translatedLine.Length;
            lengthParam.bytes = bytesLength;
        }

        private void WriteCsvToJson_AddNewCodeLine((int lineIdx, int paramIdx) index, JsonSerializerSettings jsetting, string translatedLine, CodeLine codeLine, List<(int index, CodeLine line)> addCodeLines)
        {
            var newCodeLineStr = JsonConvert.SerializeObject(script.lines[index.lineIdx], Formatting.Indented, jsetting);
            var newCodeLine = JsonConvert.DeserializeObject<CodeLine>(newCodeLineStr);
            var newTextParam = newCodeLine.paramDatas[index.paramIdx];
            var newLengthParam = newCodeLine.paramDatas[index.paramIdx - 1];
            var lineName = translatedLine.Contains("@") ? translatedLine.Split("@")[0] : "";
            var lineParts = translatedLine.Split("\n");
            var newTranslatedLine = !string.IsNullOrEmpty(lineName) ? lineName + "@" + lineParts[lineParts.Length - 1] : lineParts[lineParts.Length - 1];

            byte[] newBytes = Encoding.Unicode.GetBytes(newTranslatedLine);
            newTextParam.valueString = newTranslatedLine;
            newTextParam.valueOp = newTranslatedLine;
            newTextParam.value = newTranslatedLine;
            newTextParam.bytes = newBytes;

            byte[] newBytesLength = BitConverter.GetBytes(newTranslatedLine.Length);
            newLengthParam.valueString = newTranslatedLine.Length.ToString();
            newLengthParam.valueOp = (UInt16)newTranslatedLine.Length;
            newLengthParam.value = (UInt16)newTranslatedLine.Length;
            newLengthParam.bytes = newBytesLength;

            // Remove voice line
            if (newCodeLine.info.data.Length == 3 && newCodeLine.info.data[2] != 0)
            {
                newCodeLine.info.data[2] = 0;
            }
            // Move animation from currnet line to the new code line
            if (newCodeLine.paramDatas.Count >= 6 && newCodeLine.paramDatas[5].valueString != "0x0100")
            {
                newCodeLine.paramDatas[4].valueString = codeLine.paramDatas[4].valueString;
                newCodeLine.paramDatas[4].valueOp = codeLine.paramDatas[4].valueOp;
                newCodeLine.paramDatas[4].value = codeLine.paramDatas[4].value;
                newCodeLine.paramDatas[4].bytes = codeLine.paramDatas[4].bytes;

                newCodeLine.paramDatas[5].valueString = codeLine.paramDatas[5].valueString;
                newCodeLine.paramDatas[5].valueOp = codeLine.paramDatas[5].valueOp;
                newCodeLine.paramDatas[5].value = codeLine.paramDatas[5].value;
                newCodeLine.paramDatas[5].bytes = codeLine.paramDatas[5].bytes;

                byte[] noAnimation2 = ScriptUtil.Hex2Byte("0x0BC2");
                codeLine.paramDatas[4].valueString = "0x0BC2";
                codeLine.paramDatas[4].valueOp = noAnimation2;
                codeLine.paramDatas[4].value = noAnimation2;
                codeLine.paramDatas[4].bytes = noAnimation2;

                byte[] noAnimation = ScriptUtil.Hex2Byte("0x0100");
                codeLine.paramDatas[5].valueString = "0x0100";
                codeLine.paramDatas[5].valueOp = noAnimation;
                codeLine.paramDatas[5].value = noAnimation;
                codeLine.paramDatas[5].bytes = noAnimation;
            }

            addCodeLines.Add((index.lineIdx + 1, newCodeLine));
        }

        private List<(int lineIdx, int paramIdx)> IndexCsvToScript()
        {
            var indexes = new List<(int lineIdx, int paramIdx)>();
            for (var i = 0; i < script.lines.Count; i++)
            {
                var line = script.lines[i];
                if (line.opcode == "MESSAGE" || line.opcode == "CHOICE" || line.opcode == "VARSTR")
                {
                    for (var j = 0; j < line.paramDatas.Count; j++)
                    {
                        var paramData = line.paramDatas[j];
                        if (paramData.type == DataType.StringUnicode && paramData.valueString != null && !string.IsNullOrEmpty(paramData.valueString))
                        {
                            // japanese
                            indexes.Add((i, j));
                            // english
                            // indexes.Add((i, j+2));
                            break;
                        }
                    }
                }
            }
            return indexes;
        }

        public class CSVFileRecord
        {
            [Index(0)]
            public string ID { get; set; }
            [Index(1)]
            public string Japanese { get; set; }
            [Index(2)]
            public string Vietnamese { get; set; }
            [Index(3)]
            public string English { get; set; }
        }

        public bool LoadCsv(string path, string outPath)
        {
            if (path.EndsWith(".json") || path.Contains("TABLE") || path.Contains("FONT")) return false;
            Console.WriteLine(Path.GetFileName(path));

            // Read Json
            ReadJson(path);

            // Read TABLE.txt
            var table = ReadTable(path);

            // Read TABLE_NAME.csv
            var tableName = ReadTableName(path);

            // Read FONT_WIDTH
            var fontWidth = ReadFontWidth(path);

            // Read CSV
            var translatedLines = new List<string>();
            var originalLines = new List<string>();
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = false,
            };
            using (var reader = new StreamReader(path))
            using (var csv = new CsvReader(reader, config))
            {
                while (csv.Read())
                {
                    var row = csv.GetRecord<CSVFileRecord>();
                    string[] fields = new string[4] { row.ID, row.Japanese, row.Vietnamese, row.English };
                    ProcessCSVRow(table, tableName, fontWidth, translatedLines, originalLines, fields);
                }
            }
            /*
            using (TextFieldParser parser = new TextFieldParser(path))
            {
                
                
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");
                while (!parser.EndOfData)
                {
                    // Process row
                    ProcessCSVRow(table, tableName, fontWidth, translatedLines, originalLines, parser.ReadFields());
                } 
            }*/

            // Match Csv line to script line
            var indexes = IndexCsvToScript();

            if (indexes.Count != translatedLines.Count) throw new Exception("Line numbers in json and csv doesn't match.");

            // Output Json
            WriteCsvToJson(indexes, translatedLines, originalLines, outPath);
            

            if (script.toolVersion > Program.toolVersion)
            {
                throw new Exception(String.Format("Tool version is {0}, but this file version is {1}!", Program.toolVersion, script.toolVersion));
            }
            return false;
        }

        public void LoadJson(string path)
        {
            Console.WriteLine(Path.GetFileName(path));
            JsonSerializerSettings jsetting = new JsonSerializerSettings();
            jsetting.DefaultValueHandling = DefaultValueHandling.Ignore;
            StreamReader sr = new StreamReader(path, Encoding.UTF8);
            script = JsonConvert.DeserializeObject<ScriptEntity>(sr.ReadToEnd(), jsetting);
            sr.Close();
            if (script.toolVersion > Program.toolVersion)
            {
                throw new Exception(String.Format("Tool version is {0}, but this file version is {1}!", Program.toolVersion, script.toolVersion));
            }

        }
        public void LoadLua(string path)
        {
            StreamReader sr = new StreamReader(path, Encoding.UTF8);
            script.lines.Clear();
            script.toolVersion = uint.MaxValue;
            if (sr.BaseStream.Length > 0)
            {
                string[] verstr = sr.ReadLine().Split(':');
                if (verstr.Length == 2)
                {
                    script.toolVersion = Convert.ToUInt32(verstr[1].Trim());
                }
                verstr = sr.ReadLine().Split(':');
                if (verstr.Length == 2)
                {
                    script.version = Convert.ToInt32(verstr[1].Trim());
                }
            }
            if (script.toolVersion > Program.toolVersion)
            {
                throw new Exception(String.Format("Tool version is {0}, but this file version is {1}!", Program.toolVersion, script.toolVersion));
            }
            string line = "";
            while ((line = sr.ReadLine()) != null)
            {
                script.lines.Add(new CodeLine(line));
            }
            sr.Close();
        }
        
    }
}
