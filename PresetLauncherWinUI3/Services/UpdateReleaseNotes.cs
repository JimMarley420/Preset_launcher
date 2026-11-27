using System.Text.RegularExpressions;

namespace PresetLauncher.Services;

internal enum ReleaseNoteKind { Paragraph, Heading, ListItem, Code }
internal enum ReleaseNoteStyle { Plain, Bold, Code }
internal sealed record ReleaseNoteSpan(string Text, ReleaseNoteStyle Style = ReleaseNoteStyle.Plain);
internal sealed record ReleaseNoteBlock(ReleaseNoteKind Kind, IReadOnlyList<ReleaseNoteSpan> Spans);

internal static partial class UpdateReleaseNotes
{
    private const string Begin = "<!-- presetlauncher-app-notes:v1:begin -->";
    private const string End = "<!-- presetlauncher-app-notes:v1:end -->";

    [GeneratedRegex(@"presetlauncher-app-notes\s*:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Envelope();

    [GeneratedRegex(@"<!--[\s\S]*?(?:-->|\z)|<(?:details|script|style|template)\b[^>]*>[\s\S]*?(?:</(?:details|script|style|template)\s*>|\z)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex HiddenMarkup();

    [GeneratedRegex(@"</?[A-Za-z][^<>\r\n]*>|presetlauncher\s*:[^\r\n]*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InternalMarkup();

    [GeneratedRegex(@"\\(?<literal>[\\`*_[\]#])|!?\[(?<label>[^\]\r\n]+)\]\([^)\r\n]*\)|\*\*(?<bold>[^*\r\n]+)\*\*|`(?<code>[^`\r\n]+)`",
        RegexOptions.NonBacktracking)]
    private static partial Regex Inline();

    internal static string? Extract(string body)
    {
        if (body.Length > 256_000 || Envelope().Matches(body).Count != 2)
            return null;

        var lines = body.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var start = Array.IndexOf(lines, Begin);
        var end = Array.IndexOf(lines, End);
        if (start < 0 || end <= start + 1 || end - start > 750)
            return null;
        var notes = string.Join('\n', lines, start + 1, end - start - 1);
        if (notes.Length > 48_000)
            return null;
        notes = InternalMarkup().Replace(HiddenMarkup().Replace(notes, ""), "").Trim();
        return notes.Length == 0 || notes.Contains("-->", StringComparison.Ordinal) ? null : notes;
    }

    internal static IReadOnlyList<ReleaseNoteBlock> Format(string markdown)
    {
        List<ReleaseNoteBlock> blocks = [];
        List<string> paragraph = [];
        List<string>? code = null;
        var inlineBudget = 800;
        foreach (var raw in markdown.Split('\n'))
        {
            if (inlineBudget < 0) return [];
            if (raw.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                FlushParagraph();
                if (code is null) code = [];
                else
                {
                    if (code.Count > 0) Add(ReleaseNoteKind.Code, string.Join('\n', code));
                    code = null;
                }
                continue;
            }
            if (code is not null)
            {
                code.Add(raw);
                continue;
            }
            var line = raw.Trim();
            if (line.Length == 0)
            {
                FlushParagraph();
                continue;
            }
            var heading = line.Length - line.TrimStart('#').Length;
            if (heading is > 0 and <= 6 && heading < line.Length && line[heading] == ' ')
            {
                FlushParagraph();
                Add(ReleaseNoteKind.Heading, line[(heading + 1)..].TrimStart());
            }
            else if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal) ||
                     line.StartsWith("+ ", StringComparison.Ordinal))
            {
                FlushParagraph();
                Add(ReleaseNoteKind.ListItem, "• " + line[2..]);
            }
            else if (line.IndexOf(". ", StringComparison.Ordinal) is > 0 and <= 3 and var marker &&
                     line.AsSpan(0, marker).IndexOfAnyExceptInRange('0', '9') < 0)
            {
                FlushParagraph();
                Add(ReleaseNoteKind.ListItem, line);
            }
            else paragraph.Add(line);
        }
        if (code is { Count: > 0 }) Add(ReleaseNoteKind.Code, string.Join('\n', code));
        FlushParagraph();
        return inlineBudget < 0 ? [] : blocks;

        void FlushParagraph()
        {
            if (paragraph.Count == 0) return;
            Add(ReleaseNoteKind.Paragraph, string.Join(' ', paragraph));
            paragraph.Clear();
        }

        void Add(ReleaseNoteKind kind, string value) =>
            blocks.Add(new(kind, kind == ReleaseNoteKind.Code ? [new(value, ReleaseNoteStyle.Code)] : FormatInline(value, ref inlineBudget)));
    }

    private static List<ReleaseNoteSpan> FormatInline(string text, ref int inlineBudget)
    {
        List<ReleaseNoteSpan> spans = [];
        var offset = 0;
        foreach (Match match in Inline().Matches(text))
        {
            if (--inlineBudget < 0) return [];
            if (match.Index > offset) spans.Add(new(text[offset..match.Index]));
            if (match.Groups["bold"] is { Success: true } bold) spans.Add(new(bold.Value, ReleaseNoteStyle.Bold));
            else if (match.Groups["code"] is { Success: true } code) spans.Add(new(code.Value, ReleaseNoteStyle.Code));
            else spans.Add(new(match.Groups["label"].Success ? match.Groups["label"].Value : match.Groups["literal"].Value));
            offset = match.Index + match.Length;
        }
        if (offset < text.Length) spans.Add(new(text[offset..]));
        return spans;
    }
}
