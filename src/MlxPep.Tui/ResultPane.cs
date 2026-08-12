using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MlxPep.Tui
{
    /// <summary>
    /// Displays command results and output in a scrollable text area.
    /// This component is a container for command output that can be updated
    /// after each command invocation.
    /// </summary>
    internal class ResultPane
    {
        private readonly List<string> _lines = new();
        private const int MaxLines = 1000;  // Limit buffer to prevent memory issues

        /// <summary>
        /// Gets all lines currently in the result pane.
        /// </summary>
        public IReadOnlyList<string> Lines => _lines.AsReadOnly();

        /// <summary>
        /// Gets the total number of lines in the result pane.
        /// </summary>
        public int LineCount => _lines.Count;

        /// <summary>
        /// Adds a line of output to the result pane.
        /// </summary>
        public void AddLine(string line)
        {
            Debug.WriteLine($"ResultPane.AddLine: {line}");
            
            _lines.Add(line);
            
            // Trim older lines if buffer exceeds max
            if (_lines.Count > MaxLines)
            {
                _lines.RemoveRange(0, _lines.Count - MaxLines);
            }
        }

        /// <summary>
        /// Adds multiple lines of output to the result pane.
        /// </summary>
        public void AddLines(IEnumerable<string> lines)
        {
            foreach (var line in lines)
            {
                AddLine(line);
            }
        }

        /// <summary>
        /// Displays the output from a command invocation.
        /// </summary>
        public void DisplayCommandResult(CommandResult result, string output, string errorOutput = "")
        {
            Debug.WriteLine($"ResultPane.DisplayCommandResult: success={result.IsSuccess}");
            
            AddLine("");
            AddLine("═══════════════════════════════════════════════════════════");
            AddLine($"Result: {(result.IsSuccess ? "✓ SUCCESS" : "✗ FAILURE")}");
            AddLine($"Message: {result.Message}");
            AddLine("═══════════════════════════════════════════════════════════");
            
            if (!string.IsNullOrWhiteSpace(output))
            {
                AddLine("");
                AddLine("Output:");
                AddLine("───────────────────────────────────────────────────────────");
                AddLines(output.Split(new[] { Environment.NewLine }, StringSplitOptions.None));
            }
            
            if (!string.IsNullOrWhiteSpace(errorOutput))
            {
                AddLine("");
                AddLine("Errors:");
                AddLine("───────────────────────────────────────────────────────────");
                AddLines(errorOutput.Split(new[] { Environment.NewLine }, StringSplitOptions.None));
            }
        }

        /// <summary>
        /// Clears all content from the result pane.
        /// </summary>
        public void Clear()
        {
            Debug.WriteLine("ResultPane.Clear");
            _lines.Clear();
        }

        /// <summary>
        /// Gets the last N lines from the result pane.
        /// Useful for terminal display with limited height.
        /// </summary>
        public IEnumerable<string> GetLastLines(int count)
        {
            return _lines.TakeLast(count);
        }

        /// <summary>
        /// Gets lines starting from a specific index.
        /// Useful for scrolling through output.
        /// </summary>
        public IEnumerable<string> GetLinesFrom(int startIndex)
        {
            if (startIndex < 0 || startIndex >= _lines.Count)
                return Enumerable.Empty<string>();
            
            return _lines.Skip(startIndex);
        }

        /// <summary>
        /// Formats the result pane content as a multi-line string.
        /// </summary>
        public override string ToString()
        {
            return string.Join(Environment.NewLine, _lines);
        }
    }
}
