using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

public static class SubtitleRenamerApp
{
    public static void Run()
    {
        var videoExtensions = new[] { ".mkv", ".mp4", ".avi", ".mov" };
        var subtitleExtensions = new[] { ".srt", ".ass", ".vtt" };
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
                Helpers.WriteOrange("Invalid path. Try again.");
                continue;
            }

            var topFolder = inputPath;
            Console.WriteLine($"\nProcessing folder: {topFolder}");

            var logFilePath = Path.Combine(topFolder, "match-subtitles.log");

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
                        FileUtils.MoveWithBackup(video, destPath, logFilePath, "video");
                        movedVideoFiles.Add(destPath);
                    }
                    catch (Exception ex)
                    {
                        Helpers.WriteRed($"Failed to move video: {video} — {ex.Message}");
                        File.AppendAllText(logFilePath, $"[{DateTime.Now}] ERROR moving video: {video} -> {destPath} : {ex.Message}{Environment.NewLine}");
                    }
                }
                else
                {
                    movedVideoFiles.Add(destPath);
                }
            }

            // 2. Find all subtitle files (support multiple extensions)
            var allSubtitleFiles = Directory.GetFiles(topFolder, "*.*", SearchOption.AllDirectories)
                .Where(f => subtitleExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
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

                var candidates = allSubtitleFiles.Where(sub =>
                {
                    var subName = Path.GetFileNameWithoutExtension(sub);
                    var subMatch = episodeRegex.Match(subName);
                    return subMatch.Success &&
                           subMatch.Groups[1].Value == season &&
                           subMatch.Groups[2].Value == episode &&
                           !usedSubtitles.Contains(sub);
                }).ToList();

                if (candidates.Count > 0)
                {
                    // Better duplicate handling:
                    // 1) prefer subtitle with same base name as video
                    // 2) prefer .srt over other extensions
                    // 3) fallback to filename similarity (Levenshtein distance)
                    string? chosen = null;
                    if (candidates.Count == 1) chosen = candidates[0];
                    else
                    {
                        var exact = candidates.FirstOrDefault(c => Path.GetFileNameWithoutExtension(c).Equals(videoName, StringComparison.OrdinalIgnoreCase));
                        if (exact != null) chosen = exact;
                        else
                        {
                            var srtPref = candidates.FirstOrDefault(c => Path.GetExtension(c).Equals(".srt", StringComparison.OrdinalIgnoreCase));
                            if (srtPref != null) chosen = srtPref;
                            else
                            {
                                // pick by shortest Levenshtein distance to video name
                                chosen = candidates.OrderBy(c => Helpers.LevenshteinDistance(Path.GetFileNameWithoutExtension(c), videoName)).First();
                            }
                        }

                        File.AppendAllText(logFilePath, $"[{DateTime.Now}] Multiple subtitle candidates for {videoName}: {string.Join(", ", candidates)}. Chosen: {chosen}{Environment.NewLine}");
                    }

                    var expectedSubtitlePath = Path.Combine(
                        topFolder,
                        Path.GetFileNameWithoutExtension(videoFile) + Path.GetExtension(chosen)
                    );

                    usedSubtitles.Add(chosen);

                    if (Path.GetFullPath(chosen).Equals(Path.GetFullPath(expectedSubtitlePath), StringComparison.OrdinalIgnoreCase))
                    {
                        Helpers.WriteGreen($"Correct name: {Path.GetFileName(expectedSubtitlePath)}");
                        File.AppendAllText(logFilePath, $"[{DateTime.Now}] Correct subtitle: {expectedSubtitlePath}{Environment.NewLine}");
                    }
                    else
                    {
                        try
                        {
                            FileUtils.MoveWithBackup(chosen, expectedSubtitlePath, logFilePath, "subtitle");
                        }
                        catch (Exception ex)
                        {
                            Helpers.WriteRed($"Failed to move subtitle: {chosen} — {ex.Message}");
                            File.AppendAllText(logFilePath, $"[{DateTime.Now}] ERROR moving subtitle: {chosen} -> {expectedSubtitlePath} : {ex.Message}{Environment.NewLine}");
                        }
                    }
                }
            }

            // 4. Delete subfolders
            var subDirs = Directory.GetDirectories(topFolder);
            if (subDirs.Length > 0)
            {
                Console.Write($"Delete all subfolders under {topFolder}? (y/N): ");
                var resp = Console.ReadLine()?.Trim();
                bool doDeleteSubfolders = !string.IsNullOrEmpty(resp) && resp.Equals("y", StringComparison.OrdinalIgnoreCase);

                if (doDeleteSubfolders)
                {
                    foreach (var dir in subDirs)
                    {
                        try
                        {
                            Directory.Delete(dir, recursive: true);
                            Helpers.WriteOrange($"Deleted folder: {dir}");
                            File.AppendAllText(logFilePath, $"[{DateTime.Now}] Deleted folder: {dir}{Environment.NewLine}");
                        }
                        catch (Exception ex)
                        {
                            Helpers.WriteRed($"Failed to delete folder {dir} — {ex.Message}");
                            File.AppendAllText(logFilePath, $"[{DateTime.Now}] ERROR deleting folder: {dir} : {ex.Message}{Environment.NewLine}");
                        }
                    }
                }
            }

            // 5. Delete unrelated files in the top folder
            var finalVideoFiles = Directory.GetFiles(topFolder)
                .Where(f => videoExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var finalSubtitleFiles = finalVideoFiles
                .Select(v => Path.ChangeExtension(v, ".srt"))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Also include other subtitle extensions paired with video base names
            foreach (var v in finalVideoFiles)
            {
                foreach (var ext in subtitleExtensions)
                {
                    finalSubtitleFiles.Add(Path.ChangeExtension(v, ext));
                }
            }

            var allowedFiles = finalVideoFiles.Union(finalSubtitleFiles);
            Console.Write($"Delete unrelated files in {topFolder}? (y/N): ");
            var resp2 = Console.ReadLine()?.Trim();
            bool doDeleteFiles = !string.IsNullOrEmpty(resp2) && resp2.Equals("y", StringComparison.OrdinalIgnoreCase);

            if (doDeleteFiles)
            {
                foreach (var file in Directory.GetFiles(topFolder))
                {
                    if (!allowedFiles.Contains(file))
                    {
                        try
                        {
                            File.Delete(file);
                            Helpers.WriteOrange($"Deleted file: {file}");
                            File.AppendAllText(logFilePath, $"[{DateTime.Now}] Deleted file: {file}{Environment.NewLine}");
                        }
                        catch (Exception ex)
                        {
                            Helpers.WriteRed($"Failed to delete file: {file} — {ex.Message}");
                            File.AppendAllText(logFilePath, $"[{DateTime.Now}] ERROR deleting file: {file} : {ex.Message}{Environment.NewLine}");
                        }
                    }
                }
            }

            Helpers.WriteGreen($"\n{topFolder} is ready to be watched!\n");
        }
    }
}
