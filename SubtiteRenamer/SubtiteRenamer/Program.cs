using System.Text.RegularExpressions;

var videoExtensions = new[] { ".mkv", ".mp4", ".avi", ".mov" };
var subtitleExtension = ".srt";
var episodeRegex = new Regex(@"S(\d{2})[ ._-]*E(\d{2})", RegexOptions.IgnoreCase);

Console.ForegroundColor = ConsoleColor.White;

while (true)
{
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine("\nEnter folder path (or type 'exit' to quit):");
    var inputPath = Console.ReadLine()?.Trim('"');

    if (string.IsNullOrWhiteSpace(inputPath)) continue;
    if (inputPath.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;
    if (!Directory.Exists(inputPath))
    {
        WriteOrange("Invalid path. Try again.");
        continue;
    }

    var topFolder = inputPath;
    Console.WriteLine($"\nProcessing folder: {topFolder}");

    // 1. Move video files from subfolders to top folder
    var allVideoFiles = Directory.GetFiles(topFolder, "*.*", SearchOption.AllDirectories)
        .Where(f => videoExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
        .ToList();

    var movedVideoFiles = new List<string>();
    foreach (var video in allVideoFiles)
    {
        var destPath = Path.Combine(topFolder, Path.GetFileName(video));
        if (!video.Equals(destPath, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                File.Move(video, destPath, overwrite: true);
                WriteYellow($"Moved video: {Path.GetFileName(video)} -> {Path.GetFileName(destPath)}");
                movedVideoFiles.Add(destPath);
            }
            catch (Exception ex)
            {
                WriteRed($"Failed to move video: {video} — {ex.Message}");
            }
        }
        else
        {
            movedVideoFiles.Add(destPath);
        }
    }

    // 2. Find all subtitle files
    var allSubtitleFiles = Directory.GetFiles(topFolder, $"*{subtitleExtension}", SearchOption.AllDirectories)
        .ToList();

    var usedSubtitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    // 3. Match, check or rename subtitles
    foreach (var videoFile in movedVideoFiles)
    {
        var videoName = Path.GetFileNameWithoutExtension(videoFile);
        var match = episodeRegex.Match(videoName);
        if (!match.Success) continue;

        var season = match.Groups[1].Value;
        var episode = match.Groups[2].Value;

        var subtitleMatch = allSubtitleFiles.FirstOrDefault(sub =>
        {
            var subName = Path.GetFileNameWithoutExtension(sub);
            var subMatch = episodeRegex.Match(subName);
            return subMatch.Success &&
                   subMatch.Groups[1].Value == season &&
                   subMatch.Groups[2].Value == episode &&
                   !usedSubtitles.Contains(sub);
        });

        if (subtitleMatch != null)
        {
            var expectedSubtitlePath = Path.Combine(
                topFolder,
                Path.GetFileNameWithoutExtension(videoFile) + subtitleExtension
            );

            usedSubtitles.Add(subtitleMatch);

            if (Path.GetFullPath(subtitleMatch).Equals(Path.GetFullPath(expectedSubtitlePath), StringComparison.OrdinalIgnoreCase))
            {
                WriteGreen($"Correct name: {Path.GetFileName(expectedSubtitlePath)}");
            }
            else
            {
                try
                {
                    File.Move(subtitleMatch, expectedSubtitlePath, overwrite: true);
                    WriteYellow($"Renamed subtitle: {Path.GetFileName(subtitleMatch)} -> {Path.GetFileName(expectedSubtitlePath)}");
                }
                catch (Exception ex)
                {
                    WriteRed($"Failed to move subtitle: {subtitleMatch} — {ex.Message}");
                }
            }
        }
    }

    // 4. Delete subfolders
    foreach (var dir in Directory.GetDirectories(topFolder))
    {
        try
        {
            Directory.Delete(dir, recursive: true);
            WriteOrange($"Deleted folder: {dir}");
        }
        catch (Exception ex)
        {
            WriteRed($"Failed to delete folder {dir} — {ex.Message}");
        }
    }

    // 5. Delete unrelated files in the top folder
    var finalVideoFiles = Directory.GetFiles(topFolder)
        .Where(f => videoExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    var finalSubtitleFiles = finalVideoFiles
        .Select(v => Path.ChangeExtension(v, subtitleExtension))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    var allowedFiles = finalVideoFiles.Union(finalSubtitleFiles);

    foreach (var file in Directory.GetFiles(topFolder))
    {
        if (!allowedFiles.Contains(file))
        {
            try
            {
                File.Delete(file);
                WriteOrange($"Deleted file: {file}");
            }
            catch (Exception ex)
            {
                WriteRed($"Failed to delete file: {file} — {ex.Message}");
            }
        }
    }

    WriteGreen($"\n{topFolder} is ready to be watched!\n");
}

// === Helper Methods ===

void WriteGreen(string text)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine(text);
    Console.ForegroundColor = ConsoleColor.White;
}

void WriteYellow(string text)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine(text);
    Console.ForegroundColor = ConsoleColor.White;
}

void WriteOrange(string text)
{
    Console.ForegroundColor = ConsoleColor.DarkYellow; // closest to orange
    Console.WriteLine(text);
    Console.ForegroundColor = ConsoleColor.White;
}

void WriteRed(string text)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine(text);
    Console.ForegroundColor = ConsoleColor.White;
}
