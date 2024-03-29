﻿#region copyright
// /*
//     ShipItSharp Deployment Coordinator. Provides extra tooling to help
//     deploy software through Octopus Deploy.
// 
//     Copyright (C) 2022  Steven Davies
// 
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
// 
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
// 
//     You should have received a copy of the GNU General Public License
//     along with this program.  If not, see <http://www.gnu.org/licenses/>.
// */
#endregion


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;
using ShipItSharp.Core.Language;

namespace ShipItSharp.Console.ConsoleTools
{
    internal class InteractiveRunner
    {
        private readonly List<string> _columns;
        private readonly ILanguageProvider _languageProvider;
        private readonly string _promptText;
        private readonly List<string[]> _rows;
        private readonly List<int> _selected;
        private readonly List<int> _unselectable;
        private readonly string _unselectableText;

        internal InteractiveRunner(string promptText, string unselectableText, ILanguageProvider languageProvider, params string[] columns)
        {
            _columns = new List<string>(columns);
            _rows = new List<string[]>();
            _selected = new List<int>();
            _unselectable = new List<int>();
            _promptText = promptText;
            _unselectableText = unselectableText;
            _languageProvider = languageProvider;
        }

        public void AddRow(bool selected, bool selectable = true, params string[] values)
        {
            if (values.Count() != _columns.Count())
            {
                throw new Exception(string.Format(_languageProvider.GetString(LanguageSection.UiStrings, "ErrorColumnHeadingMismatch"), values.Count(), _columns.Count()));
            }
            _rows.Add(values);
            if (selected && selectable)
            {
                _selected.Add(_rows.Count() - 1);
            }
            if (!selectable)
            {
                _unselectable.Add(_rows.Count() - 1);
            }
        }

        public void Run()
        {
            var run = true;
            while (run)
            {
                var newColumns = _columns.ToList();
                newColumns.Insert(0, "#");
                newColumns.Insert(1, "*");
                var table = new ConsoleTable(newColumns.ToArray());

                var rowPosition = 1;

                foreach (var row in _rows)
                {
                    var newRow = row.ToList();
                    newRow.Insert(0, rowPosition.ToString());
                    newRow.Insert(1, _selected.Contains(rowPosition - 1) ? "*" : string.Empty);
                    table.AddRow(newRow.ToArray());
                    rowPosition++;
                }

                System.Console.WriteLine(Environment.NewLine + _promptText + Environment.NewLine);
                table.Write(Format.Minimal);

                System.Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "InteractiveRunnerInstructions"));
                var prompt = Prompt.GetString(">");

                switch (prompt)
                {
                    case "1":
                        SelectProjectsForDeployment(true);
                        break;
                    case "2":
                        SelectProjectsForDeployment(false);
                        break;
                    case "c":
                        run = false;
                        break;
                    case "e":
                        System.Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "Exiting"));
                        Environment.Exit(0);
                        break;
                    default:
                        System.Console.WriteLine(Environment.NewLine);
                        break;
                }
            }
        }

        public IEnumerable<int> GetSelectedIndexes()
        {
            return _selected.ToList();
        }

        private void SelectProjectsForDeployment(bool select)
        {
            var range = GetRangeFromPrompt(_rows.Count());
            var unselectable = new List<int>();

            foreach (var index in range)
            {
                if (select)
                {
                    if (_unselectable.Contains(index - 1))
                    {
                        unselectable.Add(index);
                        continue;
                    }
                    if (!_selected.Contains(index - 1))
                    {
                        _selected.Add(index - 1);
                    }
                }
                else
                {
                    if (_selected.Contains(index - 1))
                    {
                        _selected.Remove(index - 1);
                    }
                }
            }

            if (unselectable.Any())
            {
                var projectList = string.Join(", ", unselectable.Select(index => _rows[index][0]));
                WriteError(_unselectableText + $" {projectList}");
            }
        }

        private void WriteError(string text)
        {
            System.Console.WriteLine(_unselectable);
            System.Console.ForegroundColor = ConsoleColor.Red;
            System.Console.WriteLine(text);
            System.Console.ResetColor();
            Thread.Sleep(3000);
        }

        private IEnumerable<int> GetRangeFromPrompt(int max)
        {
            var rangeValid = false;
            var intRange = new List<int>();
            while (!rangeValid)
            {
                intRange.Clear();
                var userInput = Prompt.GetString(_languageProvider.GetString(LanguageSection.UiStrings, "InteractiveRunnerSelectionInstructions"));
                if (string.IsNullOrEmpty(userInput))
                {
                    return new List<int>();
                }
                if (!userInput.All(c => c >= 0 || c <= 9 || c == '-'))
                {
                    continue;
                }
                var segments = userInput.Split(",");
                foreach (var segment in segments)
                {
                    var match = Regex.Match(segment, "([0-9]+)-([0-9]+)");
                    if (match.Success)
                    {
                        var start = Convert.ToInt32(match.Groups[1].Value);
                        var end = Convert.ToInt32(match.Groups[2].Value);
                        if (start > end || end > max)
                        {
                            continue;
                        }
                        intRange.AddRange(Enumerable.Range(start, (end - start) + 1).ToList());
                    }
                    else
                    {
                        if (!int.TryParse(segment, out var number))
                        {
                            continue;
                        }
                        if (number > max || number < 1)
                        {
                            continue;
                        }
                        intRange.Add(number);
                    }
                }
                rangeValid = true;
            }
            return intRange.Distinct().OrderBy(i => i);
        }
    }

}