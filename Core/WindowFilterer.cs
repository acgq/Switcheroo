/*
 * Switcheroo - The incremental-search task switcher for Windows.
 * http://www.switcheroo.io/
 * Copyright 2009, 2010 James Sulak
 * Copyright 2014 Regin Larsen
 * 
 * Switcheroo is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * Switcheroo is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with Switcheroo.  If not, see <http://www.gnu.org/licenses/>.
 */

using System.Collections.Generic;
using System.Linq;
using Switcheroo.Core.Matchers;

namespace Switcheroo.Core
{
    public class WindowFilterer
    {
        public IEnumerable<FilterResult<T>> Filter<T>(WindowFilterContext<T> context, string query) where T : IWindowText
        {
            var filterText = query;
            string processFilterText = null;

            var queryParts = query.Split(new [] {'.'}, 2);

            if (queryParts.Length == 2)
            {
                processFilterText = queryParts[0];
                if (processFilterText.Length == 0)
                {
                    processFilterText = context.ForegroundWindowProcessTitle;
                }

                filterText = queryParts[1];
            }

            return context.Windows
                .AsParallel()
                .Select(
                    w =>
                        new
                        {
                            Window = w,
                            ResultsTitle = Score(w.WindowTitle, filterText),
                            ResultsProcessTitle = Score(w.ProcessTitle, processFilterText ?? filterText)
                        })
                .Where(r =>
                {
                    if (processFilterText == null)
                    {
                        return r.ResultsTitle.Any(wt => wt.Matched) || r.ResultsProcessTitle.Any(pt => pt.Matched);
                    }
                    return r.ResultsTitle.Any(wt => wt.Matched) && r.ResultsProcessTitle.Any(pt => pt.Matched);
                })
                .OrderByDescending(r => r.ResultsTitle.Sum(wt => wt.Score) + r.ResultsProcessTitle.Sum(pt => pt.Score))
                .Select(
                    r =>
                        new FilterResult<T>
                        {
                            AppWindow = r.Window,
                            WindowTitleMatchResults = r.ResultsTitle,
                            ProcessTitleMatchResults = r.ResultsProcessTitle
                        });
        }

        private static List<MatchResult> Score(string title, string filterText)
        {
            List<IMatcher> matchers = new List<IMatcher>();
            matchers.Add(new StartsWithMatcher());
            matchers.Add(new ContainsMatcher());
            matchers.Add(new SignificantCharactersMatcher());
            matchers.Add(new IndividualCharactersMatcher());
            matchers.Add(new PinyinMatcher());
            var results = matchers.AsParallel()
                .Select(matcher => matcher.Evaluate(title,filterText))
                .ToList();


            return results;
        }
    }

    public class WindowFilterContext<T> where T : IWindowText
    {
        public string ForegroundWindowProcessTitle { get; set; }
        public IEnumerable<T> Windows { get; set; } 
    }
}