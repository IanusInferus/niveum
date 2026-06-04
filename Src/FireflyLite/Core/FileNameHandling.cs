using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Firefly
{
    public static class FileNameHandling
    {
        public static string GetFileName(string FilePath)
        {
            if (FilePath == "") return "";
            int NameS = 0;
            int NameS2 = FilePath.Replace("/", "\\").IndexOf('\\', NameS);
            while (NameS2 != -1)
            {
                NameS = NameS2 + 1;
                NameS2 = FilePath.Replace("/", "\\").IndexOf('\\', NameS);
            }
            return FilePath.Substring(NameS);
        }
        public static string GetMainFileName(string FilePath)
        {
            if (FilePath == "") return "";
            int NameS = 0;
            int NameS2 = FilePath.Replace("/", "\\").IndexOf('\\', NameS);
            while (NameS2 != -1)
            {
                NameS = NameS2 + 1;
                NameS2 = FilePath.Replace("/", "\\").IndexOf('\\', NameS);
            }
            int NameE = FilePath.Length - 1;
            int NameE2 = FilePath.LastIndexOf('.', NameE);
            if (NameE2 != -1)
            {
                NameE = NameE2 - 1;
            }
            return FilePath.Substring(NameS, NameE - NameS + 1);
        }
        public static string GetExtendedFileName(string FilePath)
        {
            if (FilePath == "") return "";
            if (!FilePath.Contains(".")) return "";
            return FilePath.Substring(FilePath.LastIndexOf(".") + 1);
        }
        public static string GetFileDirectory(string FilePath)
        {
            if (FilePath == "") return "";
            int NameE = 0;
            int NameE2 = 0;
            while (NameE2 != -1)
            {
                NameE = NameE2 + 1;
                NameE2 = FilePath.Replace("/", "\\").IndexOf('\\', NameE);
            }
            return FilePath.Substring(0, NameE - 1);
        }
        public static string GetRelativePath(string FilePath, string BaseDirectory)
        {
            if (FilePath == "" || BaseDirectory == "") return FilePath;
            string a = FilePath.TrimEnd('\\').TrimEnd('/');
            string b = BaseDirectory.TrimEnd('\\').TrimEnd('/');
            string c;
            string d;

            c = PopFirstDir(ref a);
            d = PopFirstDir(ref b);
            if (!string.Equals(c, d, StringComparison.OrdinalIgnoreCase)) return FilePath;
            while (string.Equals(c, d, StringComparison.OrdinalIgnoreCase))
            {
                if (c == "" && a == "" && b == "") return ".";
                c = PopFirstDir(ref a);
                d = PopFirstDir(ref b);
            }

            a = (c + "\\" + a).TrimEnd('\\').TrimEnd('/');
            b = (d + "\\" + b).TrimEnd('\\').TrimEnd('/');

            while (PopFirstDir(ref b) != "")
            {
                a = "..\\" + a;
            }
            return a.Replace('\\', System.IO.Path.DirectorySeparatorChar);
        }
        public static string GetReducedPath(string Path)
        {
            var l = new Stack<string>();
            if (Path != "")
            {
                foreach (var d in Regex.Split(Path, "\\\\|/"))
                {
                    if (d == ".") continue;
                    if (d == "..")
                    {
                        if (l.Count > 0)
                        {
                            var p = l.Pop();
                            if (p == "..")
                            {
                                l.Push(p);
                                l.Push(d);
                            }
                        }
                        else
                        {
                            l.Push(d);
                        }
                        continue;
                    }
                    if (d.Contains(":")) l.Clear();
                    l.Push(d);
                }
            }
            return string.Join(System.IO.Path.DirectorySeparatorChar.ToString(), l.Reverse().ToArray());
        }
        public static string GetDirectoryPathWithoutTailingSeparator(string DirectoryPath)
        {
            if (DirectoryPath == "") return "";
            return DirectoryPath.TrimEnd('\\').TrimEnd('/');
        }
        public static string GetDirectoryPathWithTailingSeparator(string DirectoryPath)
        {
            var d = GetDirectoryPathWithoutTailingSeparator(DirectoryPath);
            if (d == "") return "";
            return d + System.IO.Path.DirectorySeparatorChar;
        }
        public static string GetAbsolutePath(string FilePath, string BaseDirectory)
        {
            BaseDirectory = GetDirectoryPathWithoutTailingSeparator(BaseDirectory);
            var s = new Stack<string>();
            if (BaseDirectory != "")
            {
                foreach (var d in Regex.Split(BaseDirectory, "\\\\|/"))
                {
                    if (d == ".") continue;
                    if (d == "..")
                    {
                        if (s.Count > 0)
                        {
                            var p = s.Pop();
                            if (p == "..")
                            {
                                s.Push(p);
                                s.Push(d);
                            }
                        }
                        else
                        {
                            s.Push(d);
                        }
                        continue;
                    }
                    if (d.Contains(":")) s.Clear();
                    s.Push(d);
                }
            }
            if (FilePath != "")
            {
                if (FilePath.StartsWith("\\") || FilePath.StartsWith("/")) s.Clear();
                foreach (var d in Regex.Split(FilePath, "\\\\|/"))
                {
                    if (d == ".") continue;
                    if (d == "..")
                    {
                        if (s.Count > 0)
                        {
                            var p = s.Pop();
                            if (p == "..")
                            {
                                s.Push(p);
                                s.Push(d);
                            }
                        }
                        else
                        {
                            s.Push(d);
                        }
                        continue;
                    }
                    if (d.Contains(":")) s.Clear();
                    s.Push(d);
                }
            }
            return string.Join(System.IO.Path.DirectorySeparatorChar.ToString(), s.Reverse().ToArray());
        }

        public static string PopFirstDir(ref string Path)
        {
            string ret;
            if (Path == "") return "";
            int NameS = 0;
            NameS = Path.Replace("/", "\\").IndexOf('\\', NameS);
            if (NameS < 0)
            {
                ret = Path;
                Path = "";
                return ret;
            }
            else
            {
                ret = Path.Substring(0, NameS);
                Path = Path.Substring(NameS + 1);
                return ret;
            }
        }
        public static string GetPath(string Directory, string FileName)
        {
            if (Directory == "") return FileName;
            Directory = Directory.TrimEnd('\\').TrimEnd('/');
            return (Directory + "\\" + FileName).Replace('\\', System.IO.Path.DirectorySeparatorChar);
        }
        public static string ChangeExtension(string FilePath, string Extension)
        {
            return System.IO.Path.ChangeExtension(FilePath, Extension);
        }

        public static bool IsMatchFileMask(string FileName, string Mask)
        {
            var Pattern = "^" + Regex.Escape(Mask).Replace("\\?", ".?").Replace("\\*", ".*?") + "$";
            var r = new Regex(Pattern, RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase);
            return r.Match(FileName).Success;
        }
    }
}
