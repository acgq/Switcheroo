using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Caching;
using System.Text;

namespace Switcheroo.Core.Matchers
{
    public class PinyinMatcher : IMatcher
    {
        public MatchResult Evaluate(string input, string pattern)
        {
            var result = new MatchResult();
            if (input == null)
            {
                return result;
            }

            if (pattern == null)
            {
                result.StringParts.Add(new StringPart(input));
                return result;
            }

            var tuple = PinYinSentence.match(input, pattern);
            if (tuple.Item1)
            {
                result.Matched = true;
                result.Score = 3;
                var indexTuple = tuple.Item2;
                result.StringParts.Add(new StringPart(input.Substring(0, indexTuple.Item1)));
                result.StringParts.Add(
                    new StringPart(input.Substring(indexTuple.Item1, indexTuple.Item2 - indexTuple.Item1 + 1), true));
                if (indexTuple.Item2 < input.Length - 1)
                {
                    result.StringParts.Add(new StringPart(input.Substring(indexTuple.Item2 + 1)));
                }
            }

            return result;
        }
    }


    internal class PinYinSentence
    {
        public static readonly Dictionary<string, List<string>> pinyinMap = new Dictionary<string, List<string>>();

        private static MemoryCache _cache = MemoryCache.Default;

        private static CacheItemPolicy policy = new CacheItemPolicy();

        static readonly Func<List<String>, Func<List<StringBuilder>>> generateCopyList = (generateCopyList) =>
        {
            return () => generateCopyList.Select(item => new StringBuilder(item)).ToList();
        };

        static PinYinSentence()
        {
            policy.AbsoluteExpiration = DateTimeOffset.Now.AddHours(5);
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                @"kTGHZ2013.txt");
            if (File.Exists(path))
            {
                string[] lines = File.ReadAllLines(path);

                foreach (var line in lines)
                {
                    if (line.StartsWith("#"))
                    {
                        continue;
                    }

                    var strings = line.Split('\t');
                    if (strings.Length == 2)
                    {
                        pinyinMap.Add(strings[0], strings[1].Split(' ').ToList());
                    }
                }
            }
        }

        private static Tuple<bool, Tuple<int, int>> matchFull(Tuple<string, List<int>> tuple, string pattern)
        {
            var fullPinyin = tuple.Item1;
            var indexes = tuple.Item2;
            for (var i = 0; i < indexes.Count; i++)
            {
                var startPos = fullPinyin.IndexOf(pattern, indexes[i]);
                if (startPos == -1)
                {
                    return new Tuple<bool, Tuple<int, int>>(false, null);
                }

                if (indexes.Contains(startPos))
                {
                    var start = indexes.IndexOf(startPos);
                    var end = 0;
                    for (var i1 = 0; i1 < indexes.Count; i1++)
                    {
                        if (indexes[i1] > startPos + pattern.Length - 1)
                        {
                            break;
                        }
                        else
                        {
                            end = i1;
                        }
                    }

                    return new Tuple<bool, Tuple<int, int>>(true, new Tuple<int, int>(start, end));
                }
            }

            return new Tuple<bool, Tuple<int, int>>(false, null);
        }


        public static Tuple<bool, Tuple<int, int>> match(string input, String pattern)
        {
            var prefixPinYinSentence = GetPrefixPinYinSentence(input);
            var first = prefixPinYinSentence.FirstOrDefault(tuple => tuple.Item1.Contains(pattern));
            if (first != null)
            {
                var prefixString = first.Item1;
                var start = prefixString.IndexOf(pattern);
                var end = start + pattern.Length - 1;
                return new Tuple<bool, Tuple<int, int>>(true, new Tuple<int, int>(start, end));
            }

            var pinYinSentence = GetFullPinYinSentence(input);
            var fullMatch = pinYinSentence.Select(tuple => matchFull(tuple, pattern))
                .ToList()
                .Find(tuple => tuple.Item1);
            if (fullMatch != null)
            {
                return fullMatch;
            }

            return new Tuple<bool, Tuple<int, int>>(false, null);
        }

        private static HashSet<Tuple<string, List<int>>> GetPrefixPinYinSentence(string input)
        {
            var results = new HashSet<Tuple<string, List<int>>>() {Tuple.Create("", new List<int>() {0})};
            foreach (var c in input)
            {
                var key = c.ToString();
                var pinyinMap = PinYinSentence.pinyinMap;
                if (pinyinMap.ContainsKey(key))
                {
                    var pinyinList = pinyinMap[key];
                    results = (from result in results
                            from pinyin in pinyinList
                            select Tuple.Create(result.Item1 + pinyin.First(),
                                new List<int>(result.Item2) {result.Item2.Last() + pinyin.Length})
                        ).ToHashSet();
                }
            }

            return results;
        }

        private static HashSet<Tuple<string, List<int>>> GetFullPinYinSentence(string input)
        {
            if (_cache.Contains(input))
            {
                return (HashSet<Tuple<string, List<int>>>) _cache.Get(input);
            }

            var results = new HashSet<Tuple<string, List<int>>>() {Tuple.Create("", new List<int>() {0})};
            foreach (var c in input)
            {
                var key = c.ToString();
                var pinyinMap = PinYinSentence.pinyinMap;
                if (pinyinMap.ContainsKey(key))
                {
                    var pinyinList = pinyinMap[key];
                    results = (from result in results
                            from pinyin in pinyinList
                            select Tuple.Create(result.Item1 + pinyin,
                                new List<int>(result.Item2) {result.Item2.Last() + pinyin.Length})
                        ).ToHashSet();
                }
            }

            _cache.Set(input, results, policy);
            return results;
        }
    }
}