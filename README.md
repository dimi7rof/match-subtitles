# This app is 100% created by AI

# SubtitleRenamerWpf

A Windows desktop (WPF) application for matching, renaming, and cleaning up TV episode video and subtitle files in a folder. Provides a graphical interface for selecting a folder, running the process, and viewing logs/results.

## Features

- Select a folder containing TV episodes and subtitles
- Moves video files from subfolders to the top folder
- Matches and renames subtitles to match video files (supports .srt, .ass, .vtt)
- Deletes unrelated files and empty subfolders
- Shows a log of all actions and errors

## Usage

1. Launch the app.
2. Select the folder to process.
3. Click the button to start matching/renaming.
4. Review the log output in the app.

## Build

- Requires .NET 7+ SDK
- Build with `dotnet build` or open the project in Visual Studio/VS Code

## Migration

This app is a WPF port of the original console-based SubtitleRenamer. All logic is now accessible via a graphical interface.
