using System;
using System.IO;

public static class FileUtils
{
    public static void MoveWithBackup(string source, string dest, string logPath, string itemDescription)
    {
        try
        {
            if (File.Exists(dest))
            {
                var backupDir = Path.Combine(Path.GetDirectoryName(logPath) ?? Path.GetDirectoryName(dest) ?? ".", "backups");
                var ts = DateTime.Now.ToString("yyyyMMddHHmmssfff");
                var backupName = Path.GetFileName(dest) + $".bak.{ts}";
                var backupPath = Path.Combine(backupDir, backupName);

                Directory.CreateDirectory(backupDir);
                File.Move(dest, backupPath, overwrite: true);
                Helpers.WriteOrange($"Backed up existing {itemDescription}: {Path.GetFileName(dest)} -> backups\\{backupName}");
                File.AppendAllText(logPath, $"[{DateTime.Now}] Backed up existing {itemDescription}: {dest} -> {backupPath}{Environment.NewLine}");
            }

            File.Move(source, dest, overwrite: true);
            Helpers.WriteYellow($"Moved {itemDescription}: {Path.GetFileName(source)} -> {Path.GetFileName(dest)}");
            File.AppendAllText(logPath, $"[{DateTime.Now}] Moved {itemDescription}: {source} -> {dest}{Environment.NewLine}");
        }
        catch (Exception ex)
        {
            Helpers.WriteRed($"Failed to move {itemDescription}: {source} — {ex.Message}");
            File.AppendAllText(logPath, $"[{DateTime.Now}] ERROR moving {itemDescription}: {source} -> {dest} : {ex.Message}{Environment.NewLine}");
        }
    }
}
