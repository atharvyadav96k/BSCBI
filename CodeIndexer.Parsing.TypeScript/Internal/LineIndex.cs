namespace CodeIndexer.Parsing.TypeScript.Internal;

/// <summary>Maps a character offset to a 1-based line number, built once per file.</summary>
internal sealed class LineIndex
{
    private readonly int[] _lineStartOffsets;

    public LineIndex(string text)
    {
        var starts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                starts.Add(i + 1);
            }
        }

        _lineStartOffsets = starts.ToArray();
    }

    public int LineOf(int charOffset)
    {
        var index = Array.BinarySearch(_lineStartOffsets, charOffset);
        if (index < 0)
        {
            index = ~index - 1;
        }

        return index + 1;
    }
}
