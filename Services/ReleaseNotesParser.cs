// Portions inspired by Filee (MIT License - Copyright (c) KnifeLemon).
// Adapted for KanbanCards release notes parsing and bullet point extraction.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace KanbanCards.Services;

/// <summary>
/// Extracts clean, readable summary bullet points from GitHub release Markdown bodies.
/// </summary>
public static class ReleaseNotesParser
{
    private const string StartTag = "<!-- update";
    private const string EndTag = "-->";

    /// <summary>
    /// Parses the raw markdown body of a GitHub release and returns a clean list of bullet points.
    /// </summary>
    public static List<string> ExtractSummary(string? markdownBody, int maxItems = 4)
    {
        if (string.IsNullOrWhiteSpace(markdownBody))
            return new List<string> { "Performance improvements and bug fixes." };

        // 1. Check for Filee-style structured metadata comment block:
        // <!-- update
        // en: Summary line 1
        // en: Summary line 2
        // -->
        var startIndex = markdownBody.IndexOf(StartTag, StringComparison.OrdinalIgnoreCase);
        if (startIndex >= 0)
        {
            var endIndex = markdownBody.IndexOf(EndTag, startIndex, StringComparison.OrdinalIgnoreCase);
            if (endIndex > startIndex)
            {
                var block = markdownBody.Substring(startIndex + StartTag.Length, endIndex - (startIndex + StartTag.Length));
                var structuredLines = new List<string>();
                foreach (var line in block.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var colon = line.IndexOf(':');
                    if (colon > 0)
                    {
                        var key = line.Substring(0, colon).Trim().ToLowerInvariant();
                        var val = line.Substring(colon + 1).Trim();
                        if ((key == "en" || key == "summary" || key == "note" || key == "desc") && !string.IsNullOrWhiteSpace(val))
                        {
                            structuredLines.Add(val);
                        }
                    }
                }
                if (structuredLines.Count > 0)
                    return structuredLines.Take(maxItems).ToList();
            }
        }

        // 2. Parse general GitHub markdown release notes
        var lines = markdownBody.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>();

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Skip markdown headers
            if (line.StartsWith("#"))
                continue;

            // Skip horizontal rules
            if (line == "---" || line == "***" || line == "___")
                continue;

            // Skip full changelog and release metadata URLs
            if (line.Contains("Full Changelog", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("What's Changed", StringComparison.OrdinalIgnoreCase))
                continue;

            bool isList = line.StartsWith("* ") || line.StartsWith("- ") || line.StartsWith("+ ") || line.StartsWith("• ");
            string cleaned = line;
            if (isList)
            {
                cleaned = line.Substring(2).Trim();
            }
            else if (char.IsDigit(line[0]) && line.Length > 2 && (line[1] == '.' || (line.Length > 3 && line[2] == '.')))
            {
                var dot = line.IndexOf('.');
                cleaned = line.Substring(dot + 1).Trim();
            }
            else
            {
                // Normal paragraph sentence - skip if too short or likely a label
                if (line.Length < 12 || line.EndsWith(":"))
                    continue;
            }

            // Clean markdown syntax:
            // Remove bold/italics markers
            cleaned = cleaned.Replace("**", "").Replace("__", "").Replace("`", "");
            // Convert markdown links [Text](url) -> Text
            cleaned = Regex.Replace(cleaned, @"\[([^\]]+)\]\([^\)]+\)", "$1");
            // Remove PR links like "in https://github.com/..."
            cleaned = Regex.Replace(cleaned, @"\s+in\s+https?://\S+", "");
            // Remove PR numbers like "(#123)"
            cleaned = Regex.Replace(cleaned, @"\s*\(\s*#\d+\s*\)", "");
            // Remove user attribution like "by @user"
            cleaned = Regex.Replace(cleaned, @"\s+by\s+@[\w-]+", "");

            cleaned = cleaned.Trim();
            if (!string.IsNullOrWhiteSpace(cleaned) && cleaned.Length > 4)
            {
                result.Add(cleaned);
                if (result.Count >= maxItems)
                    break;
            }
        }

        if (result.Count == 0)
        {
            result.Add("Performance improvements and stability enhancements.");
        }

        return result;
    }
}
