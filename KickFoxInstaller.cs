using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;
using Microsoft.Win32;

namespace KickFox
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            if (!IsAdministrator())
            {
                var psi = new ProcessStartInfo();
                psi.FileName = Application.ExecutablePath;
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                try { Process.Start(psi); } catch { }
                return;
            }

            Application.Run(new InstallerForm());
        }

        static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
    }

    public class InstallerForm : Form
    {
        private TextBox txtPath;
        private Button btnBrowse;
        private CheckBox chkLaunch;
        private CheckBox chkShortcut;
        private Button btnInstall;
        private Button btnCancel;
        private Label lblStatus;

        public InstallerForm()
        {
            this.Text = "KickFox Setup";
            this.Size = new Size(500, 440);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = SystemColors.Control;

            Panel pnlHeader = new Panel(); pnlHeader.Dock = DockStyle.Top; pnlHeader.Height = 75; pnlHeader.BackColor = Color.White;
            Label lblTitle = new Label(); lblTitle.Text = "KickFox Setup"; lblTitle.Font = new Font(this.Font.FontFamily, 14, FontStyle.Bold); lblTitle.Location = new Point(20, 15); lblTitle.AutoSize = true;
            Label lblSubtitle = new Label(); lblSubtitle.Text = "Keep your network connection alive automatically."; lblSubtitle.Location = new Point(22, 45); lblSubtitle.AutoSize = true; lblSubtitle.ForeColor = Color.DimGray;
            pnlHeader.Controls.Add(lblTitle); pnlHeader.Controls.Add(lblSubtitle);

            Label lblDesc = new Label(); 
            lblDesc.Text = "This background utility continuously monitors your internet connection. If the connection drops or gets stuck in a loop, it will automatically restart your Wi-Fi adapter to get you back online.";
            lblDesc.Location = new Point(20, 95); lblDesc.Size = new Size(440, 40);

            GroupBox grpPath = new GroupBox(); grpPath.Text = "Installation Path"; grpPath.Location = new Point(20, 145); grpPath.Size = new Size(445, 65);
            txtPath = new TextBox(); txtPath.Text = @"C:\Program Files\KickFox"; txtPath.Location = new Point(15, 25); txtPath.Width = 370;
            btnBrowse = new Button(); btnBrowse.Text = "..."; btnBrowse.Location = new Point(390, 24); btnBrowse.Width = 40;
            btnBrowse.Click += BtnBrowse_Click;
            grpPath.Controls.Add(txtPath); grpPath.Controls.Add(btnBrowse);

            GroupBox grpOptions = new GroupBox(); grpOptions.Text = "Additional Options"; grpOptions.Location = new Point(20, 225); grpOptions.Size = new Size(445, 80);
            chkLaunch = new CheckBox(); chkLaunch.Text = "Launch KickFox immediately after installation"; chkLaunch.Location = new Point(15, 25); chkLaunch.AutoSize = true; chkLaunch.Checked = true;
            chkShortcut = new CheckBox(); chkShortcut.Text = "Create a desktop shortcut for Settings UI"; chkShortcut.Location = new Point(15, 50); chkShortcut.AutoSize = true; chkShortcut.Checked = true;
            grpOptions.Controls.Add(chkLaunch); grpOptions.Controls.Add(chkShortcut);

            lblStatus = new Label(); lblStatus.Text = "Ready to install."; lblStatus.Location = new Point(20, 320); lblStatus.AutoSize = true; lblStatus.ForeColor = Color.Gray;

            btnInstall = new Button(); btnInstall.Text = "Install"; btnInstall.Location = new Point(285, 350); btnInstall.Width = 85; btnInstall.Height = 32;
            btnInstall.Click += BtnInstall_Click;

            btnCancel = new Button(); btnCancel.Text = "Cancel"; btnCancel.Location = new Point(380, 350); btnCancel.Width = 85; btnCancel.Height = 32;
            btnCancel.Click += delegate { this.Close(); };

            this.Controls.Add(pnlHeader);
            this.Controls.Add(lblDesc);
            this.Controls.Add(grpPath);
            this.Controls.Add(grpOptions);
            this.Controls.Add(lblStatus);
            this.Controls.Add(btnInstall);
            this.Controls.Add(btnCancel);
        }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Select installation folder. The app will be placed in a 'KickFox' subfolder.";
                fbd.ShowNewFolderButton = true;
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    string path = fbd.SelectedPath;
                    if (Path.GetFileName(path).Equals("KickFox", StringComparison.OrdinalIgnoreCase))
                    {
                        txtPath.Text = path;
                    }
                    else
                    {
                        txtPath.Text = Path.Combine(path, "KickFox");
                    }
                }
            }
        }

        private void CreateShortcut(string targetDir)
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string shortcutPath = Path.Combine(desktopPath, "KickFox.lnk");
                string targetExe = Path.Combine(targetDir, "KickFox.exe");
                
                string psCommand = string.Format("$s=(New-Object -COM WScript.Shell).CreateShortcut('{0}');$s.TargetPath='{1}';$s.WorkingDirectory='{2}';$s.IconLocation='{1},0';$s.Save()", shortcutPath, targetExe, targetDir);
                
                var psi = new ProcessStartInfo("powershell.exe", "-WindowStyle Hidden -Command \"" + psCommand + "\"");
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                using (var p = Process.Start(psi)) { p.WaitForExit(); }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to create shortcut: " + ex.Message, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static readonly string[] PayloadFiles = { "KickFox.exe", "KickFoxUninstaller.exe", "Updater.exe" };

        private static void ExtractPayload(string fileName, string destPath)
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload." + fileName))
            {
                if (s != null)
                {
                    using (FileStream fs = new FileStream(destPath, FileMode.Create, FileAccess.Write)) s.CopyTo(fs);
                    return;
                }
            }

            // Fallback for developer builds: use a file placed next to the installer.
            string sideBySide = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
            if (!File.Exists(sideBySide))
                throw new FileNotFoundException("This installer does not contain " + fileName + ". Please download KickFoxInstaller.exe again from GitHub Releases.", fileName);
            File.Copy(sideBySide, destPath, true);
        }

        private static void StopRunningInstances()
        {
            try
            {
                var psi = new ProcessStartInfo("schtasks.exe", "/end /tn \"KickFox\"");
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                using (var p = Process.Start(psi)) { p.WaitForExit(10000); }
            }
            catch { }

            foreach (var process in Process.GetProcessesByName("KickFox"))
            {
                try { process.Kill(); process.WaitForExit(5000); } catch { }
            }
        }

        private void BtnInstall_Click(object sender, EventArgs e)
        {
            btnInstall.Enabled = false;
            string targetDir = txtPath.Text;
            lblStatus.Text = "Installing...";

            try
            {
                if (!Directory.Exists(targetDir))
                    Directory.CreateDirectory(targetDir);

                // An existing install may be running; stop it so its files can be overwritten.
                StopRunningInstances();

                // The program files are embedded inside this installer, so a single
                // downloaded KickFoxInstaller.exe is enough to install.
                foreach (string name in PayloadFiles)
                {
                    ExtractPayload(name, Path.Combine(targetDir, name));
                }
                try { File.Delete(Path.Combine(targetDir, "Updater.new.exe")); } catch { }

                // Register Scheduled Task (on logon, highest privilege)
                string exePath = Path.Combine(targetDir, "KickFox.exe");
                string taskCmd = string.Format("/create /tn \"KickFox\" /tr \"\\\"{0}\\\" --tray\" /sc onlogon /rl highest /f", exePath);
                
                var psi = new ProcessStartInfo("schtasks.exe", taskCmd);
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                using (var p = Process.Start(psi))
                {
                    p.WaitForExit();
                }

                if (chkShortcut.Checked)
                {
                    CreateShortcut(targetDir);
                }

                // Register in Windows "Programs and Features"
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\KickFox"))
                    {
                        key.SetValue("DisplayName", "KickFox");
                        key.SetValue("DisplayIcon", Path.Combine(targetDir, "KickFox.exe"));
                        key.SetValue("UninstallString", string.Format("\"{0}\"", Path.Combine(targetDir, "KickFoxUninstaller.exe")));
                        key.SetValue("Publisher", "Shawn");
                        key.SetValue("DisplayVersion", AppInfo.Version);
                        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    }
                }
                catch (Exception regEx)
                {
                    Debug.WriteLine(regEx.Message);
                }

                if (chkLaunch.Checked)
                {
                    var psiStart = new ProcessStartInfo("schtasks.exe", "/run /tn \"KickFox\"");
                    psiStart.CreateNoWindow = true;
                    psiStart.UseShellExecute = false;
                    Process.Start(psiStart);
                }

                MessageBox.Show("Installation complete! KickFox is installed and registered.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Installation failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnInstall.Enabled = true;
                lblStatus.Text = "Failed.";
            }
        }
    }
}
