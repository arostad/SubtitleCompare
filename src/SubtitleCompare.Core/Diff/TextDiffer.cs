using System.Text.RegularExpressions;
using SubtitleCompare.Core.Models;
using SubtitleCompare.Core.Parsing;

namespace SubtitleCompare.Core.Diff;

/// <summary>
/// Word-level differ. Tokens are letters/digits or punctuation (whitespace is
/// kept in the output as Equal segments). Comparison is case-insensitive;
/// original display text is preserved. Leftover ASS tags are stripped first.
/// </summary>
public static class TextDiffer
{
    private static readonly Regex Tokenizer = new(
        @"\([^()\r\n]*\)|\s+|[\p{L}\p{N}]+|[^\s\p{L}\p{N}()]+|[()]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<IReadOnlyList<DiffSegment>> Compare(params string?[] texts)
    {
        ArgumentNullException.ThrowIfNull(texts);
        if (texts.Length < 2 || texts.Length > 3)
            throw new ArgumentException("Compare expects 2 or 3 strings.", nameof(texts));

        var prepared = texts.Select(t => AssTagStripper.Strip(t ?? "")).ToArray();
        var tokenLists = prepared.Select(Tokenize).ToArray();

        if (tokenLists.Length == 2)
            return CompareTwo(tokenLists[0], tokenLists[1]);
        return CompareThree(tokenLists[0], tokenLists[1], tokenLists[2]);
    }

    public static IReadOnlyList<IReadOnlyList<DiffSegment>> Compare(string a, string b) =>
        Compare(new string?[] { a, b });

    public static IReadOnlyList<IReadOnlyList<DiffSegment>> Compare(string a, string b, string c) =>
        Compare(new string?[] { a, b, c });

    private readonly record struct Token(string Display, string Key, bool IsWord);

    private static List<Token> Tokenize(string text)
    {
        var list = new List<Token>();
        foreach (Match m in Tokenizer.Matches(text))
        {
            var raw = m.Value;
            var isWs = char.IsWhiteSpace(raw[0]);
            var key = isWs ? "" : raw.ToLowerInvariant();
            list.Add(new Token(raw, key, !isWs));
        }
        return list;
    }

    private static IReadOnlyList<IReadOnlyList<DiffSegment>> CompareTwo(List<Token> a, List<Token> b)
    {
        var aWords = Indices(a);
        var bWords = Indices(b);
        var lcs = Lcs(a, aWords, b, bWords);
        var kindsA = new DiffKind[a.Count];
        var kindsB = new DiffKind[b.Count];
        Array.Fill(kindsA, DiffKind.Equal);
        Array.Fill(kindsB, DiffKind.Equal);

        var ai = 0;
        var bi = 0;
        var last = (aWords.Count, bWords.Count);
        for (var p = 0; p <= lcs.Count; p++)
        {
            var (ax, bx) = p < lcs.Count ? lcs[p] : last;
            ClassifyGap(a, kindsA, aWords, ai, ax - ai, b, kindsB, bWords, bi, bx - bi);
            if (ax < aWords.Count && bx < bWords.Count)
            {
                kindsA[aWords[ax]] = DiffKind.Equal;
                kindsB[bWords[bx]] = DiffKind.Equal;
            }
            ai = ax + 1;
            bi = bx + 1;
        }

        return new IReadOnlyList<DiffSegment>[]
        {
            Materialize(a, kindsA),
            Materialize(b, kindsB),
        };
    }

    private static IReadOnlyList<IReadOnlyList<DiffSegment>> CompareThree(List<Token> a, List<Token> b, List<Token> c)
    {
        var aWords = Indices(a);
        var bWords = Indices(b);
        var cWords = Indices(c);
        var lcs = Lcs3(a, aWords, b, bWords, c, cWords);

        var kindsA = new DiffKind[a.Count];
        var kindsB = new DiffKind[b.Count];
        var kindsC = new DiffKind[c.Count];
        Array.Fill(kindsA, DiffKind.Equal);
        Array.Fill(kindsB, DiffKind.Equal);
        Array.Fill(kindsC, DiffKind.Equal);

        var ai = 0;
        var bi = 0;
        var ci = 0;
        var last = (aWords.Count, bWords.Count, cWords.Count);
        for (var p = 0; p <= lcs.Count; p++)
        {
            var (ax, bx, cx) = p < lcs.Count ? lcs[p] : last;
            ClassifyGap3(
                a, kindsA, aWords, ai, ax - ai,
                b, kindsB, bWords, bi, bx - bi,
                c, kindsC, cWords, ci, cx - ci);
            if (ax < aWords.Count)
            {
                kindsA[aWords[ax]] = DiffKind.Equal;
                kindsB[bWords[bx]] = DiffKind.Equal;
                kindsC[cWords[cx]] = DiffKind.Equal;
            }
            ai = ax + 1;
            bi = bx + 1;
            ci = cx + 1;
        }

        return new IReadOnlyList<DiffSegment>[]
        {
            Materialize(a, kindsA),
            Materialize(b, kindsB),
            Materialize(c, kindsC),
        };
    }

    private static List<int> Indices(List<Token> tokens)
    {
        var list = new List<int>();
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].IsWord)
                list.Add(i);
        }
        return list;
    }

    private static void ClassifyGap(
        List<Token> a, DiffKind[] kindsA, List<int> aWords, int aFrom, int aCount,
        List<Token> b, DiffKind[] kindsB, List<int> bWords, int bFrom, int bCount)
    {
        var n = Math.Min(aCount, bCount);
        for (var i = 0; i < n; i++)
        {
            var same = a[aWords[aFrom + i]].Key == b[bWords[bFrom + i]].Key;
            var kind = same ? DiffKind.Equal : DiffKind.Changed;
            kindsA[aWords[aFrom + i]] = kind;
            kindsB[bWords[bFrom + i]] = kind;
        }
        for (var i = n; i < aCount; i++)
            kindsA[aWords[aFrom + i]] = DiffKind.Unique;
        for (var i = n; i < bCount; i++)
            kindsB[bWords[bFrom + i]] = DiffKind.Unique;
    }

    private static void ClassifyGap3(
        List<Token> a, DiffKind[] kindsA, List<int> aWords, int aFrom, int aCount,
        List<Token> b, DiffKind[] kindsB, List<int> bWords, int bFrom, int bCount,
        List<Token> c, DiffKind[] kindsC, List<int> cWords, int cFrom, int cCount)
    {
        var present = (aCount > 0 ? 1 : 0) + (bCount > 0 ? 1 : 0) + (cCount > 0 ? 1 : 0);
        if (present <= 1)
        {
            for (var i = 0; i < aCount; i++) kindsA[aWords[aFrom + i]] = DiffKind.Unique;
            for (var i = 0; i < bCount; i++) kindsB[bWords[bFrom + i]] = DiffKind.Unique;
            for (var i = 0; i < cCount; i++) kindsC[cWords[cFrom + i]] = DiffKind.Unique;
            return;
        }

        if (aCount > 0 && bCount > 0 && cCount == 0)
        {
            ClassifyGap(a, kindsA, aWords, aFrom, aCount, b, kindsB, bWords, bFrom, bCount);
            return;
        }
        if (aCount > 0 && cCount > 0 && bCount == 0)
        {
            ClassifyGap(a, kindsA, aWords, aFrom, aCount, c, kindsC, cWords, cFrom, cCount);
            return;
        }
        if (bCount > 0 && cCount > 0 && aCount == 0)
        {
            ClassifyGap(b, kindsB, bWords, bFrom, bCount, c, kindsC, cWords, cFrom, cCount);
            return;
        }

        var n = Math.Min(aCount, Math.Min(bCount, cCount));
        for (var i = 0; i < n; i++)
        {
            var ka = a[aWords[aFrom + i]].Key;
            var kb = b[bWords[bFrom + i]].Key;
            var kc = c[cWords[cFrom + i]].Key;
            var kind = (ka == kb && kb == kc) ? DiffKind.Equal : DiffKind.Changed;
            kindsA[aWords[aFrom + i]] = kind;
            kindsB[bWords[bFrom + i]] = kind;
            kindsC[cWords[cFrom + i]] = kind;
        }
        for (var i = n; i < aCount; i++) kindsA[aWords[aFrom + i]] = DiffKind.Unique;
        for (var i = n; i < bCount; i++) kindsB[bWords[bFrom + i]] = DiffKind.Unique;
        for (var i = n; i < cCount; i++) kindsC[cWords[cFrom + i]] = DiffKind.Unique;
    }

    private static List<(int A, int B)> Lcs(List<Token> a, List<int> aWords, List<Token> b, List<int> bWords)
    {
        var n = aWords.Count;
        var m = bWords.Count;
        var dp = new int[n + 1, m + 1];
        for (var i = n - 1; i >= 0; i--)
        {
            for (var j = m - 1; j >= 0; j--)
            {
                dp[i, j] = KeysEqual(a[aWords[i]], b[bWords[j]])
                    ? dp[i + 1, j + 1] + 1
                    : Math.Max(dp[i + 1, j], dp[i, j + 1]);
            }
        }

        var pairs = new List<(int, int)>();
        var x = 0;
        var y = 0;
        while (x < n && y < m)
        {
            if (KeysEqual(a[aWords[x]], b[bWords[y]]))
            {
                pairs.Add((x, y));
                x++;
                y++;
            }
            else if (dp[x + 1, y] >= dp[x, y + 1])
                x++;
            else
                y++;
        }
        return pairs;
    }

    private static List<(int A, int B, int C)> Lcs3(
        List<Token> a, List<int> aWords,
        List<Token> b, List<int> bWords,
        List<Token> c, List<int> cWords)
    {
        var n = aWords.Count;
        var m = bWords.Count;
        var p = cWords.Count;
        var dp = new int[n + 1, m + 1, p + 1];
        for (var i = n - 1; i >= 0; i--)
        {
            for (var j = m - 1; j >= 0; j--)
            {
                for (var k = p - 1; k >= 0; k--)
                {
                    if (KeysEqual(a[aWords[i]], b[bWords[j]]) && KeysEqual(a[aWords[i]], c[cWords[k]]))
                        dp[i, j, k] = dp[i + 1, j + 1, k + 1] + 1;
                    else
                        dp[i, j, k] = Math.Max(dp[i + 1, j, k], Math.Max(dp[i, j + 1, k], dp[i, j, k + 1]));
                }
            }
        }

        var triples = new List<(int, int, int)>();
        var x = 0;
        var y = 0;
        var z = 0;
        while (x < n && y < m && z < p)
        {
            if (KeysEqual(a[aWords[x]], b[bWords[y]]) && KeysEqual(a[aWords[x]], c[cWords[z]]))
            {
                triples.Add((x, y, z));
                x++;
                y++;
                z++;
            }
            else
            {
                var dA = dp[x + 1, y, z];
                var dB = dp[x, y + 1, z];
                var dC = dp[x, y, z + 1];
                if (dA >= dB && dA >= dC) x++;
                else if (dB >= dC) y++;
                else z++;
            }
        }
        return triples;
    }

    private static bool KeysEqual(Token x, Token y) =>
        string.Equals(x.Key, y.Key, StringComparison.Ordinal);

    private static string ConcatDisplay(List<Token> tokens, int start, int end)
    {
        var len = 0;
        for (var i = start; i < end; i++)
            len += tokens[i].Display.Length;
        if (len == 0)
            return "";
        if (start + 1 == end)
            return tokens[start].Display;

        return string.Create(len, (tokens, start, end), static (span, state) =>
        {
            var at = 0;
            for (var i = state.start; i < state.end; i++)
            {
                var s = state.tokens[i].Display;
                s.AsSpan().CopyTo(span[at..]);
                at += s.Length;
            }
        });
    }

    private static IReadOnlyList<DiffSegment> Materialize(List<Token> tokens, DiffKind[] kinds)
    {
        if (tokens.Count == 0)
            return Array.Empty<DiffSegment>();

        var list = new List<DiffSegment>();
        var i = 0;
        while (i < tokens.Count)
        {
            var kind = tokens[i].IsWord ? kinds[i] : DiffKind.Equal;
            var start = i;
            i++;
            while (i < tokens.Count)
            {
                var nextKind = tokens[i].IsWord ? kinds[i] : DiffKind.Equal;
                if (nextKind != kind)
                    break;
                i++;
            }

            var text = ConcatDisplay(tokens, start, i);
            if (text.Length > 0)
                list.Add(new DiffSegment(text, kind));
        }
        return list;
    }

    /// <summary>
    /// True when any selected pane differs (unique/changed token, or a missing counterpart).
    /// </summary>
    public static bool RowHasDifference(IReadOnlyList<IReadOnlyList<DiffSegment>> panes)
    {
        foreach (var pane in panes)
        {
            foreach (var seg in pane)
            {
                if (seg.Kind is DiffKind.Unique or DiffKind.Changed)
                    return true;
            }
        }
        return false;
    }

    public static bool TextsEqual(string? a, string? b)
    {
        var na = AssTagStripper.CollapseWhitespace(AssTagStripper.Strip(a ?? "")).ToLowerInvariant();
        var nb = AssTagStripper.CollapseWhitespace(AssTagStripper.Strip(b ?? "")).ToLowerInvariant();
        return na == nb;
    }
}
