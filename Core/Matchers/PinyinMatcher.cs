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
            var sentence = PinYinSentence.Generate(input);
            if (sentence.match(pattern))
            {
                result.Matched = true;
                result.Score = 3;
                //@TODO - matched pinyin should be highlighted, but this is not supported by the current version
                result.StringParts.Add(new StringPart(input));
            }

            return result;
        }
    }


    internal class PinYinSentence
    {
        static readonly Dictionary<string, List<string>> pinyinMap = new Dictionary<string, List<string>>();

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

        public static PinYinSentence Generate(string rawSentence)
        {
            PinYinSentence sentence = new PinYinSentence();
            sentence.rawSentence = rawSentence;
            sentence.pinyinSet = new HashSet<string>();
            List<StringBuilder> fullList = new List<StringBuilder>();
            List<StringBuilder> shortList = new List<StringBuilder>();
            bool hasPinYin = false;
            foreach (var item in rawSentence.ToCharArray())
            {
                string word = new string(item, 1);
                if (pinyinMap.ContainsKey(word))
                {
                    var pinyinList = pinyinMap[word];
                    if (hasPinYin)
                    {
                        if (pinyinList.Count == 1)
                        {
                            pinyinList.ForEach(pinyin =>
                            {
                                fullList.ForEach(builder => builder.Append(pinyin));
                                shortList.ForEach(builder => builder.Append(pinyin.Substring(0, 1)));
                            });
                        }
                        else if (pinyinList.Count > 1)
                        {
                            var fullCopy = generateCopyList(fullList.Select(builder => builder.ToString()).ToList());
                            var shortCopy = generateCopyList(shortList.Select(builder => builder.ToString()).ToList());
                            for (int i = 0; i < pinyinList.Count; i++)
                            {
                                var pinyin = pinyinList[i];
                                if (i == 0)
                                {
                                    fullList.ForEach(builder => builder.Append(pinyin));
                                    shortList.ForEach(builder => builder.Append(pinyin.Substring(0, 1)));
                                }
                                else
                                {
                                    fullList.AddRange(fullCopy().Select(builder => builder.Append(pinyin)).ToList());
                                    shortList.AddRange(shortCopy()
                                        .Select(builder => builder.Append(pinyin.Substring(0, 1))).ToList());
                                }
                            }
                        }
                    }
                    else
                    {
                        fullList.AddRange(pinyinList.Select(pinyin => new StringBuilder(pinyin)).ToList());
                        shortList.AddRange(pinyinList.Select(pinyin => new StringBuilder(pinyin.Substring(0, 1)))
                            .ToList());
                    }

                    hasPinYin = true;
                }
                else
                {
                    if (fullList.Count > 0)
                    {
                        for (int i = 0; i < fullList.Count; i++)
                        {
                            sentence.pinyinSet.Add(fullList[i].ToString());
                            sentence.pinyinSet.Add(shortList[i].ToString());
                        }
                    }

                    hasPinYin = false;
                }
            }

            if (fullList.Count > 0)
            {
                for (int i = 0; i < fullList.Count; i++)
                {
                    sentence.pinyinSet.Add(fullList[i].ToString());
                    sentence.pinyinSet.Add(shortList[i].ToString());
                }
            }

            return sentence;
        }
    }
}