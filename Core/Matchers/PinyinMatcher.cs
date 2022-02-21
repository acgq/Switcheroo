using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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

            var match = PinYinMatch.match(input, pattern);
            if (match != null)
            {
                result.Matched = true;
                result.Score = 3;
                var index = input.IndexOf(match.result);
                result.StringParts.Add(new StringPart(input.Substring(0, index)));
                result.StringParts.Add(new StringPart(match.result, true));
                if (index + match.result.Length < input.Length)
                {
                    result.StringParts.Add(new StringPart(input.Substring(index + match.result.Length)));
                }
            }

            return result;
        }
    }


    internal class PinYinSentence
    {
        public static readonly Dictionary<string, List<string>> pinyinMap = new Dictionary<string, List<string>>();

        static readonly Func<List<String>, Func<List<StringBuilder>>> generateCopyList = (generateCopyList) =>
        {
            return () => generateCopyList.Select(item => new StringBuilder(item)).ToList();
        };

        static PinYinSentence()
        {
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


        String rawSentence;

        HashSet<String> pinyinSet = new HashSet<string>();

        public bool match(string sentence)
        {
            return pinyinSet.Any(pinyin => pinyin.Contains(sentence));
        }
    }

    internal class PinYinMatch
    {
        private string value;
        private List<string> pinyin;
        private PinYinMatch child { get; set; }

        public static PinYinMatchResult match(string input, string pattern)
        {
            var inputMatch = generate(input);
            return inputMatch.Select(charMatch => charMatch.matchPattern(pattern, 0))
                .ToList()
                .Find(result => result.match);
        }

        private PinYinMatchResult matchPattern(string pattern, int index)
        {
            if (pinyin != null)
            {
                if (index >= pattern.Length)
                {
                    return new PinYinMatchResult(true, 0, "");
                }

                var searchItem = pattern.Substring(index);
                foreach (var item in pinyin)
                {
                    if (item.StartsWith(searchItem))
                    {
                        return new PinYinMatchResult(true, 1, value);
                    }

                    if (searchItem.StartsWith(item) && child != null)
                    {
                        var match = child.matchPattern(pattern, item.Length + index);

                        if (match.match)
                        {
                            match.count = match.count + 1;
                            match.result = value + match.result;
                            return match;
                        }
                    }
                }
            }

            if (child == null)
            {
                return new PinYinMatchResult(false, 0, "");
            }

            return child.matchPattern(pattern, 0);
        }


        private static List<PinYinMatch> generate(string input)
        {
            var result = new List<PinYinMatch>();
            if (input.Length == 0)
            {
                return result;
            }

            var chars = input.ToCharArray();

            PinYinMatch fullResult = full(new string(chars[0], 1));
            PinYinMatch firstResult = first(new string(chars[0], 1));
            PinYinMatch fullTemp = fullResult;
            PinYinMatch firstTemp = firstResult;
            for (int i = 1; i < chars.Length; i++)
            {
                string word = new string(chars[i], 1);
                var full = PinYinMatch.full(word);
                var first = PinYinMatch.first(word);
                fullTemp.child = full;
                firstTemp.child = first;
                fullTemp = full;
                firstTemp = first;
            }

            result.Add(fullResult);
            result.Add(firstResult);
            return result;
        }

        static PinYinMatch full(string value)
        {
            var result = new PinYinMatch();
            result.value = value;
            result.pinyin = (PinYinSentence.pinyinMap.ContainsKey(value)
                ? PinYinSentence.pinyinMap[value]
                : new List<string>());
            return result;
        }

        static PinYinMatch first(string value)
        {
            var result = new PinYinMatch();
            result.value = value;
            result.pinyin = (PinYinSentence.pinyinMap.ContainsKey(value)
                    ? PinYinSentence.pinyinMap[value]
                    : new List<string>())
                .Select(s => s.Substring(0, 1))
                .ToList();
            return result;
        }
    }

    internal class PinYinMatchResult
    {
        public PinYinMatchResult(bool match, int count, string result)
        {
            this.match = match;
            this.count = count;
            this.result = result;
        }

        public bool match { get; set; }
        public int count { get; set; }
        public string result { get; set; }
    }
}