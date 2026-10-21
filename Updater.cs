using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;
using KickFox;

namespace KickFoxUpdater
{
    /// <summary>
    /// Usage: Updater.exe "path\to\Update.zip" "path\to\Update.zip.sig"
    ///
    /// 1. Verifies the package signature (refuses unsigned / tampered packages).
    /// 2. Waits for KickFox.exe to exit (aborts if it never does).
    /// 3. Backs up every file it is about to overwrite, then copies the new files.
    /// 4. On any failure, restores the backup.
    /// 5. Always restarts KickFox afterwards, so monitoring never silently disappears.
    ///
    /// Updater.exe cannot overwrite itself while running, so a new updater ships as
    /// "Updater.new.exe" and KickFox.exe swaps it in on its next start.
    /// </summary>
    class Program
    {
        static string logPath;

        static int Main(string[] args)
        {
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KickFox");
            try { Directory.CreateDirectory(appData); } catch { }
            logPath = Path.Combine(appData, "updater_log.txt");

            string installDir = AppDomain.CurrentDomain.BaseDirectory;
            string mainExePath = Path.Combine(installDir, "KickFox.exe");

            if (args.Length < 2)
            {
                Log("Refusing to update: signature file argument is missing.");
                return 1;
            }

            string zipPath = args[0];
            string sigPath = args[1];
            Log(string.Format("Updater {0} started. Package: {1}", AppInfo.Version, zipPath));

            string reason;
            if (!UpdateVerifier.Verify(zipPath, sigPath, out reason))
            {
                Log("Signature verification FAILED: " + reason + " Update aborted.");
                RestartMainApp(mainExePath);
                return 2;
            }
            Log("Signature verified.");

            if (!WaitForUnlock(mainExePath, 30))
            {
                Log("KickFox.exe is still running after 30s. Update aborted, nothing was changed.");
                return 3;
            }

            string workDir = Path.Combine(Path.GetTempPath(), "KickFoxUpdate_" + Guid.NewGuid().ToString("N"));
            string extractDir = Path.Combine(workDir, "extracted");
            string backupDir = Path.Combine(workDir, "backup");
            var replaced = new List<string>(); // relative paths that were overwritten (backup exists)
            var created = new List<string>();  // relative paths that did not exist before

            int exitCode = 0;
            try
            {
                Directory.CreateDirectory(extractDir);
                Directory.CreateDirectory(backupDir);
                ZipFile.ExtractToDirectory(zipPath, extractDir);

                foreach (string newPath in Directory.GetFiles(extractDir, "*", SearchOption.AllDirectories))
                {
                    string relativePath = newPath.Substring(extractDir.Length).TrimStart('\\', '/');

                    // Never overwrite the running updater; stage it for KickFox.exe to swap in.
                    if (Path.GetFileName(relativePath).Equals("Updater.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        relativePath = Path.Combine(Path.GetDirectoryName(relativePath), "Updater.new.exe");
                    }

                    string destPath = Path.Combine(installDir, relativePath);
                    string destDir = Path.GetDirectoryName(destPath);
                    if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

                    if (File.Exists(destPath))
                    {
                        string backupPath = Path.Combine(backupDir, relativePath);
                        Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
                        File.Copy(destPath, backupPath, true);
                        replaced.Add(relativePath);
                    }
                    else
                    {
                        created.Add(relativePath);
                    }

                    File.Copy(newPath, destPath, true);
                    Log("Updated: " + relativePath);
                }

                Log("Update completed successfully.");
            }
            catch (Exception ex)
            {
                exitCode = 4;
                Log("Update FAILED, rolling back: " + ex);
                Rollback(installDir, backupDir, replaced, created);
            }
            finally
            {
                try { Directory.Delete(workDir, true); } catch { }
                try { File.Delete(zipPath); } catch { }
                try { File.Delete(sigPath); } catch { }
                RestartMainApp(mainExePath);
            }
            return exitCode;
        }

        static void Rollback(string installDir, string backupDir, List<string> replaced, List<string> created)
        {
            foreach (string rel in replaced)
            {
                try { File.Copy(Path.Combine(backupDir, rel), Path.Combine(installDir, rel), true); Log("Restored: " + rel); }
                catch (Exception ex) { Log("Could not restore " + rel + ": " + ex.Message); }
            }
            foreach (string rel in created)
            {
                try { File.Delete(Path.Combine(installDir, rel)); } catch { }
            }
        }

        static void RestartMainApp(string mainExePath)
        {
            try
            {
                if (File.Exists(mainExePath) && Process.GetProcessesByName("KickFox").Length == 0)
                {
                    Process.Start(mainExePath, "--tray");
                    Log("KickFox restarted.");
                }
            }
            catch (Exception ex)
            {
                Log("Failed to restart KickFox: " + ex.Message);
            }
        }

        static bool WaitForUnlock(string file, int seconds)
        {
            for (int i = 0; i < seconds; i++)
            {
                if (!IsFileLocked(file)) return true;
                Thread.Sleep(1000);
            }
            return !IsFileLocked(file);
        }

        static bool IsFileLocked(string file)
        {
            if (!File.Exists(file)) return false;
            try
            {
                using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                return false;
            }
            catch (IOException)
            {
                return true;
            }
        }

        static void Log(string message)
        {
            try { File.AppendAllText(logPath, string.Format("{0:yyyy-MM-dd HH:mm:ss} - {1}{2}", DateTime.Now, message, Environment.NewLine)); }
            catch { }
        }
    }
}