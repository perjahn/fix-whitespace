using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FixWhitespace
{
    class Program
    {
        static int Main(string[] args)
        {
            var parsedArgs = args.ToList();
            bool dryrun = ExtractBoolFlag(parsedArgs, "-dryrun");
            int tabSize = ExtractIntFlag(parsedArgs, "-t") ?? 4;
            string[] excludeFiles = ExtractStringFlags(parsedArgs, "-x");

            Log($"Dry run: {dryrun}");
            Log($"Tab size: {tabSize}");
            Log(excludeFiles.Length == 0 ? "Exclude files: -" : $"Exclude files: '{string.Join("', '", excludeFiles)}'");

            if (parsedArgs.Count != 1)
            {
                Log($"Args: '{string.Join("', '", parsedArgs)}'");
                Log("Usage: FixWhitespace [-dryrun] [-t tabsize] [-x excludefile...] <filepattern>");
                return 1;
            }

            string pattern = parsedArgs[0];

            Log($"Using pattern: '{pattern}'");

            string[] files = Directory.GetFiles(".", pattern, SearchOption.AllDirectories)
                .Select(f => (f.StartsWith($".{Path.DirectorySeparatorChar}") || f.StartsWith($".{Path.AltDirectorySeparatorChar}")) ? f.Substring(2) : f)
                .ToArray();

            Array.Sort(files);

            files = ExcludeFiles(files, excludeFiles);

            Log($"Got {files.Length} files.");

            foreach (string filename in files)
            {
                Log($"Reading: '{filename}'");
                byte[] buf = File.ReadAllBytes(filename);

                if (buf.Length == 0)
                {
                    continue;
                }

                bool hasBom = buf[0] == 239 || buf[0] == 254 || buf[0] == 255;
                bool addRowAtEnd = buf[buf.Length - 1] == '\r' || buf[buf.Length - 1] == '\n';

                string[] rows = File.ReadAllLines(filename);

                bool modified = false;

                for (int row = 0; row < rows.Length; row++)
                {
                    if (rows[row] == string.Empty)
                    {
                        continue;
                    }

                    if (rows[row].All(c => c == ' ' || c == '\t'))
                    {
                        rows[row] = string.Empty;
                        modified = true;
                        continue;
                    }

                    if (rows[row].EndsWith(' ') || rows[row].EndsWith('\t'))
                    {
                        rows[row] = rows[row].TrimEnd();
                        modified = true;
                    }

                    int indentation = GetIndentation(rows[row], tabSize, out bool hadTabs);
                    //Log($"Indentation: {indentation}");
                    if (hadTabs || indentation % tabSize != 0)
                    {
                        int spaces = indentation % tabSize;
                        if (spaces > tabSize / 2)
                        {
                            //Log($"Spaces1: {spaces}");
                            indentation += tabSize - spaces;
                        }
                        else
                        {
                            //Log($"Spaces2: {spaces}");
                            indentation -= spaces;
                        }

                        //Log($"New indentation: {indentation}");

                        rows[row] = new string(' ', indentation) + rows[row].Trim();
                        modified = true;
                    }
                }

                if (modified)
                {
                    if (addRowAtEnd)
                    {
                        Array.Resize(ref rows, rows.Length + 1);
                        rows[rows.Length - 1] = string.Empty;
                    }

                    Log($"Saving: '{filename}'");
                    if (hasBom)
                    {
                        if (!dryrun)
                        {
                            File.WriteAllLines(filename, rows, Encoding.UTF8);
                        }
                    }
                    else
                    {
                        if (!dryrun)
                        {
                            File.WriteAllLines(filename, rows);
                        }
                    }
                }
            }

            return 0;
        }

        static bool ExtractBoolFlag(List<string> args, string flag)
        {
            for (int i = 0; i < args.Count; i++)
            {
                if (args[i] == flag)
                {
                    args.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        static int? ExtractIntFlag(List<string> args, string flag)
        {
            for (int i = 0; i < args.Count - 1; i++)
            {
                if (args[i] == flag && int.TryParse(args[i + 1], out int value))
                {
                    args.RemoveAt(i);
                    args.RemoveAt(i);
                    return value;
                }
            }
            return null;
        }

        static string[] ExtractStringFlags(List<string> args, string flag)
        {
            var returnValues = new List<string>();
            for (int i = 0; i < args.Count - 1;)
            {
                if (args[i] == flag)
                {
                    returnValues.Add(args[i + 1]);
                    args.RemoveAt(i);
                    args.RemoveAt(i);
                }
                else
                {
                    i++;
                }
            }
            return returnValues.ToArray();
        }

        static string[] ExcludeFiles(string[] files, string[] excludeFiles)
        {
            var returnFiles = new List<string>();

            foreach (string filename in files)
            {
                if (excludeFiles.Contains(Path.GetFileName(filename)))
                {
                    Log($"Excluding: '{filename}'");
                }
                else
                {
                    returnFiles.Add(filename);
                }
            }

            return returnFiles.ToArray();
        }

        static int GetIndentation(string row, int tabSize, out bool hadTabs)
        {
            int indentation = 0;

            hadTabs = false;

            for (int i = 0; i < row.Length && (row[i] == ' ' || row[i] == '\t'); i++)
            {
                if (row[i] == ' ')
                {
                    indentation++;
                }
                else
                {
                    hadTabs = true;

                    int overflow = indentation % tabSize;
                    if (overflow == 0)
                    {
                        indentation += tabSize;
                    }
                    else
                    {
                        indentation += tabSize - overflow;
                    }
                }
            }

            return indentation;
        }

        static void Log(string message)
        {
            Console.WriteLine(message);
        }
    }
}
