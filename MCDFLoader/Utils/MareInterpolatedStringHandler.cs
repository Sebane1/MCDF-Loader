using System.Runtime.CompilerServices;
using System.Text;

namespace McdfLoader.Utils;

[InterpolatedStringHandler]
public readonly ref struct McdfInterpolatedStringHandler
{
    readonly StringBuilder _logMessageStringbuilder;

    public McdfInterpolatedStringHandler(int literalLength, int formattedCount)
    {
        _logMessageStringbuilder = new StringBuilder(literalLength);
    }

    public void AppendLiteral(string s)
    {
        _logMessageStringbuilder.Append(s);
    }

    public void AppendFormatted<T>(T t)
    {
        _logMessageStringbuilder.Append(t?.ToString());
    }

    public string BuildMessage() => _logMessageStringbuilder.ToString();
}
