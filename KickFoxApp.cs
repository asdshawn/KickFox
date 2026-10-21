using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading;
using System.Collections.Generic;
using System.Security.Principal;
using System.Net;
using System.Web.Script.Serialization;

namespace KickFox
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            if (!IsAdministrator())
            {
                var psi = new ProcessStartInfo();
                psi.FileName = Application.ExecutablePath;
                psi.Arguments = string.Join(" ", args);
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                try { Process.Start(psi); } catch { }
                return;
            }

            bool isNewInstance;
            Mutex mutex = new Mutex(true, "{9A6F0AC4-B9A1-45fd-A8CF-72F04E6BDE8F}", out isNewInstance);

            if (!isNewInstance)
            {
                try
                {
                    using (EventWaitHandle ewh = EventWaitHandle.OpenExisting("KickFoxShowEvent"))
                    {
                        ewh.Set();
                    }
                }
                catch { }
                return;
            }

            bool startMinimized = false;
            if (args.Length > 0 && args[0] == "--tray")
            {
                startMinimized = true;
            }

            Application.Run(new MonitorForm(startMinimized));
            GC.KeepAlive(mutex);
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

    public class Config
    {
        public int CheckIntervalSeconds { get; set; }
        public bool UseMaxRetries { get; set; }
        public int MaxRetries { get; set; }
        public List<string> PingTargets { get; set; }
        public bool IsEnabled { get; set; }
        public int UpdateCheckDays { get; set; }
        public string LastUpdateCheck { get; set; }
        
        public Config() 
        { 
            CheckIntervalSeconds = 60; 
            UseMaxRetries = true;
            MaxRetries = 3; 
            PingTargets = new List<string> { "8.8.8.8", "1.1.1.1", "168.95.1.1" }; 
            IsEnabled = true; 
            UpdateCheckDays = 1;
            LastUpdateCheck = DateTime.MinValue.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }

    public class TargetInputDialog : Form
    {
        public string TargetIP { get; private set; }
        private TextBox txtInput;

        public TargetInputDialog()
        {
            this.Text = "Add Ping Target";
            this.Size = new Size(300, 150);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            Label lbl = new Label(); lbl.Text = "Enter IP Address or Hostname:"; lbl.Location = new Point(20, 20); lbl.AutoSize = true;
            txtInput = new TextBox(); txtInput.Location = new Point(20, 45); txtInput.Width = 240;

            Button btnOk = new Button(); btnOk.Text = "OK"; btnOk.Location = new Point(100, 80); btnOk.Width = 75;
            btnOk.Click += delegate { TargetIP = txtInput.Text.Trim(); this.DialogResult = DialogResult.OK; this.Close(); };

            Button btnCancel = new Button(); btnCancel.Text = "Cancel"; btnCancel.Location = new Point(185, 80); btnCancel.Width = 75;
            btnCancel.Click += delegate { this.DialogResult = DialogResult.Cancel; this.Close(); };

            this.Controls.Add(lbl);
            this.Controls.Add(txtInput);
            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);
            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }
    }

    public class MonitorForm : Form
    {
        public const string CURRENT_VERSION = "v" + AppInfo.Version;
        
        private EventWaitHandle showEvent;
        private Thread showEventThread;
        private NotifyIcon trayIcon;
        private ContextMenu trayMenu;
        private System.Windows.Forms.Timer monitorTimer;
        private int consecutiveFailures = 0;
        private bool isSleeping = false;
        private bool isChecking = false;
        private DateTime sleepUntil = DateTime.MinValue;
        private Config config;
        private string configPath;
        private string logPath;

        // Dynamic Icons
        private Icon iconNormal;
        private Icon iconKickA;
        private Icon iconKickB;
        private Icon iconFatal;
        private System.Windows.Forms.Timer animTimer;
        private bool isAnimFrameA = true;
        private string currentTrayMode = "Normal"; // Normal, Kick, Fatal

        // UI Controls
        private CheckBox chkEnable;
        private GroupBox grpSettings;
        private TextBox txtInterval;
        private CheckBox chkUseMaxRetries;
        private TextBox txtMaxRetries;
        private TextBox txtUpdateDays;
        
        private ListBox lstTargets;
        private Button btnAddTarget;
        private Button btnRemoveTarget;
        private Button btnMoveUp;
        private Button btnMoveDown;

        private GroupBox grpStatus;
        private Panel pnlStatusLight;
        private Label lblStatus;

        private Button btnConfirm;
        private Button btnApply;
        private Button btnCancel;
        private Button btnCheckUpdate;

        public MonitorForm(bool startMinimized)
        {
            showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "KickFoxShowEvent");
            showEventThread = new Thread(new ThreadStart(WaitShowEvent));
            showEventThread.IsBackground = true;
            showEventThread.Start();

            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KickFox");
            if (!Directory.Exists(appData)) Directory.CreateDirectory(appData);
            
            configPath = Path.Combine(appData, "config.ini");
            logPath = Path.Combine(appData, "monitor_log.txt");
            LoadConfig();
            ApplyStagedUpdater();

            LoadDynamicIcons();

            InitializeUI();
            InitializeTray();
            InitializeTimer();

            if (startMinimized)
            {
                this.WindowState = FormWindowState.Minimized;
                this.ShowInTaskbar = false;
            }
        }

        private void LoadDynamicIcons()
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            try { iconNormal = new Icon(asm.GetManifestResourceStream("IconNormal.ico")); } catch { iconNormal = SystemIcons.Shield; }
            try { iconKickA = new Icon(asm.GetManifestResourceStream("IconKickA.ico")); } catch { iconKickA = SystemIcons.Warning; }
            try { iconKickB = new Icon(asm.GetManifestResourceStream("IconKickB.ico")); } catch { iconKickB = SystemIcons.Warning; }
            try { iconFatal = new Icon(asm.GetManifestResourceStream("IconFatal.ico")); } catch { iconFatal = SystemIcons.Error; }

            animTimer = new System.Windows.Forms.Timer();
            animTimer.Interval = 500;
            animTimer.Tick += AnimTimer_Tick;
        }

        private void AnimTimer_Tick(object sender, EventArgs e)
        {
            isAnimFrameA = !isAnimFrameA;
            if (currentTrayMode == "Kick")
            {
                trayIcon.Icon = isAnimFrameA ? iconKickA : iconKickB;
            }
        }

        private void SetTrayState(string mode, string tooltipText)
        {
            currentTrayMode = mode;
            if (mode == "Normal")
            {
                animTimer.Stop();
                trayIcon.Icon = iconNormal;
            }
            else if (mode == "Kick")
            {
                animTimer.Start();
                trayIcon.Icon = iconKickA;
                isAnimFrameA = true;
            }
            else if (mode == "Fatal")
            {
                animTimer.Stop();
                trayIcon.Icon = iconFatal;
            }

            if (tooltipText.Length > 63) tooltipText = tooltipText.Substring(0, 60) + "...";
            trayIcon.Text = tooltipText;
        }

        private void WaitShowEvent()
        {
            while (true)
            {
                try
                {
                    if (showEvent.WaitOne())
                    {
                        if (this.IsHandleCreated)
                        {
                            this.Invoke(new Action(delegate { OnSettings(null, null); }));
                        }
                    }
                }
                catch { Thread.Sleep(1000); }
            }
        }

        private void LoadConfig()
        {
            config = new Config();
            if (File.Exists(configPath))
            {
                var lines = File.ReadAllLines(configPath);
                foreach (var line in lines)
                {
                    var parts = line.Split('=');
                    if (parts.Length == 2)
                    {
                        if (parts[0] == "CheckIntervalSeconds") { int interval; if (int.TryParse(parts[1], out interval)) config.CheckIntervalSeconds = interval; }
                        if (parts[0] == "UseMaxRetries") { bool useMax; if (bool.TryParse(parts[1], out useMax)) config.UseMaxRetries = useMax; }
                        if (parts[0] == "MaxRetries") { int retries; if (int.TryParse(parts[1], out retries)) config.MaxRetries = retries; }
                        if (parts[0] == "IsEnabled") { bool isen; if (bool.TryParse(parts[1], out isen)) config.IsEnabled = isen; }
                        if (parts[0] == "UpdateCheckDays") { int ud; if (int.TryParse(parts[1], out ud)) config.UpdateCheckDays = ud; }
                        if (parts[0] == "LastUpdateCheck") { config.LastUpdateCheck = parts[1]; }
                        if (parts[0] == "PingTargets") 
                        {
                            config.PingTargets.Clear();
                            var targs = parts[1].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var t in targs) config.PingTargets.Add(t.Trim());
                        }
                    }
                }
            }
            else
            {
                SaveConfig();
            }
        }

        private void SaveConfig()
        {
            var lines = new List<string>();
            lines.Add(string.Format("CheckIntervalSeconds={0}", config.CheckIntervalSeconds));
            lines.Add(string.Format("UseMaxRetries={0}", config.UseMaxRetries));
            lines.Add(string.Format("MaxRetries={0}", config.MaxRetries));
            lines.Add(string.Format("IsEnabled={0}", config.IsEnabled));
            lines.Add(string.Format("UpdateCheckDays={0}", config.UpdateCheckDays));
            lines.Add(string.Format("LastUpdateCheck={0}", config.LastUpdateCheck));
            lines.Add(string.Format("PingTargets={0}", string.Join(",", config.PingTargets.ToArray())));
            File.WriteAllLines(configPath, lines.ToArray());
        }

        private void Log(string message)
        {
            string logMsg = string.Format("{0:yyyy-MM-dd HH:mm:ss} - {1}", DateTime.Now, message);
            try
            {
                File.AppendAllText(logPath, logMsg + Environment.NewLine);
            }
            catch { }
        }

        private void UpdateStatusUI(Color color, string mainStatus)
        {
            string retriesStr = config.UseMaxRetries ? string.Format("{0} / {1}", consecutiveFailures, config.MaxRetries) : string.Format("{0} (No Limit)", consecutiveFailures);
            string timeStr = DateTime.Now.ToString("HH:mm:ss");
            string fullStatus = string.Format("{0}\nLast Checked: {1}\nFailed Retries: {2}", mainStatus, timeStr, retriesStr);

            if (this.InvokeRequired)
            {
                this.Invoke(new Action(delegate { pnlStatusLight.BackColor = color; lblStatus.Text = fullStatus; }));
            }
            else
            {
                pnlStatusLight.BackColor = color; lblStatus.Text = fullStatus;
            }
        }

        private void InitializeUI()
        {
            this.Text = "KickFox Settings (" + CURRENT_VERSION + ")";
            this.Size = new Size(420, 560);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            chkEnable = new CheckBox(); chkEnable.Text = "Enable KickFox Monitoring"; chkEnable.Location = new Point(20, 15); chkEnable.AutoSize = true; 
            chkEnable.Font = new Font(chkEnable.Font, FontStyle.Bold);
            chkEnable.Checked = config.IsEnabled;
            chkEnable.CheckedChanged += delegate { grpSettings.Enabled = chkEnable.Checked; };

            grpSettings = new GroupBox(); grpSettings.Text = "Monitor Settings"; grpSettings.Location = new Point(20, 45); grpSettings.Size = new Size(360, 265);
            grpSettings.Enabled = config.IsEnabled;

            var lbl1 = new Label(); lbl1.Text = "Check Interval (sec):"; lbl1.Location = new Point(20, 30); lbl1.AutoSize = true;
            txtInterval = new TextBox(); txtInterval.Text = config.CheckIntervalSeconds.ToString(); txtInterval.Location = new Point(160, 27); txtInterval.Width = 60;
            
            chkUseMaxRetries = new CheckBox(); chkUseMaxRetries.Text = "Enable Max Retries:"; chkUseMaxRetries.Location = new Point(20, 60); chkUseMaxRetries.AutoSize = true;
            chkUseMaxRetries.Checked = config.UseMaxRetries;
            
            txtMaxRetries = new TextBox(); txtMaxRetries.Text = config.MaxRetries.ToString(); txtMaxRetries.Location = new Point(160, 57); txtMaxRetries.Width = 60;
            txtMaxRetries.Enabled = config.UseMaxRetries;
            chkUseMaxRetries.CheckedChanged += delegate { txtMaxRetries.Enabled = chkUseMaxRetries.Checked; };

            var lbl3 = new Label(); lbl3.Text = "Ping Targets (Checked in order):"; lbl3.Location = new Point(20, 95); lbl3.AutoSize = true;
            
            lstTargets = new ListBox(); lstTargets.Location = new Point(20, 115); lstTargets.Size = new Size(240, 100);
            foreach (var t in config.PingTargets) lstTargets.Items.Add(t);

            btnMoveUp = new Button(); btnMoveUp.Text = "Up"; btnMoveUp.Location = new Point(270, 115); btnMoveUp.Width = 70;
            btnMoveUp.Click += delegate {
                int i = lstTargets.SelectedIndex;
                if (i > 0) {
                    object item = lstTargets.SelectedItem;
                    lstTargets.Items.RemoveAt(i);
                    lstTargets.Items.Insert(i - 1, item);
                    lstTargets.SelectedIndex = i - 1;
                }
            };

            btnMoveDown = new Button(); btnMoveDown.Text = "Down"; btnMoveDown.Location = new Point(270, 145); btnMoveDown.Width = 70;
            btnMoveDown.Click += delegate {
                int i = lstTargets.SelectedIndex;
                if (i >= 0 && i < lstTargets.Items.Count - 1) {
                    object item = lstTargets.SelectedItem;
                    lstTargets.Items.RemoveAt(i);
                    lstTargets.Items.Insert(i + 1, item);
                    lstTargets.SelectedIndex = i + 1;
                }
            };

            btnAddTarget = new Button(); btnAddTarget.Text = "Add Target"; btnAddTarget.Location = new Point(20, 220); btnAddTarget.Width = 115;
            btnAddTarget.Click += delegate { 
                using (TargetInputDialog dlg = new TargetInputDialog()) {
                    if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.TargetIP)) {
                        lstTargets.Items.Add(dlg.TargetIP);
                    }
                }
            };

            btnRemoveTarget = new Button(); btnRemoveTarget.Text = "Remove Target"; btnRemoveTarget.Location = new Point(145, 220); btnRemoveTarget.Width = 115;
            btnRemoveTarget.Click += delegate { if (lstTargets.SelectedIndex >= 0) lstTargets.Items.RemoveAt(lstTargets.SelectedIndex); };

            grpSettings.Controls.Add(lbl1);
            grpSettings.Controls.Add(txtInterval);
            grpSettings.Controls.Add(chkUseMaxRetries);
            grpSettings.Controls.Add(txtMaxRetries);
            grpSettings.Controls.Add(lbl3);
            grpSettings.Controls.Add(lstTargets);
            grpSettings.Controls.Add(btnAddTarget);
            grpSettings.Controls.Add(btnRemoveTarget);
            grpSettings.Controls.Add(btnMoveUp);
            grpSettings.Controls.Add(btnMoveDown);

            GroupBox grpUpdate = new GroupBox(); grpUpdate.Text = "Updates"; grpUpdate.Location = new Point(20, 320); grpUpdate.Size = new Size(360, 60);
            var lblUpdate = new Label(); lblUpdate.Text = "Check every (days):"; lblUpdate.Location = new Point(20, 25); lblUpdate.AutoSize = true;
            txtUpdateDays = new TextBox(); txtUpdateDays.Text = config.UpdateCheckDays.ToString(); txtUpdateDays.Location = new Point(135, 22); txtUpdateDays.Width = 40;
            btnCheckUpdate = new Button(); btnCheckUpdate.Text = "Check Now"; btnCheckUpdate.Location = new Point(230, 20); btnCheckUpdate.Width = 110;
            btnCheckUpdate.Click += delegate { CheckForUpdates(true); };
            grpUpdate.Controls.Add(lblUpdate);
            grpUpdate.Controls.Add(txtUpdateDays);
            grpUpdate.Controls.Add(btnCheckUpdate);

            grpStatus = new GroupBox(); grpStatus.Text = "System Status"; grpStatus.Location = new Point(20, 390); grpStatus.Size = new Size(360, 75);
            pnlStatusLight = new Panel(); pnlStatusLight.Location = new Point(20, 30); pnlStatusLight.Size = new Size(20, 20); pnlStatusLight.BackColor = Color.Gray;
            lblStatus = new Label(); lblStatus.Text = "Ready\nLast Checked: N/A\nFailed Retries: 0"; lblStatus.Location = new Point(50, 20); lblStatus.AutoSize = false; lblStatus.Size = new Size(300, 50); lblStatus.ForeColor = Color.DarkSlateGray;
            grpStatus.Controls.Add(pnlStatusLight);
            grpStatus.Controls.Add(lblStatus);

            btnConfirm = new Button(); btnConfirm.Text = "Confirm"; btnConfirm.Location = new Point(140, 475); btnConfirm.Width = 75; btnConfirm.Height = 30;
            btnConfirm.Click += delegate { ApplySettings(); this.Close(); };

            btnApply = new Button(); btnApply.Text = "Apply"; btnApply.Location = new Point(225, 475); btnApply.Width = 75; btnApply.Height = 30;
            btnApply.Click += delegate { ApplySettings(); };

            btnCancel = new Button(); btnCancel.Text = "Cancel"; btnCancel.Location = new Point(310, 475); btnCancel.Width = 75; btnCancel.Height = 30;
            btnCancel.Click += delegate { LoadConfigToUI(); this.Close(); };

            this.Controls.Add(chkEnable);
            this.Controls.Add(grpSettings);
            this.Controls.Add(grpUpdate);
            this.Controls.Add(grpStatus);
            this.Controls.Add(btnConfirm);
            this.Controls.Add(btnApply);
            this.Controls.Add(btnCancel);

            this.FormClosing += MonitorForm_FormClosing;
        }

        // ───────────────────────── Auto-update ─────────────────────────

        private class ReleaseInfo
        {
            public string Tag;
            public string ZipUrl;
            public string SigUrl;
        }

        /// <summary>Swap in an updater that was staged by a previous update (Updater.exe can't overwrite itself).</summary>
        private void ApplyStagedUpdater()
        {
            try
            {
                string dir = AppDomain.CurrentDomain.BaseDirectory;
                string staged = Path.Combine(dir, "Updater.new.exe");
                string current = Path.Combine(dir, "Updater.exe");
                if (!File.Exists(staged)) return;
                if (File.Exists(current)) File.Delete(current);
                File.Move(staged, current);
                Log("Installed staged Updater.exe.");
            }
            catch (Exception ex)
            {
                Log("Failed to install staged Updater.exe: " + ex.Message);
            }
        }

        private void RunOnUI(Action action)
        {
            if (this.IsDisposed) return;
            try
            {
                if (this.InvokeRequired) this.BeginInvoke(action);
                else action();
            }
            catch (InvalidOperationException) { }
        }

        private static Version ParseVersion(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return null;
            string s = tag.Trim().TrimStart('v', 'V');
            int cut = s.IndexOfAny(new[] { '-', '+' });
            if (cut >= 0) s = s.Substring(0, cut);
            Version v;
            if (!Version.TryParse(s, out v)) return null;
            return new Version(v.Major, v.Minor, Math.Max(v.Build, 0)); // treat "1.1" as "1.1.0"
        }

        private static WebClient CreateWebClient()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var client = new WebClient();
            client.Encoding = System.Text.Encoding.UTF8;
            client.Headers.Add("User-Agent", "KickFox-Updater/" + AppInfo.Version);
            return client;
        }

        private static ReleaseInfo FetchLatestRelease()
        {
            string json;
            using (WebClient client = CreateWebClient())
            {
                client.Headers.Add("Accept", "application/vnd.github+json");
                json = client.DownloadString("https://api.github.com/repos/" + AppInfo.GitHubRepo + "/releases/latest");
            }

            var release = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
            if (release == null || !release.ContainsKey("tag_name"))
                throw new InvalidDataException("Unexpected response from GitHub.");

            var info = new ReleaseInfo();
            info.Tag = Convert.ToString(release["tag_name"]);

            object assets;
            if (release.TryGetValue("assets", out assets) && assets is object[])
            {
                foreach (object item in (object[])assets)
                {
                    var asset = item as Dictionary<string, object>;
                    if (asset == null || !asset.ContainsKey("name") || !asset.ContainsKey("browser_download_url")) continue;
                    string name = Convert.ToString(asset["name"]);
                    string url = Convert.ToString(asset["browser_download_url"]);
                    // Match exact asset names only — never "the first .zip we happen to see".
                    if (name.Equals(AppInfo.UpdateAssetName, StringComparison.OrdinalIgnoreCase)) info.ZipUrl = url;
                    else if (name.Equals(AppInfo.SignatureAssetName, StringComparison.OrdinalIgnoreCase)) info.SigUrl = url;
                }
            }
            return info;
        }

        private void CheckForUpdates(bool manual)
        {
            if (!manual && config.UpdateCheckDays > 0)
            {
                DateTime lastCheck;
                if (!DateTime.TryParse(config.LastUpdateCheck, out lastCheck)) lastCheck = DateTime.MinValue;
                if ((DateTime.Now - lastCheck).TotalDays < config.UpdateCheckDays) return;
            }

            if (manual)
            {
                btnCheckUpdate.Text = "Checking...";
                btnCheckUpdate.Enabled = false;
            }

            ThreadPool.QueueUserWorkItem(state =>
            {
                ReleaseInfo info = null;
                Exception error = null;
                try { info = FetchLatestRelease(); }
                catch (Exception ex) { error = ex; }
                RunOnUI(delegate { OnUpdateCheckCompleted(manual, info, error); });
            });
        }

        private void OnUpdateCheckCompleted(bool manual, ReleaseInfo info, Exception error)
        {
            btnCheckUpdate.Text = "Check Now";
            btnCheckUpdate.Enabled = true;

            if (error != null)
            {
                Log("Update check failed: " + error.Message);
                if (manual) MessageBox.Show(this, "Failed to check for updates: " + error.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            config.LastUpdateCheck = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            SaveConfig();

            Version latest = ParseVersion(info.Tag);
            Version current = ParseVersion(AppInfo.Version);
            if (latest == null || current == null || latest <= current)
            {
                if (manual) MessageBox.Show(this, string.Format("You are using the latest version of KickFox ({0}).", CURRENT_VERSION), "No Updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (info.ZipUrl == null || info.SigUrl == null)
            {
                Log(string.Format("Release {0} is missing {1} or {2}; skipping auto-update.", info.Tag, AppInfo.UpdateAssetName, AppInfo.SignatureAssetName));
                if (manual) MessageBox.Show(this, string.Format("KickFox {0} is available, but it cannot be installed automatically.\n\nPlease download the installer from:\nhttps://github.com/{1}/releases/latest", info.Tag, AppInfo.GitHubRepo), "Update Available", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            PromptUpdate(info);
        }

        private void PromptUpdate(ReleaseInfo info)
        {
            DialogResult result = MessageBox.Show(
                string.Format("A new version of KickFox ({0}) is available. You are using {1}.\n\nDownload and install it now?", info.Tag, CURRENT_VERSION),
                "Update Available", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (result != DialogResult.Yes) return;

            btnCheckUpdate.Text = "Downloading...";
            btnCheckUpdate.Enabled = false;

            ThreadPool.QueueUserWorkItem(state =>
            {
                string dir = Path.Combine(Path.GetTempPath(), "KickFoxDownload");
                string zipPath = Path.Combine(dir, AppInfo.UpdateAssetName);
                string sigPath = Path.Combine(dir, AppInfo.SignatureAssetName);
                string error = null;
                try
                {
                    Directory.CreateDirectory(dir);
                    using (WebClient client = CreateWebClient()) client.DownloadFile(info.ZipUrl, zipPath);
                    using (WebClient client = CreateWebClient()) client.DownloadFile(info.SigUrl, sigPath);

                    string reason;
                    if (!UpdateVerifier.Verify(zipPath, sigPath, out reason))
                    {
                        error = "The downloaded update failed signature verification and was discarded.\n\n" + reason;
                        try { File.Delete(zipPath); File.Delete(sigPath); } catch { }
                    }
                }
                catch (Exception ex)
                {
                    error = "Download failed: " + ex.Message;
                }
                RunOnUI(delegate { OnUpdateDownloaded(info, zipPath, sigPath, error); });
            });
        }

        private void OnUpdateDownloaded(ReleaseInfo info, string zipPath, string sigPath, string error)
        {
            btnCheckUpdate.Text = "Check Now";
            btnCheckUpdate.Enabled = true;

            if (error != null)
            {
                Log(error.Replace("\n", " "));
                MessageBox.Show(error, "Update Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string updaterPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Updater.exe");
            if (!File.Exists(updaterPath))
            {
                MessageBox.Show("Updater.exe not found! Please reinstall KickFox from GitHub.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                var psi = new ProcessStartInfo(updaterPath, string.Format("\"{0}\" \"{1}\"", zipPath, sigPath));
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                Process.Start(psi);
                Log("Signature verified. Handing over to Updater.exe for " + info.Tag);
                OnExit(null, null); // Updater waits for this process to exit, then restarts KickFox.
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not start the updater: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void LoadConfigToUI()
        {
            txtInterval.Text = config.CheckIntervalSeconds.ToString();
            chkUseMaxRetries.Checked = config.UseMaxRetries;
            txtMaxRetries.Text = config.MaxRetries.ToString();
            txtUpdateDays.Text = config.UpdateCheckDays.ToString();
            lstTargets.Items.Clear();
            foreach (var t in config.PingTargets) lstTargets.Items.Add(t);
            chkEnable.Checked = config.IsEnabled;
            grpSettings.Enabled = config.IsEnabled;
        }

        private void ApplySettings()
        {
            int interval;
            int retries;
            int uDays;
            if (int.TryParse(txtInterval.Text, out interval) && interval > 0) config.CheckIntervalSeconds = interval;
            if (int.TryParse(txtUpdateDays.Text, out uDays) && uDays >= 0) config.UpdateCheckDays = uDays;
            
            config.UseMaxRetries = chkUseMaxRetries.Checked;
            if (int.TryParse(txtMaxRetries.Text, out retries) && retries >= 0) config.MaxRetries = retries;
            
            config.PingTargets.Clear();
            foreach (var item in lstTargets.Items) config.PingTargets.Add(item.ToString());
            
            config.IsEnabled = chkEnable.Checked;
            SaveConfig();
            
            monitorTimer.Interval = config.CheckIntervalSeconds * 1000;
            
            if (!config.IsEnabled)
            {
                UpdateStatusUI(Color.Gray, "Monitoring is Disabled.");
                SetTrayState("Fatal", "KickFox: 監控已停用");
            }
            else
            {
                UpdateStatusUI(Color.LimeGreen, "Monitoring is Enabled. Waiting for next tick...");
                SetTrayState("Normal", "KickFox: 等待首次檢查中...");
            }
        }

        private void InitializeTray()
        {
            trayMenu = new ContextMenu();
            trayMenu.MenuItems.Add("Settings", OnSettings);
            trayMenu.MenuItems.Add("-");
            trayMenu.MenuItems.Add("Exit Monitor", OnExit);

            trayIcon = new NotifyIcon();
            trayIcon.ContextMenu = trayMenu;
            trayIcon.Visible = true;
            trayIcon.DoubleClick += OnSettings;
            
            SetTrayState("Normal", "KickFox: 啟動中...");
        }

        private void InitializeTimer()
        {
            monitorTimer = new System.Windows.Forms.Timer();
            monitorTimer.Interval = config.CheckIntervalSeconds * 1000;
            monitorTimer.Tick += MonitorTimer_Tick;
            monitorTimer.Start();
            
            if (!config.IsEnabled)
            {
                UpdateStatusUI(Color.Gray, "Monitoring is Disabled.");
                SetTrayState("Fatal", "KickFox: 監控已停用");
            }
            else
            {
                UpdateStatusUI(Color.LimeGreen, "Monitoring started.");
                Log("Monitoring started.");
            }
        }

        private void MonitorTimer_Tick(object sender, EventArgs e)
        {
            if (!config.IsEnabled) return;
            if (isChecking) return;

            if (isSleeping)
            {
                if (DateTime.Now >= sleepUntil)
                {
                    isSleeping = false;
                    consecutiveFailures = 0;
                    Log("Wake up from sleep mode. Resuming checks.");
                }
                else
                {
                    UpdateStatusUI(Color.Red, string.Format("Sleeping until {0} due to max retries...", sleepUntil.ToShortTimeString()));
                    return;
                }
            }

            isChecking = true;
            ThreadPool.QueueUserWorkItem(CheckConnection);
        }

        private void CheckConnection(object state)
        {
            try
            {
                bool isOnline = false;
                long pingTime = 0;

                foreach (var target in config.PingTargets)
                {
                    try
                    {
                        using (Ping pingSender = new Ping())
                        {
                            PingReply reply = pingSender.Send(target.Trim(), 2000);
                            if (reply.Status == IPStatus.Success)
                            {
                                isOnline = true;
                                pingTime = reply.RoundtripTime;
                                break;
                            }
                        }
                    }
                    catch { }
                }

                if (isOnline)
                {
                    if (consecutiveFailures > 0)
                    {
                        consecutiveFailures = 0;
                        Log("Network connection restored successfully.");
                    }
                    UpdateStatusUI(Color.LimeGreen, "Network is online");
                    this.Invoke(new Action(() => {
                        SetTrayState("Normal", string.Format("KickFox: 連線正常 (Ping: {0}ms)", pingTime));
                    }));
                }
                else
                {
                    consecutiveFailures++;
                    
                    string failMsg = string.Format("Ping failed. Attempt {0}", consecutiveFailures);
                    if (config.UseMaxRetries) failMsg += string.Format("/{0}", config.MaxRetries);
                    Log(failMsg);

                    if (config.UseMaxRetries && consecutiveFailures > config.MaxRetries)
                    {
                        isSleeping = true;
                        sleepUntil = DateTime.Now.AddMinutes(30);
                        UpdateStatusUI(Color.Red, "Max retries reached. Sleeping for 30 mins.");
                        Log("Max retries reached. Entering sleep mode for 30 mins to prevent infinite loops.");
                        
                        this.Invoke(new Action(() => {
                            SetTrayState("Fatal", "KickFox: 重啟失敗，請檢查實體數據機或線路");
                        }));
                    }
                    else
                    {
                        UpdateStatusUI(Color.Gold, "Restarting adapter...");
                        this.Invoke(new Action(() => {
                            SetTrayState("Kick", "KickFox: 偵測到斷線，正在踢醒網卡...");
                        }));
                        RestartWifi();
                    }
                }
            }
            finally
            {
                isChecking = false;
            }
        }

        private void RestartWifi()
        {
            Log("Restarting Wi-Fi adapters...");
            string psScript = @"Get-NetAdapter | Where-Object { $_.PhysicalMediaType -match '802.11' } | ForEach-Object { Disable-NetAdapter -Name $_.Name -Confirm:$false; Start-Sleep 5; Enable-NetAdapter -Name $_.Name -Confirm:$false }";
            
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "powershell.exe";
            psi.Arguments = string.Format("-WindowStyle Hidden -Command \"{0}\"", psScript);
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            
            try
            {
                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit();
                }
                UpdateStatusUI(Color.Gold, "Wi-Fi restarted. Waiting 20s...");
                Log("Wi-Fi restarted. Waiting 20s for reconnection...");
                Thread.Sleep(20000); 
            }
            catch (Exception ex)
            {
                Log("Failed to execute restart command: " + ex.Message);
            }
        }

        private void MonitorForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                this.ShowInTaskbar = false;
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.Hide();
                this.ShowInTaskbar = false;
            }

            // Window handle now exists, so background results can be marshalled back safely.
            CheckForUpdates(false);
        }

        private void OnSettings(object sender, EventArgs e)
        {
            LoadConfigToUI();
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
            this.Activate();
        }

        private void OnExit(object sender, EventArgs e)
        {
            trayIcon.Visible = false;
            Application.Exit();
        }
    }
}
