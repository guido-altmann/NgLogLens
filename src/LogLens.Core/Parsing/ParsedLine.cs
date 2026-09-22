using System.Diagnostics.CodeAnalysis;
using LogLens.Core.Models;

namespace LogLens.Core.Parsing;

public enum LogLineKind
{
    Unknown,
    Access,
    AccessTruncated,
    Error,
    ErrorTruncated,
}

/// <summary>Ergebnis eines einzelnen Zeilenparsers.</summary>
public abstract record ParsedLine
{
    public abstract LogLineKind Kind { get; }
}

public sealed record ParsedAccessLine(AccessEntry Entry) : ParsedLine
{
    public override LogLineKind Kind =>
        Entry.IsTruncated ? LogLineKind.AccessTruncated : LogLineKind.Access;
}

public sealed record ParsedErrorLine(ErrorEntry Entry) : ParsedLine
{
    public override LogLineKind Kind =>
        Entry.IsTruncated ? LogLineKind.ErrorTruncated : LogLineKind.Error;
}

/// <summary>
/// Ein Zeilenformat. Weitere Formate (Apache, IIS …) lassen sich ergänzen, ohne die
/// Pipeline zu ändern (SPEC 11).
/// </summary>
public interface ILogLineParser
{
    bool TryParse(string line, int lineNumber, [NotNullWhen(true)] out ParsedLine? parsed);
}
