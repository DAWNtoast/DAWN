using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ShatteredSail.Content;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEngine;

namespace ShatteredSail.Editor
{
    // Import this temporary audit tool into the isolated project's existing Editor assembly.
    public static class TextPolishPreviewSync
    {
        public static void SyncFromCommandLine() => GameplayPreviewSceneBuilder.SyncTextPolishFromCommandLine();
    }

    public static partial class GameplayPreviewSceneBuilder
    {
        [Serializable] private sealed class TextPolishPair { public string old; public string @new; }
        [Serializable] private sealed class TextPolishPairs { public TextPolishPair[] items; }
        [Serializable] private sealed class TextPolishSceneReport
        {
            public string path, backup, beforeSha256, afterSha256, nonTextSha256;
            public int textFields, changedTextFields, mappedOccurrences, forcedCrewFields;
            public bool nonTextBytesUnchanged, writtenAndVerified;
        }
        [Serializable] private sealed class TextPolishReport
        {
            public string project, utc, status, error;
            public int uniqueMappings, sceneCount, changedScenes;
            public bool dryRun;
            public TextPolishSceneReport[] scenes;
        }
        private sealed class TextPolishScalar
        {
            public int Start, Length;
            public string Value;
        }
        private sealed class TextPolishField
        {
            public string Id, Owner, Name, After;
            public TextPolishScalar Scalar;
            public bool Forced;
            public int Matches;
        }
        private sealed class TextPolishPlan
        {
            public string Path;
            public byte[] Before, After;
            public TextPolishSceneReport Report;
        }
        private static readonly Regex TextPolishBlocks = new Regex(@"(?ms)^--- !u!(\d+) &(-?\d+)[^\r\n]*\r?\n.*?(?=^--- !u!|\z)");
        private static readonly UTF8Encoding TextPolishUtf8 = new UTF8Encoding(false, true);

        public static void SyncTextPolishFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stopped edit mode required.");
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (string.Equals(project.TrimEnd('/', '\\'), Path.GetFullPath("D:/Game/UnityClient"), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("This temporary tool must run in an isolated copy, never the main project.");
            string artifacts = TextPolishAbsoluteArgument("-textPolishArtifactsPath");
            string output = TextPolishAbsoluteArgument("-textPolishOutputPath");
            if (!output.StartsWith(artifacts.TrimEnd('/', '\\') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Output must be a fresh child directory of the supplied artifacts directory.");
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new IOException("Refusing to overwrite a previous run: " + output);
            bool dryRun = Environment.GetCommandLineArgs().Contains("-textPolishValidateOnly");
            TextPolishParserSelfCheck();
            TextPolishPair[] pairs = LoadTextPolishPairs(artifacts);
            string sceneRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "_Project/Scenes"));
            string[] paths = Directory.GetFiles(sceneRoot, "*.unity", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            if (paths.Length != 189) throw new InvalidDataException("Expected the audited 189 scenes, found " + paths.Length);
            MethodInfo formatter = typeof(CoreGameplayDemoController).GetMethod("FormatCrewBiography", BindingFlags.NonPublic | BindingFlags.Static);
            if (formatter == null) throw new MissingMethodException("FormatCrewBiography");
            var plans = new List<TextPolishPlan>();
            // Prepare and validate every scene before writing any scene or backup.
            foreach (string path in paths)
            {
                AssertTextPolishPath(path, sceneRoot);
                plans.Add(PrepareTextPolishPlan(path, project, pairs, formatter));
            }
            var report = new TextPolishReport
            {
                project = project, utc = DateTime.UtcNow.ToString("O"), status = "prepared", dryRun = dryRun,
                uniqueMappings = pairs.Length, sceneCount = plans.Count,
                changedScenes = plans.Count(p => p.Report.changedTextFields > 0), scenes = plans.Select(p => p.Report).ToArray(),
            };
            Directory.CreateDirectory(output);
            string reportPath = Path.Combine(output, "report.json");
            if (File.Exists(reportPath)) throw new IOException("Report already exists: " + reportPath);
            try
            {
                foreach (TextPolishPlan plan in plans)
                {
                    if (!File.ReadAllBytes(plan.Path).SequenceEqual(plan.Before)) throw new IOException("Scene changed during preparation: " + plan.Path);
                    string backup = Path.GetFullPath(Path.Combine(output, "before", plan.Report.path));
                    AssertTextPolishPath(backup, output);
                    Directory.CreateDirectory(Path.GetDirectoryName(backup));
                    WriteTextPolishNewFile(backup, plan.Before);
                    plan.Report.backup = backup;
                }
                if (!dryRun)
                {
                    foreach (TextPolishPlan plan in plans.Where(p => p.Report.changedTextFields > 0))
                    {
                        if (!File.ReadAllBytes(plan.Path).SequenceEqual(plan.Before)) throw new IOException("Scene changed before write: " + plan.Path);
                        File.WriteAllBytes(plan.Path, plan.After);
                        if (!File.ReadAllBytes(plan.Path).SequenceEqual(plan.After)) throw new IOException("Scene write verification failed: " + plan.Path);
                        plan.Report.writtenAndVerified = true;
                    }
                }
                report.status = dryRun ? "validated-without-scene-writes" : "complete";
            }
            catch (Exception exception)
            {
                report.status = "failed"; report.error = exception.ToString();
                throw;
            }
            finally { WriteTextPolishNewFile(reportPath, TextPolishUtf8.GetBytes(JsonUtility.ToJson(report, true))); }
            // No OpenScene, SaveScene, scene rebuild, or refresh that could serialize other fields.
            Debug.Log("TEXT_POLISH_PREVIEW_SYNC " + report.status + ": " + report.sceneCount + " scenes, " + report.changedScenes + " changed. " + reportPath);
        }

        private static string TextPolishAbsoluteArgument(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, key);
            if (index < 0 || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1]))
                throw new ArgumentException("Required absolute path argument: " + key);
            return Path.GetFullPath(args[index + 1]).TrimEnd('/', '\\');
        }

        private static void AssertTextPolishPath(string path, string root)
        {
            path = Path.GetFullPath(path); root = Path.GetFullPath(root).TrimEnd('/', '\\');
            if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("Out-of-scope path: " + path);
            for (string cursor = path; !string.Equals(cursor, root, StringComparison.OrdinalIgnoreCase); cursor = Path.GetDirectoryName(cursor))
                if ((File.Exists(cursor) || Directory.Exists(cursor)) && (File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Reparse point is not allowed in scene or backup path: " + cursor);
        }

        private static TextPolishPair[] LoadTextPolishPairs(string root)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string stem in new[] { "root", "ui", "crew", "item" })
            {
                string path = Path.Combine(root, stem + "-text-replacements.json");
                string json = File.ReadAllText(path, TextPolishUtf8).Trim();
                if (!json.StartsWith("[", StringComparison.Ordinal) || !json.EndsWith("]", StringComparison.Ordinal))
                    throw new InvalidDataException("Expected JSON replacement array: " + path);
                TextPolishPairs parsed = JsonUtility.FromJson<TextPolishPairs>("{\"items\":" + json + "}");
                if (parsed?.items == null) throw new InvalidDataException("Invalid replacement array: " + path);
                foreach (TextPolishPair pair in parsed.items)
                {
                    if (pair == null || string.IsNullOrEmpty(pair.old) || pair.@new == null) throw new InvalidDataException("Empty old/null new mapping in " + path);
                    if (values.TryGetValue(pair.old, out string prior) && prior != pair.@new)
                        throw new InvalidDataException("Conflicting replacements for " + CrewBiographyYamlString(pair.old) + " in " + path);
                    values[pair.old] = pair.@new;
                }
            }
            return values.Select(p => new TextPolishPair { old = p.Key, @new = p.Value })
                .OrderByDescending(p => p.old.Length).ThenBy(p => p.old, StringComparer.Ordinal).ToArray();
        }

        private static TextPolishPlan PrepareTextPolishPlan(string path, string project, TextPolishPair[] pairs, MethodInfo formatter)
        {
            byte[] beforeBytes = File.ReadAllBytes(path);
            bool bom = beforeBytes.Length >= 3 && beforeBytes[0] == 239 && beforeBytes[1] == 187 && beforeBytes[2] == 191;
            string before = TextPolishUtf8.GetString(beforeBytes, bom ? 3 : 0, beforeBytes.Length - (bom ? 3 : 0));
            var objectBlocks = new Dictionary<string, FacilityNavigationYamlObject>(StringComparer.Ordinal);
            var fields = new List<TextPolishField>();
            foreach (Match match in TextPolishBlocks.Matches(before))
            {
                var block = new FacilityNavigationYamlObject { ClassId = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), Id = match.Groups[2].Value, Value = match.Value };
                objectBlocks.Add(block.Id, block);
                if (block.ClassId != 114 || !block.Value.Contains("guid: 5f7201a12d95ffc409449d95f23cf332,") || !block.Value.Contains("  m_FontData:")) continue;
                TextPolishScalar scalar = ReadTextPolishScalar(block.Value, "m_Text");
                scalar.Start += match.Index;
                fields.Add(new TextPolishField { Id = block.Id, Owner = TextPolishReference(block.Read("m_GameObject")), Scalar = scalar });
            }
            foreach (TextPolishField field in fields)
            {
                if (!objectBlocks.TryGetValue(field.Owner, out FacilityNavigationYamlObject owner) || owner.ClassId != 1)
                    throw new InvalidDataException("Text owner missing in " + path);
                field.Name = ReadTextPolishScalar(owner.Value, "m_Name").Value;
                field.After = ApplyTextPolishOnce(field.Scalar.Value, pairs, out field.Matches);
                // Every encountered scalar must survive our encoder, even if it does not match an edit.
                string encoded = "  m_Text: " + CrewBiographyYamlString(field.Scalar.Value) + "\n";
                if (ReadTextPolishScalar(encoded, "m_Text").Value != field.Scalar.Value) throw new InvalidDataException("Scalar codec mismatch: " + path + " / " + field.Id);
            }
            ForceTextPolishCrew(fields, objectBlocks, formatter, path);
            string after = RewriteTextPolishFields(before, fields, false);
            string maskedBefore = RewriteTextPolishFields(before, fields, true);
            // Re-parse the resulting text spans, independently of the original lengths.
            var afterFields = new List<TextPolishField>();
            foreach (Match match in TextPolishBlocks.Matches(after))
            {
                if (match.Groups[1].Value != "114" || !match.Value.Contains("guid: 5f7201a12d95ffc409449d95f23cf332,") || !match.Value.Contains("  m_FontData:")) continue;
                TextPolishScalar scalar = ReadTextPolishScalar(match.Value, "m_Text"); scalar.Start += match.Index;
                afterFields.Add(new TextPolishField { Id = match.Groups[2].Value, Scalar = scalar, After = scalar.Value });
            }
            if (fields.Count != afterFields.Count) throw new InvalidDataException("Text component count changed: " + path);
            for (int i = 0; i < fields.Count; i++)
                if (fields[i].Id != afterFields[i].Id || fields[i].After != afterFields[i].Scalar.Value)
                    throw new InvalidDataException("Resulting Text differs from plan: " + path + " / " + fields[i].Id);
            string maskedAfter = RewriteTextPolishFields(after, afterFields, true);
            byte[] maskBytes = EncodeTextPolishFile(maskedBefore, bom);
            if (!maskBytes.SequenceEqual(EncodeTextPolishFile(maskedAfter, bom))) throw new InvalidDataException("Non-m_Text YAML bytes changed: " + path);
            byte[] afterBytes = EncodeTextPolishFile(after, bom);
            string relative = path.Substring(project.TrimEnd('/', '\\').Length + 1).Replace('\\', '/');
            return new TextPolishPlan
            {
                Path = path, Before = beforeBytes, After = afterBytes,
                Report = new TextPolishSceneReport
                {
                    path = relative, beforeSha256 = HashTextPolish(beforeBytes), afterSha256 = HashTextPolish(afterBytes), nonTextSha256 = HashTextPolish(maskBytes),
                    textFields = fields.Count, changedTextFields = fields.Count(f => f.Scalar.Value != f.After),
                    mappedOccurrences = fields.Sum(f => f.Matches), forcedCrewFields = fields.Count(f => f.Forced && f.Scalar.Value != f.After), nonTextBytesUnchanged = true,
                },
            };
        }

        private static void ForceTextPolishCrew(List<TextPolishField> fields, Dictionary<string, FacilityNavigationYamlObject> blocks, MethodInfo formatter, string path)
        {
            var rects = blocks.Values.Where(b => b.ClassId == 224).ToDictionary(b => TextPolishReference(b.Read("m_GameObject")), StringComparer.Ordinal);
            string Dossier(string owner)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                while (rects.TryGetValue(owner, out FacilityNavigationYamlObject rect) && seen.Add(owner))
                {
                    if (ReadTextPolishScalar(blocks[owner].Value, "m_Name").Value == "Tavern Crew Detail") return owner;
                    string parent = TextPolishReference(rect.Read("m_Father"));
                    if (parent == "0") break;
                    if (!blocks.TryGetValue(parent, out FacilityNavigationYamlObject parentRect) || parentRect.ClassId != 224) break;
                    owner = TextPolishReference(parentRect.Read("m_GameObject"));
                }
                return null;
            }
            foreach (TextPolishField name in fields.Where(f => f.Name == "Tavern Crew Detail Name"))
            {
                CrewVariantDefinition crew = CrewNarrativeCatalog.AllCrew.SingleOrDefault(c => c.DisplayName == name.Scalar.Value);
                if (crew == null) throw new InvalidDataException("Unknown authored crew display name: " + path + " / " + name.Scalar.Value);
                string dossier = Dossier(name.Owner);
                if (dossier == null) throw new InvalidDataException("Crew dossier ancestor missing: " + path);
                foreach (string label in new[] { "Tavern Crew Biography", "Tavern Crew Story" })
                {
                    TextPolishField[] found = fields.Where(f => f.Name == label && Dossier(f.Owner) == dossier).ToArray();
                    if (found.Length != 1) throw new InvalidDataException("Expected one " + label + " in dossier: " + path);
                    TextPolishField target = found[0];
                    if (target.Forced) throw new InvalidDataException("Multiple crew identities in the same dossier: " + path);
                    target.After = label == "Tavern Crew Biography" ? (string)formatter.Invoke(null, new object[] { crew.Biography }) : CrewNarrativeCatalog.StoryFor(crew).Body;
                    target.Matches = 0; target.Forced = true;
                }
            }
        }

        private static string TextPolishReference(string value)
        {
            Match match = Regex.Match(value, @"^\{fileID: (-?\d+)\}$");
            if (!match.Success) throw new InvalidDataException("Invalid local YAML reference: " + value);
            return match.Groups[1].Value;
        }

        private static string ApplyTextPolishOnce(string value, TextPolishPair[] pairs, out int count)
        {
            count = 0;
            // Whole-value matches take precedence, including short labels.
            foreach (TextPolishPair pair in pairs)
                if (value == pair.old) { count = pair.old == pair.@new ? 0 : 1; return pair.@new; }
            var result = new StringBuilder();
            for (int position = 0; position < value.Length;)
            {
                TextPolishPair match = null;
                foreach (TextPolishPair pair in pairs)
                    if (pair.old.Length >= 8 && position + pair.old.Length <= value.Length &&
                        string.CompareOrdinal(value, position, pair.old, 0, pair.old.Length) == 0) { match = pair; break; }
                if (match == null) result.Append(value[position++]);
                else { result.Append(match.@new); position += match.old.Length; if (match.old != match.@new) count++; }
            }
            return result.ToString();
        }

        private static string RewriteTextPolishFields(string source, List<TextPolishField> fields, bool mask)
        {
            var result = new StringBuilder(source.Length);
            int cursor = 0;
            foreach (TextPolishField field in fields.OrderBy(f => f.Scalar.Start))
            {
                if (field.Scalar.Start < cursor) throw new InvalidDataException("Overlapping text spans.");
                result.Append(source, cursor, field.Scalar.Start - cursor);
                result.Append(mask ? "<UI-TEXT:" + field.Id + ">" : field.Scalar.Value == field.After
                    ? source.Substring(field.Scalar.Start, field.Scalar.Length) : CrewBiographyYamlString(field.After));
                cursor = field.Scalar.Start + field.Scalar.Length;
            }
            return result.Append(source, cursor, source.Length - cursor).ToString();
        }

        private static TextPolishScalar ReadTextPolishScalar(string block, string key)
        {
            MatchCollection matches = Regex.Matches(block, @"(?m)^  " + Regex.Escape(key) + @":[ \t]*");
            if (matches.Count != 1) throw new InvalidDataException("Expected one YAML field: " + key);
            int start = matches[0].Index + matches[0].Length, position = start;
            var result = new StringBuilder();
            if (position == block.Length || block[position] == '\r' || block[position] == '\n') return new TextPolishScalar { Start = start, Length = 0, Value = string.Empty };
            char quote = block[position];
            if (quote != '"' && quote != '\'')
            {
                if (quote == '|' || quote == '>') throw new InvalidDataException("Unsupported block scalar; audited scenes use plain/quoted Text only.");
                int lineEnd = TextPolishLineEnd(block, position);
                result.Append(block.Substring(position, lineEnd - position).TrimEnd(' ', '\t'));
                position = lineEnd;
                while (position < block.Length)
                {
                    int next = TextPolishAfterNewline(block, position), content = next;
                    while (content < block.Length && (block[content] == ' ' || block[content] == '\t')) content++;
                    if (content - next < 4 || content == block.Length || block[content] == '\r' || block[content] == '\n') break;
                    lineEnd = TextPolishLineEnd(block, content);
                    result.Append(' ').Append(block.Substring(content, lineEnd - content).TrimEnd(' ', '\t'));
                    position = lineEnd;
                }
                return new TextPolishScalar { Start = start, Length = position - start, Value = result.ToString() };
            }
            position++;
            while (position < block.Length)
            {
                char c = block[position++];
                if (c == quote)
                {
                    if (quote == '\'' && position < block.Length && block[position] == '\'') { result.Append('\''); position++; continue; }
                    int tail = position;
                    while (tail < block.Length && (block[tail] == ' ' || block[tail] == '\t')) tail++;
                    if (tail < block.Length && block[tail] != '\r' && block[tail] != '\n' && block[tail] != '#') throw new InvalidDataException("Unexpected quoted scalar suffix.");
                    return new TextPolishScalar { Start = start, Length = position - start, Value = result.ToString() };
                }
                if (c == '\r' || c == '\n') { position--; AppendTextPolishFold(block, ref position, result, false); continue; }
                if ((c == ' ' || c == '\t') && TextPolishOnlySpaceToNewline(block, position)) continue;
                if (c != '\\' || quote != '"') { result.Append(c); continue; }
                if (position >= block.Length) throw new InvalidDataException("Unterminated escape.");
                c = block[position++];
                if (c == '\r' || c == '\n') { position--; AppendTextPolishFold(block, ref position, result, true); continue; }
                switch (c)
                {
                    case '0': result.Append('\0'); break;
                    case 'a': result.Append('\a'); break;
                    case 'b': result.Append('\b'); break;
                    case 't': result.Append('\t'); break;
                    case 'n': result.Append('\n'); break;
                    case 'v': result.Append('\v'); break;
                    case 'f': result.Append('\f'); break;
                    case 'r': result.Append('\r'); break;
                    case 'e': result.Append((char)27); break;
                    case ' ': result.Append(' '); break;
                    case '"': result.Append('"'); break;
                    case '/': result.Append('/'); break;
                    case '\\': result.Append('\\'); break;
                    case 'N': result.Append('\u0085'); break;
                    case '_': result.Append('\u00a0'); break;
                    case 'L': result.Append('\u2028'); break;
                    case 'P': result.Append('\u2029'); break;
                    case 'x': case 'u': case 'U':
                        int length = c == 'x' ? 2 : c == 'u' ? 4 : 8;
                        if (position + length > block.Length || !uint.TryParse(block.Substring(position, length), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint code))
                            throw new InvalidDataException("Invalid Unicode escape.");
                        if (c == 'U') result.Append(char.ConvertFromUtf32(checked((int)code)));
                        else result.Append((char)code);
                        position += length; break;
                    default: throw new InvalidDataException("Unsupported YAML escape: " + c);
                }
            }
            throw new InvalidDataException("Unterminated quoted YAML scalar.");
        }

        private static bool TextPolishOnlySpaceToNewline(string source, int position)
        {
            while (position < source.Length && (source[position] == ' ' || source[position] == '\t')) position++;
            return position < source.Length && (source[position] == '\r' || source[position] == '\n');
        }
        private static int TextPolishLineEnd(string source, int position)
        { while (position < source.Length && source[position] != '\r' && source[position] != '\n') position++; return position; }
        private static int TextPolishAfterNewline(string source, int position)
        {
            if (position < source.Length && source[position] == '\r') position++;
            if (position < source.Length && source[position] == '\n') position++;
            return position;
        }
        private static void AppendTextPolishFold(string source, ref int position, StringBuilder result, bool escaped)
        {
            int breaks = 0;
            while (position < source.Length && (source[position] == '\r' || source[position] == '\n'))
            {
                position = TextPolishAfterNewline(source, position); breaks++;
                while (position < source.Length && (source[position] == ' ' || source[position] == '\t')) position++;
            }
            if (breaks > 1) result.Append('\n', breaks - 1);
            else if (!escaped) result.Append(' ');
        }
        private static byte[] EncodeTextPolishFile(string text, bool bom)
        {
            byte[] content = TextPolishUtf8.GetBytes(text);
            if (!bom) return content;
            return new byte[] { 239, 187, 191 }.Concat(content).ToArray();
        }
        private static string HashTextPolish(byte[] bytes)
        { using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant(); }
        private static void WriteTextPolishNewFile(string path, byte[] bytes)
        { using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { file.Write(bytes, 0, bytes.Length); file.Flush(true); } }

        private static void TextPolishParserSelfCheck()
        {
            void Check(string yaml, string expected)
            { if (ReadTextPolishScalar("  m_Text: " + yaml + "\n  m_Other: keep\n", "m_Text").Value != expected) throw new InvalidDataException("YAML parser self-check failed."); }
            Check("\"  \\u8239\\xB7\\0\\n\\t\\\\\\\"  \"", "  船·\0\n\t\\\"  ");
            Check("\"甲\r\n    乙\"", "甲 乙");
            Check("\"甲\\\r\n    乙\"", "甲乙");
            Check("\"甲\n\n    乙\"", "甲\n乙");
            Check("'captain''s  chart'", "captain's  chart");
            Check("plain\n    continuation", "plain continuation");
            Check(string.Empty, string.Empty);
            Check("\"\\U0001F30A\"", "🌊");
            var pairs = new[] { new TextPolishPair { old = "123456789", @new = "abcdefgh" }, new TextPolishPair { old = "abcdefgh", @new = "wrong" }, new TextPolishPair { old = "12345678", @new = "short" }, new TextPolishPair { old = "船舱", @new = "物品" } };
            if (ApplyTextPolishOnce("x123456789船舱", pairs, out int matches) != "xabcdefgh船舱" || matches != 1 || ApplyTextPolishOnce("船舱", pairs, out matches) != "物品")
                throw new InvalidDataException("Non-cascading longest-match self-check failed.");
        }
    }
}
