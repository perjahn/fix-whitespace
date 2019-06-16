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
                Log("Usage: FixWhitespace [-dryrun] [-t tabsize] [-x excludefile...] <path/pattern>");
                return 1;
            }

            string path, pattern;

            if (parsedArgs[0].Contains(Path.DirectorySeparatorChar) || parsedArgs[0].Contains(Path.AltDirectorySeparatorChar))
            {
                path = Path.GetDirectoryName(parsedArgs[0]);
                pattern = Path.GetFileName(parsedArgs[0]);
            }
            else
            {
                path = ".";
                pattern = parsedArgs[0];
            }

            string[] files = Directory.GetFiles(path, pattern, SearchOption.AllDirectories)
                .Select(f => (f.StartsWith($".{Path.DirectorySeparatorChar}") || f.StartsWith($".{Path.AltDirectorySeparatorChar}")) ? f.Substring(2) : f)
                .ToArray();

            Array.Sort(files);

            files = ExcludeFiles(files, excludeFiles);

            Log($"Got {files.Length} files.");

            foreach (string filename in files)
            {
                FixFile(filename, tabSize, dryrun);
            }

            return 0;
        }

        static void FixFile(string filename, int tabSize, bool dryrun)
        {
            Log($"Reading: '{filename}'");
            byte[] buf = File.ReadAllBytes(filename);

            if (buf.Length == 0)
            {
                return;
            }

            var newcontent = new List<byte>(buf.Length);

            bool modified = false;

            int startOfLine = 0;

            for (int i = 0; i < buf.Length; i++)
            {
                if (buf[i] == '\r' || buf[i] == '\n' || i == buf.Length - 1)
                {
                    if (i == startOfLine)
                    {
                        newcontent.Add(buf[i]);
                        startOfLine = i + 1;
                        continue;
                    }

                    byte[] row = (i == buf.Length - 1) ?
                        buf.Skip(startOfLine).Take(i - startOfLine + 1).ToArray() :
                        buf.Skip(startOfLine).Take(i - startOfLine).ToArray();

                    if (row.All(c => c == ' ' || c == '\t'))
                    {
                        if (i != buf.Length - 1)
                        {
                            newcontent.Add(buf[i]);
                        }
                        startOfLine = i + 1;
                        modified = true;
                        continue;
                    }

                    int indentation = GetIndentation(row, tabSize, out bool hadTabs, out int bytesOffset);
                    //Log($"Indentation: {indentation}");

                    int trailing = GetTrailing(row);
                    //Log($"Trailing: {trailing}");

                    if (hadTabs || indentation % tabSize != 0 || trailing > 0)
                    {
                        int spaces = indentation % tabSize;
                        int newindentation = indentation;
                        if (spaces > tabSize / 2)
                        {
                            //Log($"Spaces1: {spaces}");
                            newindentation += tabSize - spaces;
                        }
                        else
                        {
                            //Log($"Spaces2: {spaces}");
                            newindentation -= spaces;
                        }

                        //Log($"New indentation: {newindentation}");

                        newcontent.AddRange(Enumerable.Repeat((byte)' ', newindentation));
                        newcontent.AddRange(row.Skip(bytesOffset).Take(row.Length - bytesOffset - trailing));

                        startOfLine = i + 1;
                        modified = true;
                        continue;
                    }

                    newcontent.AddRange(row);
                    startOfLine = i + 1;
                }
            }

            if (modified)
            {
                Log($"Saving: '{filename}'");
                if (!dryrun)
                {
                    File.WriteAllBytes(filename, newcontent.ToArray());
                }
            }
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

        static int GetIndentation(byte[] row, int tabSize, out bool hadTabs, out int bytesOffset)
        {
            int indentation = 0;
            bytesOffset = 0;

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
                bytesOffset++;
            }

            return indentation;
        }

        static int GetTrailing(byte[] row)
        {
            int trailing = 0;

            for (int i = row.Length; i > 0 && (row[i - 1] == ' ' || row[i - 1] == '\t'); i--)
            {
                trailing++;
            }

            return trailing;
        }

        static void Log(string message)
        {
            Console.WriteLine(message);
        }
    }
}
