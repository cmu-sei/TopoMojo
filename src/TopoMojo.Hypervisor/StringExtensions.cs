// Copyright 2021 Carnegie Mellon University.
// Released under a MIT (SEI) license. See LICENSE.md in the project root.

using System;
using System.Collections.Generic;
using System.Linq;

namespace TopoMojo.Hypervisor.Extensions
{
    public static class StringExtensions
    {
        public static bool HasValue(this string s)
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
            if (s.HasValue())
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
            if (s.HasValue())
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

        //Note: this assumes a guid string (length > 16)
        public static string ToSwitchName(this string s)
        {
            return string.Format("sw#{0}..{1}", s.Substring(0,8), s.Substring(s.Length-8));
        }

        public static string ToAbbreviatedHex(this string s)
        {
            return (s.Length > 8)
                ? string.Format("{0}..{1}", s.Substring(0, 4), s.Substring(s.Length - 4))
                : s;
        }

        public static string ToTenant(this string s)
        {
            char[] hex = new char[]{ '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f'};
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
