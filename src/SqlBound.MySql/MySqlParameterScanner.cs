namespace SqlBound.MySql;

/// <summary>
/// Finds <c>@name</c> parameter placeholders in a MySQL command text. Unlike the other three
/// providers, MySQL's prepared-statement protocol has no server-side way to discover parameter
/// names — <c>MySqlQueryDescriber</c> must pre-declare them before it can even describe a
/// statement's columns — so this is a hand-rolled scan rather than a server round-trip. It skips
/// single- and double-quoted string literals (both doubled-quote and backslash escaping),
/// backtick-quoted identifiers, and <c>--</c>/<c>#</c>/<c>/* */</c> comments, so a literal like
/// <c>'user@example.com'</c> is never mistaken for a placeholder.
/// </summary>
internal static class MySqlParameterScanner
{
    public static IReadOnlyList<string> ExtractNames(string commandText)
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var position = 0;
        while (position < commandText.Length)
        {
            var current = commandText[position];
            switch (current)
            {
                case '\'' or '"' or '`':
                    position = SkipQuoted(commandText, position, current);
                    break;
                case '-' when Peek(commandText, position + 1) == '-':
                case '#':
                    position = SkipLineComment(commandText, position);
                    break;
                case '/' when Peek(commandText, position + 1) == '*':
                    position = SkipBlockComment(commandText, position);
                    break;
                case '@' when IsIdentifierStart(Peek(commandText, position + 1)):
                    position = ReadParameterName(commandText, position, names, seen);
                    break;
                default:
                    position++;
                    break;
            }
        }

        return names;
    }

    private static int ReadParameterName(string text, int position, List<string> names, HashSet<string> seen)
    {
        var start = position + 1;
        var end = start;
        while (end < text.Length && IsIdentifierPart(text[end]))
        {
            end++;
        }

        var name = text.Substring(start, end - start);
        if (seen.Add(name))
        {
            names.Add(name);
        }

        return end;
    }

    private static int SkipQuoted(string text, int position, char quote)
    {
        position++;
        while (position < text.Length)
        {
            var current = text[position];
            if (current == '\\' && quote != '`')
            {
                position += 2;
                continue;
            }

            if (current == quote)
            {
                if (Peek(text, position + 1) == quote)
                {
                    position += 2;
                    continue;
                }

                return position + 1;
            }

            position++;
        }

        return position;
    }

    private static int SkipLineComment(string text, int position)
    {
        var newline = text.IndexOf('\n', position);
        return newline < 0 ? text.Length : newline + 1;
    }

    private static int SkipBlockComment(string text, int position)
    {
        var close = text.IndexOf("*/", position + 2, StringComparison.Ordinal);
        return close < 0 ? text.Length : close + 2;
    }

    private static char Peek(string text, int position) => position < text.Length ? text[position] : '\0';

    private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

    private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_';
}
