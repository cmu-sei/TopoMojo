// Copyright 2021 Carnegie Mellon University.
// Released under a MIT (SEI) license. See LICENSE.md in the project root.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace TopoMojo.Api.Extensions
{
    public static class StringExtensions
    {
        public static bool IsEmpty(this string s)
        {
            return (String.IsNullOrEmpty(s));
        }

        public static bool NotEmpty(this string s)
        {
            return (!String.IsNullOrWhiteSpace(s));
        }

        //check for presence of array values
        public static bool IsEmpty(this object[] o)
        {
            return (o == null || o.Length == 0);
        }

        public static bool IsNotEmpty(this object[] o)
        {
            return (o != null && o.Length > 0);
        }

        //expands a range string (i.e. [1-3,5,7,10-12]) into an int list
        public static int[] ExpandRange(this string s)
        {
            s = s.Inner();

            List<int> list = new List<int>();
            string[] sections = s.Split(',');
            foreach (string section in sections)
            {
                //Console.WriteLine(section);
                string[] token = section.Split('-');
                int x = 0, y = 0;
                if (Int32.TryParse(token[0], out x))
                {
                    if (token.Length > 1)
                    {
                        if (!Int32.TryParse(token[1], out y))
                            y = x;
                    }
                    else
                    {
                        y = x;
                    }
                    for (int i = x; i <= y; i++)
                    {
                        //Console.WriteLine(i);
                        list.Add(i);
                    }
                }
            }
            return list.ToArray();
        }

        //extracts string from brackets [].
        public static string Inner(this string s)
        {
            if (s == null)
                s = "";

            int x = s.IndexOf('[');
            if (x > -1)
            {
                int y = s.IndexOf(']', x);
                if (x > -1 && y > -1)
                    s = s.Substring(x + 1, y - x - 1);
            }
            return s.Trim();
        }

        // returns first token following #
        public static string Tag(this string s)
        {
            if (s.NotEmpty())
            {
                int x = s.IndexOf("#");
                if (x >= 0)
                    return s.Substring(x+1).Split(' ').First();
            }
            return "";
        }

        //strips hashtag+ from string
        public static string Untagged(this string s)
        {
            if (s.NotEmpty())
            {
                int x = s.IndexOf("#");
                if (x >= 0)
                    return s.Substring(0, x);
            }
            return s;
        }

        public static string ExtractBefore(this string s, string target)
        {
            int x = s.IndexOf(target);
            return (x>-1)
                ? s.Substring(0, x)
                : s;
        }
        public static string ExtractAfter(this string s, string target)
        {
            int x = s.IndexOf(target);
            return (x>-1)
                ? s.Substring(x+1)
                : s;
        }

        public static string WithoutSymbols(this string s)
        {
            return new string(
                s.ToCharArray()
                .Where(c => c < 128 && char.IsLetterOrDigit(c))
                .ToArray()
            );
        }

        //Note: this assumes a guid string (length > 16)
        public static string ToSwitchName(this string s)
        {
            return String.Format("sw#{0}..{1}", s.Substring(0,8), s.Substring(s.Length-8));
        }

        public static string ToAbbreviatedHex(this string s)
        {
            return (s.Length > 8)
                ? String.Format("{0}..{1}", s.Substring(0, 4), s.Substring(s.Length - 4))
                : s;
        }

        public static string ToSlug(this string target)
        {
            string result = "";

            bool duplicate = false;

            foreach (char c in target.ToCharArray())
            {
                if (char.IsLetterOrDigit(c))
                {
                    result += c;

                    duplicate = false;
                }
                else
                {
                    if (!duplicate)
                        result += '-';

                    duplicate = true;
                }
            }

            if (result.EndsWith('-'))
                result = result.Substring(0, result.Length - 1);

            return result.ToLower();
        }

        public static int ToSeconds(this string ts)
        {
            if (ts == string.Empty)
                return 0;

            if (int.TryParse(ts.Substring(0, ts.Length - 1), out int value))
            {
                char type = ts.Trim().ToCharArray().Last();
                int factor = 1;

                switch (type)
                {
                    case 'y':
                    factor = 86400 * 365;
                    break;

                    case 'w':
                    factor = 86400 * 7;
                    break;

                    case 'd':
                    factor = 86400;
                    break;

                    case 'h':
                    factor = 3600;
                    break;

                    case 'm':
                    factor = 60;
                    break;
                }

                return value * factor;
            }

            throw new ArgumentException("invalid simple-timespan");
        }

        public static DateTimeOffset ToDatePast(this string ts)
        {
            return DateTimeOffset.UtcNow
                .Subtract(
                    new TimeSpan(
                        0,
                        0,
                        ts.ToSeconds()
                    )
                );
        }

        public static string Sanitize(this string target, char[] exclude)
        {
            string p = "";

            foreach (char c in target.ToCharArray())
                if (!exclude.Contains(c))
                    p += c;

            return p.Replace(" ", "_");
        }

        public static string SanitizeFilename(this string target)
        {
            return target.Sanitize(Path.GetInvalidFileNameChars());
        }

        public static string SanitizePath(this string target)
        {
            return target.Sanitize(Path.GetInvalidPathChars());
        }

        public static string ToSha256(this string input)
        {
            using (SHA256 alg = SHA256.Create())
            {
                return BitConverter.ToString(alg
                    .ComputeHash(Encoding.UTF8.GetBytes(input)))
                    .Replace("-", "")
                    .ToLower();
            }
        }

        public static bool HasAnyToken(this string a, string b)
        {
            if (a.IsEmpty() || b.IsEmpty())
                return false;

            var delims = new char[] { ' ', ',', ';' };

            var A = a.ToLower()
                .Split(delims, StringSplitOptions.RemoveEmptyEntries)
            ;

            var B = b.ToLower()
                .Split(delims, StringSplitOptions.RemoveEmptyEntries)
            ;

            return A.Intersect(B).Any();
        }

        public static bool HasAllTokens(this string a, string b)
        {
            var delims = new char[] { ' ', ',', ';' };

            var A = a.ToLower()
                .Split(delims, StringSplitOptions.RemoveEmptyEntries)
            ;

            var B = b.ToLower()
                .Split(delims, StringSplitOptions.RemoveEmptyEntries)
            ;

            return A.Intersect(B).Count() == B.Length;
        }
    }
}
