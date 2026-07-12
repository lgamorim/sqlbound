using System.Globalization;
using System.Text;

namespace SqlBound.Generators;

/// <summary>
/// Reads a committed <c>.sqlbound/</c> JSON snapshot into a <see cref="QuerySnapshot"/>.
/// The parser is hand-rolled rather than a JSON library dependency: analyzer dependencies must
/// ship bundled beside the assembly and are a known source of version conflicts inside the IDE,
/// while this schema is tiny and produced by SqlBound's own <c>prepare</c> step. Unknown fields
/// are ignored for forward compatibility; any malformation returns <c>false</c>, never throws.
/// </summary>
internal static class QuerySnapshotReader
{
    public static bool TryRead(string text, out QuerySnapshot? snapshot)
    {
        snapshot = null;
        object? root;
        try
        {
            root = new JsonParser(text).ParseDocument();
        }
        catch (FormatException)
        {
            return false;
        }

        if (root is not Dictionary<string, object?> document
            || !TryGetString(document, "commandText", out var commandText)
            || !TryGetString(document, "provider", out var provider)
            || !TryGetArray(document, "columns", out var columnItems)
            || !TryGetArray(document, "parameters", out var parameterItems))
        {
            return false;
        }

        var columns = new SnapshotColumn[columnItems.Count];
        for (var i = 0; i < columnItems.Count; i++)
        {
            if (columnItems[i] is not Dictionary<string, object?> column
                || !TryGetOrdinal(column, out var ordinal)
                || !TryGetString(column, "name", out var name)
                || !TryGetString(column, "sqlTypeName", out var sqlTypeName)
                || !TryGetString(column, "clrTypeText", out var clrTypeText)
                || !TryGetBool(column, "isNullable", out var isNullable))
            {
                return false;
            }

            columns[i] = new SnapshotColumn(ordinal, name, sqlTypeName, clrTypeText, isNullable);
        }

        var parameters = new SnapshotParameter[parameterItems.Count];
        for (var i = 0; i < parameterItems.Count; i++)
        {
            if (parameterItems[i] is not Dictionary<string, object?> parameter
                || !TryGetString(parameter, "name", out var name)
                || !TryGetString(parameter, "sqlTypeName", out var sqlTypeName)
                || !TryGetNullableString(parameter, "clrTypeText", out var clrTypeText))
            {
                return false;
            }

            parameters[i] = new SnapshotParameter(name, sqlTypeName, clrTypeText);
        }

        snapshot = new QuerySnapshot(
            commandText,
            provider,
            new EquatableArray<SnapshotColumn>(columns),
            new EquatableArray<SnapshotParameter>(parameters));
        return true;
    }

    private static bool TryGetString(Dictionary<string, object?> map, string key, out string result)
    {
        if (map.TryGetValue(key, out var value) && value is string text)
        {
            result = text;
            return true;
        }

        result = string.Empty;
        return false;
    }

    private static bool TryGetNullableString(Dictionary<string, object?> map, string key, out string? result)
    {
        if (!map.TryGetValue(key, out var value))
        {
            result = null;
            return false;
        }

        if (value is null)
        {
            result = null;
            return true;
        }

        if (value is string text)
        {
            result = text;
            return true;
        }

        result = null;
        return false;
    }

    private static bool TryGetBool(Dictionary<string, object?> map, string key, out bool result)
    {
        if (map.TryGetValue(key, out var value) && value is bool flag)
        {
            result = flag;
            return true;
        }

        result = false;
        return false;
    }

    private static bool TryGetOrdinal(Dictionary<string, object?> map, out int result)
    {
        if (map.TryGetValue("ordinal", out var value)
            && value is double number
            && number >= 0
            && number <= int.MaxValue
            && number == Math.Floor(number))
        {
            result = (int)number;
            return true;
        }

        result = 0;
        return false;
    }

    private static bool TryGetArray(Dictionary<string, object?> map, string key, out List<object?> result)
    {
        if (map.TryGetValue(key, out var value) && value is List<object?> list)
        {
            result = list;
            return true;
        }

        result = [];
        return false;
    }

    private sealed class JsonParser(string text)
    {
        private readonly string _text = text;
        private int _position;

        public object? ParseDocument()
        {
            var value = ParseValue();
            SkipWhitespace();
            if (_position != _text.Length)
            {
                throw new FormatException("Trailing content after the JSON document.");
            }

            return value;
        }

        private object? ParseValue()
        {
            SkipWhitespace();
            return Peek() switch
            {
                '{' => ParseObject(),
                '[' => ParseArray(),
                '"' => ParseString(),
                't' or 'f' => ParseKeyword(),
                'n' => ParseKeyword(),
                _ => ParseNumber(),
            };
        }

        private Dictionary<string, object?> ParseObject()
        {
            Expect('{');
            var result = new Dictionary<string, object?>();
            SkipWhitespace();
            if (Peek() == '}')
            {
                _position++;
                return result;
            }

            while (true)
            {
                SkipWhitespace();
                var key = ParseString();
                SkipWhitespace();
                Expect(':');
                result[key] = ParseValue();
                SkipWhitespace();
                var next = Read();
                if (next == '}')
                {
                    return result;
                }

                if (next != ',')
                {
                    throw new FormatException("Expected ',' or '}' in an object.");
                }
            }
        }

        private List<object?> ParseArray()
        {
            Expect('[');
            var result = new List<object?>();
            SkipWhitespace();
            if (Peek() == ']')
            {
                _position++;
                return result;
            }

            while (true)
            {
                result.Add(ParseValue());
                SkipWhitespace();
                var next = Read();
                if (next == ']')
                {
                    return result;
                }

                if (next != ',')
                {
                    throw new FormatException("Expected ',' or ']' in an array.");
                }
            }
        }

        private string ParseString()
        {
            Expect('"');
            var builder = new StringBuilder();
            while (true)
            {
                var current = Read();
                if (current == '"')
                {
                    return builder.ToString();
                }

                if (current == '\\')
                {
                    var escape = Read();
                    builder.Append(escape switch
                    {
                        '"' => '"',
                        '\\' => '\\',
                        '/' => '/',
                        'b' => '\b',
                        'f' => '\f',
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        'u' => ParseUnicodeEscape(),
                        _ => throw new FormatException($"Unsupported escape '\\{escape}'."),
                    });
                }
                else if (current < ' ')
                {
                    throw new FormatException("Unescaped control character in a string.");
                }
                else
                {
                    builder.Append(current);
                }
            }
        }

        private char ParseUnicodeEscape()
        {
            if (_position + 4 > _text.Length)
            {
                throw new FormatException("Truncated \\u escape.");
            }

            var hex = _text.Substring(_position, 4);
            _position += 4;
            if (!ushort.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
            {
                throw new FormatException($"Invalid \\u escape '{hex}'.");
            }

            return (char)code;
        }

        private object? ParseKeyword()
        {
            foreach (var (keyword, value) in new (string, object?)[] { ("true", true), ("false", false), ("null", null) })
            {
                if (_position + keyword.Length <= _text.Length
                    && string.CompareOrdinal(_text, _position, keyword, 0, keyword.Length) == 0)
                {
                    _position += keyword.Length;
                    return value;
                }
            }

            throw new FormatException("Unrecognized JSON keyword.");
        }

        private object ParseNumber()
        {
            var start = _position;
            while (_position < _text.Length && "+-.eE0123456789".IndexOf(_text[_position]) >= 0)
            {
                _position++;
            }

            var token = _text.Substring(start, _position - start);
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                throw new FormatException($"Invalid JSON number '{token}'.");
            }

            return number;
        }

        private void SkipWhitespace()
        {
            while (_position < _text.Length && _text[_position] is ' ' or '\t' or '\r' or '\n')
            {
                _position++;
            }
        }

        private char Peek() =>
            _position < _text.Length ? _text[_position] : throw new FormatException("Unexpected end of JSON.");

        private char Read() =>
            _position < _text.Length ? _text[_position++] : throw new FormatException("Unexpected end of JSON.");

        private void Expect(char expected)
        {
            if (Read() != expected)
            {
                throw new FormatException($"Expected '{expected}'.");
            }
        }
    }
}
