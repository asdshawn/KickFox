using System;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using System.Threading;

namespace KickFoxUpdater
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                return;
            }

            string zipPath = args[0];
            if (!File.Exists(zipPath))
            {
                return;
            }

            string currentDir = AppDomain.CurrentDomain.BaseDirectory;
            string mainExePath = Path.Combine(currentDir, "KickFox.exe");

            // Wait for main app to exit completely
            int maxWait = 30; // 30 seconds
            while (maxWait > 0)
            {
                if (!IsFileLocked(mainExePath))
                {
                    break;
                }
                Thread.Sleep(1000);
                maxWait--;
            }

            try
            {
                // We extract the zip to a temporary folder first, then copy files over, 
                // to avoid issues with extracting over locked files or partial extracts.
                string tempExtractDir = Path.Combine(Path.GetTempPath(), "KickFoxUpdateExtracted");
                if (Directory.Exists(tempExtractDir))
                {
                    Directory.Delete(tempExtractDir, true);
                }
                Directory.CreateDirectory(tempExtractDir);

                ZipFile.ExtractToDirectory(zipPath, tempExtractDir);

                // Copy files over
                foreach (string newPath in Directory.GetFiles(tempExtractDir, "*.*", SearchOption.AllDirectories))
                {
                    string relativePath = newPath.Substring(tempExtractDir.Length + 1);
                    string destPath = Path.Combine(currentDir, relativePath);
                    
                    // Don't overwrite Updater.exe while it's running
                    if (Path.GetFileName(destPath).Equals("Updater.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        continue; 
                    }

                    string destDir = Path.GetDirectoryName(destPath);
                    if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

                    File.Copy(newPath, destPath, true);
                }

                // Cleanup
                Directory.Delete(tempExtractDir, true);
                try { File.Delete(zipPath); } catch { }

                // Restart app
                if (File.Exists(mainExePath))
                {
                    Process.Start(mainExePath);
                }
            }
            catch (Exception ex)
            {
                // Write error log
                File.WriteAllText(Path.Combine(currentDir, "updater_error.log"), ex.ToString());
            }
        }

        static bool IsFileLocked(string file)
        {
            if (!File.Exists(file)) return false;
            try
            {
                using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    stream.Close();
                }
            }
            catch (IOException)
            {
                return true;
            }
            return false;
        }
    }
}
