using SubRename;
using System;
using System.IO;
using Xunit;

namespace SubRenameWpf.Tests;
public class SubtitleRenamerAppTests
{
    [Fact]
    public void RunWithOptions_CreatesLogFile()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            // Act
            SubtitleRenamerApp.RunWithOptions(
                tempDir,
                cleanup: false,
                confirmDeletes: false,
                log: _ => {},
                confirmDelete: null
            );

            // Assert
            var logFile = Path.Combine(tempDir, "match-subtitles.log");
            Assert.True(File.Exists(logFile));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
