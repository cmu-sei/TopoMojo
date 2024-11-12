// Copyright 2025 Carnegie Mellon University.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root.

using System;
using System.Collections.Generic;
using System.Linq;

namespace TopoMojo.Hypervisor.Extensions
{
    public static class StringExtensions
    {
        public static bool HasValue(this string s)
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
                    int y = x;
                    if (token.Length > 1 && int.TryParse(token[1], out int yy))
                        y = yy;

                    for (int i = x; i <= y; i++)
                        list.Add(i);
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
            if (s.HasValue())
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
            if (s.HasValue())
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
            return (x >= 0)
                ? s[..x]
                : s;
        }
        public static string ExtractAfter(this string s, string target)
        {
            int x = s.IndexOf(target);
            return (x >= 0)
                ? s[(x + 1)..]
                : s;
        }

        //Note: this assumes a guid string (length > 16)
        public static string ToSwitchName(this string s)
        {
            return string.Format("sw#{0}..{1}", s[..8], s[^8..]);
        }

        public static string ToAbbreviatedHex(this string s)
        {
            return (s.Length > 8)
                ? string.Format("{0}..{1}", s[..4], s[^4..])
                : s;
        }

        public static string ToTenant(this string s)
        {
            char[] hex = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f'];
            string r = "";
            char[] c = s.Tag().ToLower().ToCharArray();
            for (int i = 0; i < c.Length; i++)
            {
                if (hex.Contains(c[i]))
                    break;
                else
                    r += c[i];
            }
            return r;
        }
    }
}
