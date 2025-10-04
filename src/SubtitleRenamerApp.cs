using SharpCompress.Archives;
using SharpCompress.Common;
using System.IO;
using System.Text.RegularExpressions;

namespace SubRename;

public static class SubtitleRenamerApp
{
    public static void RunWithOptions(string topFolder, bool deleteSubfolders, bool deleteUnrelated, bool confirmDeletes, Action<string> log, Func<string, string, bool>? confirmDelete = null)
    {
        var videoExtensions = new[] { ".mkv", ".mp4", ".avi", ".mov" };
        var subtitleExtensions = new[] { ".srt", ".ass", ".vtt" };
        var episodeRegex = new Regex(@"S(\d{2})[ ._-]*E(\d{2})", RegexOptions.IgnoreCase);

        var logFilePath = Path.Combine(topFolder, "match-subtitles.log");
        void Log(string msg)
        {
            log?.Invoke(msg);
            File.AppendAllText(logFilePath, $"[{DateTime.Now}] {msg}{Environment.NewLine}");
        }

        // 0. Extract .zip and .rar files first
        var archiveFiles = Directory.GetFiles(topFolder, "*.zip", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(topFolder, "*.rar", SearchOption.TopDirectoryOnly))
            .ToList();
        foreach (var archivePath in archiveFiles)
        {
            try
            {
                using var archive = ArchiveFactory.Open(archivePath);
                foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                {
                    entry.WriteToDirectory(topFolder, new ExtractionOptions { ExtractFullPath = true, Overwrite = true });
                }
                Log($"Extracted archive: {archivePath}");
            }
            catch (Exception ex)
            {
                Log($"Failed to extract archive: {archivePath} — {ex.Message}");
            }
        }

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
                    Log($"Moved video: {video} -> {destPath}");
                }
                catch (Exception ex)
                {
                    Log($"Failed to move video: {video} — {ex.Message}");
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
                            chosen = candidates.OrderBy(c => Helpers.LevenshteinDistance(Path.GetFileNameWithoutExtension(c), videoName)).First();
                        }
                    }
                    Log($"Multiple subtitle candidates for {videoName}: {string.Join(", ", candidates)}. Chosen: {chosen}");
                }

                var expectedSubtitlePath = Path.Combine(
                    topFolder,
                    Path.GetFileNameWithoutExtension(videoFile) + Path.GetExtension(chosen)
                );

                usedSubtitles.Add(chosen);

                if (Path.GetFullPath(chosen).Equals(Path.GetFullPath(expectedSubtitlePath), StringComparison.OrdinalIgnoreCase))
                {
                    Log($"Correct name: {Path.GetFileName(expectedSubtitlePath)}");
                }
                else
                {
                    try
                    {
                        FileUtils.MoveWithBackup(chosen, expectedSubtitlePath, logFilePath, "subtitle");
                        Log($"Renamed subtitle: {chosen} -> {expectedSubtitlePath}");
                    }
                    catch (Exception ex)
                    {
                        Log($"Failed to move subtitle: {chosen} — {ex.Message}");
                    }
                }
            }
        }

        // 4. Delete subfolders
        var subDirs = Directory.GetDirectories(topFolder);
        if (subDirs.Length > 0 && deleteSubfolders)
        {
            bool doDeleteSubfolders = true;
            if (confirmDeletes && confirmDelete != null)
            {
                foreach (var dir in subDirs)
                {
                    if (confirmDelete("folder", dir))
                    {
                        try
                        {
                            Directory.Delete(dir, recursive: true);
                            Log($"Deleted folder: {dir}");
                        }
                        catch (Exception ex)
                        {
                            Log($"Failed to delete folder {dir} — {ex.Message}");
                        }
                    }
                    else
                    {
                        Log($"Skipped deleting folder: {dir}");
                    }
                }
            }
            else if (doDeleteSubfolders)
            {
                foreach (var dir in subDirs)
                {
                    try
                    {
                        Directory.Delete(dir, recursive: true);
                        Log($"Deleted folder: {dir}");
                    }
                    catch (Exception ex)
                    {
                        Log($"Failed to delete folder {dir} — {ex.Message}");
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

        foreach (var v in finalVideoFiles)
        {
            foreach (var ext in subtitleExtensions)
            {
                finalSubtitleFiles.Add(Path.ChangeExtension(v, ext));
            }
        }

        var allowedFiles = finalVideoFiles.Union(finalSubtitleFiles);
        if (deleteUnrelated)
        {
            var filesToDelete = Directory.GetFiles(topFolder)
                .Where(f => !allowedFiles.Contains(f))
                .ToList();
            bool doDeleteFiles = true;
            if (confirmDeletes && confirmDelete != null && filesToDelete.Count > 0)
            {
                string fileList = string.Join("\n", filesToDelete.Select(Path.GetFileName));
                doDeleteFiles = confirmDelete("files", $"{filesToDelete.Count} files:\n{fileList}");
            }
            if (doDeleteFiles)
            {
                foreach (var file in filesToDelete)
                {
                    try
                    {
                        File.Delete(file);
                        Log($"Deleted file: {file}");
                    }
                    catch (Exception ex)
                    {
                        Log($"Failed to delete file: {file} — {ex.Message}");
                    }
                }
            }
            else
            {
                foreach (var file in filesToDelete)
                {
                    Log($"Skipped deleting file: {file}");
                }
            }
        }

        Log($"\n{topFolder} is ready to be watched!\n");
    }
}
