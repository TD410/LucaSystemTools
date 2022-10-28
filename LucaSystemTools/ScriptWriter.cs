using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using LucaSystemTools;
using ProtScript.Entity;
using Microsoft.VisualBasic.FileIO;

namespace ProtScript
{
    public class ScriptWriter
    {
        private FileStream fs;
        private BinaryWriter bw;

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
            fs = new FileStream(outpath, FileMode.Create);
            bw = new BinaryWriter(fs);
            opcodeDict = dict;
        }
        public void Close()
        {
            bw.Close();
            fs.Close();
        }
        public void WriteScript()
        {
            WriteParamData();
            foreach (var code in script.lines)
            {
                if (Program.debug)
                {
                    Console.WriteLine(fs.Position);
                    Console.WriteLine(code.ToString());
                }
                if (code.isLabel)
                {
                    dictLabel.Add(code.label, (uint)fs.Position);
                }
                int codeLen = (int)fs.Position;

                if(script.version == 2)
                {
                    bw.Write(opcodeDict[code.opcode]);
                    bw.Write((byte)0x00);//长度填充
                    bw.Write(code.info.ToBytes(2));
                }
                else if(script.version == 3)
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
                    if(code.isPosition && param.type == DataType.Position)
                    {
                        dictGoto.Add((int)fs.Position, param.valueString);
                        bw.Write(param.bytes);
                    }
                    else
                    {
                        bw.Write(param.bytes);
                    }
                }
                codeLen = (int)fs.Position - codeLen;
                if (script.version == 2)
                {
                    fs.Seek(- codeLen + 1, SeekOrigin.Current);
                    bw.Write((byte)Math.Ceiling(codeLen / 2.0));
                }
                else if (script.version == 3)
                {
                    fs.Seek(-codeLen, SeekOrigin.Current);
                    bw.Write(BitConverter.GetBytes((UInt16)codeLen));
                }
                fs.Seek(codeLen - 2, SeekOrigin.Current);
                if (codeLen % 2 != 0)
                {
                    bw.Write((byte)0x00);
                }
            }
            foreach (KeyValuePair<int, string> gotokv in dictGoto)
            {
                fs.Seek(gotokv.Key, SeekOrigin.Begin);
                bw.Write(BitConverter.GetBytes(dictLabel[gotokv.Value]));
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

        public bool LoadCsv(string path)
        {
            if (path.EndsWith(".json") || path.Contains("TABLE")) return false;
            Console.WriteLine(Path.GetFileName(path));

            var jsonPath = path.Replace(".csv", ".json");

            // Read Json
            JsonSerializerSettings jsetting = new JsonSerializerSettings();
            jsetting.DefaultValueHandling = DefaultValueHandling.Ignore;
            StreamReader sr = new StreamReader(jsonPath, Encoding.UTF8);
            script = JsonConvert.DeserializeObject<ScriptEntity>(sr.ReadToEnd(), jsetting);
            sr.Close();

            // Read TABLE.txt
            var table = new List<(string from, string to)>();
            var tablePath = Path.Combine(Path.GetDirectoryName(path), "TABLE.txt");
            if (!File.Exists(tablePath)) throw new Exception("TABLE.txt missing");
            StreamReader sr2 = new StreamReader(tablePath, Encoding.UTF8);
            var tableLines = sr2.ReadToEnd().Split("\n");
            sr2.Close();
            foreach(var line in tableLines)
            {
                var parts = line.Split("=");
                table.Add((parts[0].Trim(), parts[1].Trim()));
            }

            // Read TABLE_NAME.csv
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

            // Read CSV
            var translatedLines = new List<string>();
            var originalLines = new List<string>();
            using (TextFieldParser parser = new TextFieldParser(path))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");
                while (!parser.EndOfData)
                {
                    //Process row
                    string[] fields = parser.ReadFields();
                    string ID = fields[0];
                    string japanese = fields[1];
                    string vietnamese = fields[2];
                    string english = fields[3];
                    var IDParts = ID.Split("_");
                    var countAll = IDParts[0];
                    var linePos = IDParts[1].Replace("[","").Replace("]","");
                    var prefix = IDParts[2];
                    var nameJp = IDParts[3];
                    var nameEn = IDParts[4];

                    foreach(var tableReplace in table)
                    {
                        vietnamese = vietnamese.Replace(tableReplace.from, tableReplace.to);
                    }
                    string nameTranslated;
                    tableName.TryGetValue(nameJp, out nameTranslated);
                    nameTranslated = string.IsNullOrWhiteSpace(nameTranslated) ? nameJp : (nameTranslated + "@");
                    nameJp = string.IsNullOrEmpty(nameJp) ? "" : (nameJp + "@");

                    string fullLine = string.Format("{0}{1}{2}", prefix, nameTranslated, vietnamese);
                    string fullLineOriginal = string.Format("{0}{1}{2}", prefix, nameJp, japanese);
                    // Multiline fullscreen
                    if (prefix == "$A1" && (fullLineOriginal.Contains("\n") || fullLine.Contains("$n") || fullLine.Contains("\n"))) {
                        fullLine = fullLine.Replace("\n", "\n$A1").Replace("$n", "$n$A1");
                        fullLineOriginal = fullLineOriginal.Replace("\n", "\n$A1").Replace("$n", "$n$A1");
                    }
                    // Both prefix and empty name
                    else if (prefix == "$A1" && fullLineOriginal.Contains('`') && fullLineOriginal.Contains("@"))
                    {
                        // $A1`　@「ごめんなさい」 ==>  `　@$A1「ごめんなさい」
                        var parts = fullLineOriginal.Replace("$A1","").Split("@");
                        fullLineOriginal = parts[0] + "@" + "$A1" + parts[1];
                        var parts2 = fullLine.Replace("$A1", "").Split("@");
                        if (parts2.Length == 2)
                        {
                            fullLine = parts2[0] + "@" + "$A1" + parts2[1];
                        }
                    }
                    // Has speaker prefix but no name
                    else if (prefix == "`" && fullLineOriginal.Contains("@") && string.IsNullOrWhiteSpace(nameJp))
                    {
                        fullLineOriginal = fullLineOriginal.Replace("`@", "`　@");
                        fullLine = fullLine.Replace("`@", "`　@");
                    }

                    translatedLines.Add(fullLine);
                    originalLines.Add(fullLineOriginal);
                }
            }

            // Match CSV with codeline
            var relatedLines = script.lines.FindAll(x =>
            {
                return (x.opcode == "MESSAGE" || x.opcode == "CHOICE")
                && x.paramDatas.Exists(y =>
                    y.type == DataType.StringUnicode
                    && !string.IsNullOrEmpty(y.valueString.Trim()));
            });

            if (relatedLines.Count == translatedLines.Count)
            {
                for (var i = 0; i < relatedLines.Count; i++)
                {
                    var codeLine = relatedLines[i];
                    var translatedLine = translatedLines[i];
                    var originalLine = originalLines[i];
                    var found = false;
                    foreach(var paramData in codeLine.paramDatas)
                    {
                        if (paramData.type == DataType.StringUnicode && paramData.valueString.Contains(originalLine))
                        {
                            paramData.valueString = translatedLine;
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        throw new Exception("ParamData doesn't contain original japanese text");
                    }
                }
            } else
            {
                throw new Exception("Line numbers in json and csv doesn't match.");
            }

            if (script.toolVersion > Program.toolVersion)
            {
                throw new Exception(String.Format("Tool version is {0}, but this file version is {1}!", Program.toolVersion, script.toolVersion));
            }
            return true;
        }

        public void LoadJson(string path)
        {
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
