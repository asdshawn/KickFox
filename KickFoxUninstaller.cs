using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
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

            Application.Run(new UninstallerForm());
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

    public class UninstallerForm : Form
    {
        private Button btnUninstall;
        private Button btnCancel;

        public UninstallerForm()
        {
            this.Text = "KickFox Uninstall";
            this.Size = new Size(450, 420);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = SystemColors.Control;

            Panel pnlHeader = new Panel(); pnlHeader.Dock = DockStyle.Top; pnlHeader.Height = 75; pnlHeader.BackColor = Color.White;
            Label lblTitle = new Label(); lblTitle.Text = "KickFox Uninstall"; lblTitle.Font = new Font(this.Font.FontFamily, 14, FontStyle.Bold); lblTitle.Location = new Point(20, 15); lblTitle.AutoSize = true;
            Label lblSubtitle = new Label(); lblSubtitle.Text = "Remove the application and all related data."; lblSubtitle.Location = new Point(22, 45); lblSubtitle.AutoSize = true; lblSubtitle.ForeColor = Color.DimGray;
            pnlHeader.Controls.Add(lblTitle); pnlHeader.Controls.Add(lblSubtitle);

            Label lblMsg = new Label(); lblMsg.Text = "The following items will be completely removed from your system:"; lblMsg.Location = new Point(20, 95); lblMsg.AutoSize = true;
            
            ListBox lstItems = new ListBox(); lstItems.Location = new Point(20, 120); lstItems.Size = new Size(390, 140);
            
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KickFox");
            string currentDir = AppDomain.CurrentDomain.BaseDirectory;
            string desktopShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "KickFox.lnk");

            lstItems.Items.Add("[Directory] " + currentDir);
            lstItems.Items.Add("[Directory] " + appData);
            lstItems.Items.Add("[Scheduled Task] KickFox");
            if (File.Exists(desktopShortcut))
            {
                lstItems.Items.Add("[Shortcut] " + desktopShortcut);
            }

            Label lblConfirm = new Label(); lblConfirm.Text = "Are you sure you want to proceed with the uninstallation?"; lblConfirm.Location = new Point(20, 280); lblConfirm.AutoSize = true; lblConfirm.Font = new Font(this.Font.FontFamily, 9, FontStyle.Bold);
            
            btnUninstall = new Button(); btnUninstall.Text = "Uninstall"; btnUninstall.Location = new Point(230, 325); btnUninstall.Width = 85; btnUninstall.Height = 32;
            btnUninstall.Click += BtnUninstall_Click;

            btnCancel = new Button(); btnCancel.Text = "Cancel"; btnCancel.Location = new Point(325, 325); btnCancel.Width = 85; btnCancel.Height = 32;
            btnCancel.Click += delegate { this.Close(); };

            this.Controls.Add(pnlHeader);
            this.Controls.Add(lblMsg);
            this.Controls.Add(lstItems);
            this.Controls.Add(lblConfirm);
            this.Controls.Add(btnUninstall);
            this.Controls.Add(btnCancel);
        }

        private void BtnUninstall_Click(object sender, EventArgs e)
        {
            btnUninstall.Enabled = false;
            
            try
            {
                foreach (var process in Process.GetProcessesByName("KickFox"))
                {
                    try { process.Kill(); } catch { }
                }

                var psi = new ProcessStartInfo("schtasks.exe", "/delete /tn \"KickFox\" /f");
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                using (var p = Process.Start(psi)) { p.WaitForExit(); }

                try
                {
                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string shortcutPath = Path.Combine(desktopPath, "KickFox.lnk");
                    if (File.Exists(shortcutPath)) File.Delete(shortcutPath);
                }
                catch { }

                try
                {
                    Registry.LocalMachine.DeleteSubKeyTree(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\KickFox");
                }
                catch { }

                string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KickFox");
                string currentDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
                string exePath = Process.GetCurrentProcess().MainModule.FileName;
                
                string batPath = Path.Combine(Path.GetTempPath(), "remove_kickfox.bat");
                string batContent = string.Format(
                    "@echo off\r\n" +
                    ":loop\r\n" +
                    "del /f /q \"{0}\"\r\n" +
                    "if exist \"{0}\" (\r\n" +
                    "  ping 127.0.0.1 -n 2 > nul\r\n" +
                    "  goto loop\r\n" +
                    ")\r\n" +
                    "rmdir /s /q \"{1}\"\r\n" +
                    "del /f /q \"{2}\\*.*\"\r\n" +
                    "rmdir /s /q \"{2}\"\r\n" +
                    "del \"%~f0\"\r\n", 
                    exePath, appData, currentDir);
                File.WriteAllText(batPath, batContent);
                
                var psiBat = new ProcessStartInfo(batPath);
                psiBat.WorkingDirectory = Path.GetTempPath();
                psiBat.CreateNoWindow = true;
                psiBat.UseShellExecute = false;
                psiBat.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psiBat);

                MessageBox.Show("Uninstallation complete.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Application.Exit();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Uninstallation encountered errors: " + ex.Message, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                btnUninstall.Enabled = true;
            }
        }
    }
}
