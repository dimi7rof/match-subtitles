# match-subtitles (SubtiteRenamer)

Small console utility to prepare TV episode folders for watching by matching and renaming .srt subtitle files to corresponding video files and cleaning up folders.

## What it does

- Moves video files from subfolders into the specified top folder.
- Searches for subtitle files (`.srt`) and tries to match them to videos using an episode pattern (SxxExx).
- Renames matching subtitle files so they have the exact same name as the video (only extension differs).
- Deletes all subfolders under the top folder after processing.
- Removes unrelated files from the top folder (keeps videos and their `.srt` pairs).

## Supported file types

- Video extensions: `.mkv`, `.mp4`, `.avi`, `.mov` (can be modified in `Program.cs`).
- Subtitle extension: `.srt`.

## Matching logic

The app extracts season and episode from filenames using this regex (case-insensitive):

```
S(\d{2})[ ._-]*E(\d{2})
```

Examples matched: `S01E05`, `S01.E05`, `S01_E05`, `S01 - E05`, etc.

If a subtitle contains the same season/episode numbers, it is considered a match and will be moved/renamed to match the video filename.

## Caveats / Warnings

- The program will move and delete files. Make a backup or test on a disposable folder first.
- Only `.srt` subtitles are handled by default.
- If multiple subtitle files match a single video, the app picks the first unused one it finds.
- Files are overwritten if names collide (uses File.Move with overwrite behavior in .NET 6+). Adjust code if you need a different behavior.

## Build & Run

Requires .NET 7+ / .NET SDK compatible with the project target (project currently targets .NET 9 in the repo build outputs).

From PowerShell in the repo root:

- Build:
  dotnet build -c Release

- Run directly with the SDK (interactive):
  dotnet run --project .\SubtiteRenamer.csproj

- Or run the published executable after publishing for your platform, for example (replace runtime identifier as needed):
  dotnet publish -c Release -r win-x64 --self-contained false -o .\publish
  .\publish\SubtiteRenamer.exe

When running the app it prompts:

- Enter folder path (or type 'exit' to quit):

Provide the full path to the folder containing your show (it will scan subfolders).

## Where to change behavior

- `Program.cs` contains the list of video extensions, subtitle extension and the episode regex near the top. Modify those arrays/values to support additional formats or extensions.

## Suggested improvements

- Add a dry-run mode to preview changes without modifying files.
- Add logging and a confirmation prompt before deleting folders/files.
- Support more subtitle extensions (.ass, .vtt) and multiple naming schemes.
- Better duplicate-handling when multiple subtitles match a single video.

## License

This project contains minimal utility code — add a LICENSE file if you want to apply a specific license.

---

If you want, I can add a short example folder structure and a dry-run option in the code next.
