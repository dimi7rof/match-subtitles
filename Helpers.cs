using System;

public static class Helpers
{
    public static void WriteGreen(string text)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(text);
        Console.ForegroundColor = ConsoleColor.White;
    }

    public static void WriteYellow(string text)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(text);
        Console.ForegroundColor = ConsoleColor.White;
    }

    public static void WriteOrange(string text)
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow; // closest to orange
        Console.WriteLine(text);
        Console.ForegroundColor = ConsoleColor.White;
    }

    public static void WriteRed(string text)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(text);
        Console.ForegroundColor = ConsoleColor.White;
    }

    // Levenshtein distance for filename similarity
    public static int LevenshteinDistance(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return string.IsNullOrEmpty(b) ? 0 : b.Length;
        if (string.IsNullOrEmpty(b)) return a.Length;

        var la = a.Length;
        var lb = b.Length;
        var d = new int[la + 1, lb + 1];

        for (int i = 0; i <= la; i++) d[i, 0] = i;
        for (int j = 0; j <= lb; j++) d[0, j] = j;

        for (int i = 1; i <= la; i++)
        {
            for (int j = 1; j <= lb; j++)
            {
                int cost = (b[j - 1] == a[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }

        return d[la, lb];
    }
}
