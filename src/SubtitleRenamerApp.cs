using SharpCompress.Archives;
using SharpCompress.Common;
using System.IO;
using System.Text.RegularExpressions;

namespace SubRename;

public static class SubtitleRenamerApp
{
    public static void RunWithOptions(string topFolder, bool cleanup, bool confirmDeletes, Action<string> log, Func<string, string, bool>? confirmDelete = null)
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

        ExtractArchives(topFolder, Log);
        var movedVideoFiles = MoveVideos(topFolder, videoExtensions, logFilePath, Log);
        var allSubtitleFiles = FindSubtitles(topFolder, subtitleExtensions);
        MatchAndRenameSubtitles(movedVideoFiles, allSubtitleFiles, episodeRegex, topFolder, logFilePath, Log);
        DeleteSubfolders(topFolder, cleanup, confirmDeletes, confirmDelete, Log);
        DeleteUnrelatedFiles(topFolder, videoExtensions, subtitleExtensions, cleanup, confirmDeletes, confirmDelete, Log);

        Log($"\n{topFolder} is ready to be watched!\n");
    }

    private static void ExtractArchives(string topFolder, Action<string> Log)
    {
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
    }

    private static List<string> MoveVideos(string topFolder, string[] videoExtensions, string logFilePath, Action<string> Log)
    {
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
        return movedVideoFiles;
    }

    private static List<string> FindSubtitles(string topFolder, string[] subtitleExtensions)
    {
        return Directory.GetFiles(topFolder, "*.*", SearchOption.AllDirectories)
            .Where(f => subtitleExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    private static void MatchAndRenameSubtitles(List<string> movedVideoFiles, List<string> allSubtitleFiles,
        Regex episodeRegex, string topFolder, string logFilePath, Action<string> Log)
    {
        var usedSubtitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
                    var exact = candidates
                        .FirstOrDefault(c => string.Equals(Path.GetFileNameWithoutExtension(c), videoName, StringComparison.OrdinalIgnoreCase));
                    if (exact != null) chosen = exact;
                    else
                    {
                        var srtPref = candidates
                            .FirstOrDefault(c => Path.GetExtension(c).Equals(".srt", StringComparison.OrdinalIgnoreCase));
                        if (srtPref != null) chosen = srtPref;
                        else
                        {
                            chosen = candidates
                                .OrderBy(c => Helpers.LevenshteinDistance(Path.GetFileNameWithoutExtension(c), videoName))
                                .First();
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
    }

    private static void DeleteSubfolders(string topFolder, bool cleanup, bool confirmDeletes,
        Func<string, string, bool>? confirmDelete, Action<string> Log)
    {
        var subDirs = Directory.GetDirectories(topFolder);
        if (subDirs.Length == 0 || !cleanup)
            return;

        if (confirmDeletes && confirmDelete != null)
        {
            bool confirmed = true;
            string folderList = string.Join("\n", subDirs.Select(Path.GetFileName));
            confirmed = confirmDelete("folders", $"{subDirs.Length} folders?\n{folderList}");
            if (!confirmed)
            {
                foreach (var dir in subDirs)
                    Log($"Skipped deleting folder: {dir}");
                return;
            }
        }

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

    private static void DeleteUnrelatedFiles(string topFolder, string[] videoExtensions,
        string[] subtitleExtensions, bool cleanup, bool confirmDeletes,
        Func<string, string, bool>? confirmDelete, Action<string> Log)
    {
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
        if (!cleanup)
            return;

        var filesToDelete = Directory.GetFiles(topFolder)
            .Where(f => !allowedFiles.Contains(f))
            .ToList();
        if (filesToDelete.Count == 0)
            return;

        if (confirmDeletes && confirmDelete != null)
        {
            string fileList = string.Join("\n", filesToDelete.Select(Path.GetFileName));
            bool doDeleteFiles = confirmDelete("files", $"{filesToDelete.Count} files?\n{fileList}");
            if (!doDeleteFiles)
            {
                foreach (var file in filesToDelete)
                    Log($"Skipped deleting file: {file}");
                return;
            }
        }

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
}
