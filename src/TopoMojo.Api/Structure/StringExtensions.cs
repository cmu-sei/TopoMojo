// Copyright 2025 Carnegie Mellon University.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root.

using System.Security.Cryptography;
using System.Text;

namespace TopoMojo.Api.Extensions
{
    public static class StringExtensions
    {
        public static bool IsEmpty(this string s)
        {
            return string.IsNullOrEmpty(s);
        }

        public static bool NotEmpty(this string s)
        {
            return !string.IsNullOrWhiteSpace(s);
        }

        //check for presence of array values
        public static bool IsEmpty(this object[] o)
        {
            return o == null || o.Length == 0;
        }

        public static bool IsNotEmpty(this object[] o)
        {
            return o != null && o.Length > 0;
        }

        //expands a range string (i.e. [1-3,5,7,10-12]) into an int list
        public static int[] ExpandRange(this string s)
        {
            s = s.Inner();

            List<int> list = [];
            string[] sections = s.Split(',');
            foreach (string section in sections)
            {
                string[] token = section.Split('-');
                if (int.TryParse(token[0], out int x))
                {
                    int y;
                    if (token.Length > 1)
                    {
                        if (!int.TryParse(token[1], out y))
                            y = x;
                    }
                    else
                    {
                        y = x;
                    }
                    for (int i = x; i <= y; i++)
                    {
                        list.Add(i);
                    }
                }
            }
            return [.. list];
        }

        //extracts string from brackets [].
        public static string Inner(this string s)
        {
            s ??= "";

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
                int x = s.IndexOf('#');
                if (x >= 0)
                    return s[(x + 1)..].Split(' ').First();
            }
            return "";
        }

        //strips hashtag+ from string
        public static string Untagged(this string s)
        {
            if (s.NotEmpty())
            {
                int x = s.IndexOf('#');
                if (x >= 0)
                    return s[..x];
            }
            return s;
        }

        public static string ExtractBefore(this string s, string target)
        {
            int x = s.IndexOf(target);
            return (x>-1)
                ? s[..x]
                : s;
        }
        public static string ExtractAfter(this string s, string target)
        {
            int x = s.IndexOf(target);
            return (x>-1)
                ? s[(x + 1)..]
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
            return string.Format("sw#{0}..{1}", s[..8], s[^8..]);
        }

        public static string ToAbbreviatedHex(this string s)
        {
            return (s.Length > 8)
                ? String.Format("{0}..{1}", s[..4], s[^4..])
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
                result = result[..^1];

            return result.ToLower();
        }

        public static int ToSeconds(this string ts)
        {
            if (ts.ValidSimpleTimespan().Equals(false))
                return 0;

            int value = int.Parse(ts[..^1]);
            int factor = ts[^1..].First() switch
            {
                'y' => 86400 * 365,
                'w' => 86400 * 7,
                'd' => 86400,
                'h' => 3600,
                'm' => 60,
                _ => 1
            };

            return value * factor;
        }

        public static bool ValidSimpleTimespan(this string ts)
        {
            if (string.IsNullOrEmpty(ts))
                return false;
            if (int.TryParse(ts[..^1], out _))
            {
                string type = ts[^1..];
                return "ywdms".Contains(type);
            }

            return false;
        }

        public static DateTimeOffset ToDatePast(this string ts)
        {
            return DateTimeOffset.UtcNow
                .Subtract(
                    new TimeSpan( 0, 0, ts.ToSeconds())
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
            return BitConverter.ToString(
                SHA256.HashData(Encoding.UTF8.GetBytes(input))
            ).Replace("-", "").ToLower();
        }

        public static bool HasAnyToken(this string a, string b)
        {
            if (a.IsEmpty() || b.IsEmpty())
                return false;

            var A = a.ToLower()
                .Split(token_separators, StringSplitOptions.RemoveEmptyEntries)
            ;

            var B = b.ToLower()
                .Split(token_separators, StringSplitOptions.RemoveEmptyEntries)
            ;

            return A.Intersect(B).Any();
        }

        public static bool HasAllTokens(this string a, string b)
        {
            var A = a.ToLower()
                .Split(token_separators, StringSplitOptions.RemoveEmptyEntries)
            ;

            var B = b.ToLower()
                .Split(token_separators, StringSplitOptions.RemoveEmptyEntries)
            ;

            return A.Intersect(B).Count() == B.Length;
        }

        public static string ToRandomIPv4(this string s)
        {
            string result = "";
            try
            {
                var net = s.Split("/");
                var addr_string = (net.First() + ".0.0.0.0").Split(".");

                uint addr =
                    uint.Parse(addr_string[0]) << 24 |
                    uint.Parse(addr_string[1]) << 16 |
                    uint.Parse(addr_string[2]) << 8 |
                    uint.Parse(addr_string[3])
                ;

                uint mask = 0xFFFFFFFF << (32 - int.Parse(net.Last()));
                if (~mask > 0)
                {
                    uint host = (uint) new Random().Next(1,(int)~mask);
                    addr |= host;
                }

                result = $"{addr>>24&0xff}.{addr>>16&0xff}.{addr>>8&0xff}.{addr&0xff}";
            }
            catch {}

            return result;
        }

        private static readonly char[] token_separators = [' ', ',', ';'];
        private static readonly char[] hex_chars = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f'];

        public static string ExtractTenant(this string s)
        {
            string r = "";
            char[] c = s.ToLower().ToCharArray();
            for (int i = 0; i < c.Length; i++)
            {
                if (hex_chars.Contains(c[i]))
                    break;
                else
                    r += c[i];
            }
            return r;
        }
    }
}
