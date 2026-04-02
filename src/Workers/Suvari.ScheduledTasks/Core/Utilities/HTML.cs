using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Suvari.ScheduledTasks.Core.Utilities;

public static class HTML
{
    /// <summary>
    /// Remove HTML from string with Regex.
    /// </summary>
    public static string StripTagsRegex(string source)
    {
        return Regex.Replace(source, "<.*?>", string.Empty);
    }

    /// <summary>
    /// Compiled regular expression for performance.
    /// </summary>
    static readonly Regex _htmlRegex = new Regex("<.*?>", RegexOptions.Compiled);

    /// <summary>
    /// Remove HTML from string with compiled Regex.
    /// </summary>
    public static string StripTagsRegexCompiled(string source)
    {
        return _htmlRegex.Replace(source, string.Empty);
    }

    /// <summary>
    /// Remove HTML tags from string using char array.
    /// </summary>
    public static string StripTagsCharArray(string source)
    {
        char[] array = new char[source.Length];
        int arrayIndex = 0;
        bool inside = false;

        foreach (char @let in source)
        {
            if (@let == '<')
            {
                inside = true;
                continue;
            }
            if (@let == '>')
            {
                inside = false;
                continue;
            }
            if (inside) continue;
            array[arrayIndex] = @let;
            arrayIndex++;
        }
        return new string(array, 0, arrayIndex);
    }

    public static string HtmlToPlainText(string html)
    {
        const string tagWhiteSpace = @"(>|$)(\W|\n|\r)+<";//matches one or more (white space or line breaks) between '>' and '<'
        const string stripFormatting = @"<[^>]*(>|$)";//match any character between '<' and '>', even when end tag is missing
        const string lineBreak = @"<(br|BR)\s{0,1}\/{0,1}>";//matches: <br>,<br/>,<br />,<BR>,<BR/>,<BR />
        var lineBreakRegex = new Regex(lineBreak, RegexOptions.Multiline);
        var stripFormattingRegex = new Regex(stripFormatting, RegexOptions.Multiline);
        var tagWhiteSpaceRegex = new Regex(tagWhiteSpace, RegexOptions.Multiline);

        var text = html;
        //Decode html specific characters
        text = System.Net.WebUtility.HtmlDecode(text);
        //Remove tag whitespace/line breaks
        text = tagWhiteSpaceRegex.Replace(text, "><");
        //Replace <br /> with line breaks
        text = lineBreakRegex.Replace(text, Environment.NewLine);
        //Strip formatting
        text = stripFormattingRegex.Replace(text, string.Empty);

        return text;
    }
}