using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using Microsoft.Win32;

internal static class RintasApp
{
    internal static readonly string AppDir = AppDomain.CurrentDomain.BaseDirectory;
    internal static readonly string DataDir = Environment.GetEnvironmentVariable("RINTAS_DATA_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FileShareTunnel");
    private const string ControlUrl = "http://127.0.0.1:8766";
    private static readonly Icon AppIcon = LoadAppIcon();
    private static readonly Icon DarkTrayIcon = LoadThemeIcon("RintasDark.ico");
    private static readonly Icon LightTrayIcon = LoadThemeIcon("RintasLight.ico");

    private static Icon LoadAppIcon()
    {
        try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application; }
        catch { return SystemIcons.Application; }
    }

    private static Icon LoadThemeIcon(string name)
    {
        try { return new Icon(Path.Combine(AppDir, "Assets", name)); }
        catch { return AppIcon; }
    }

    private static Icon TrayIconForTheme()
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                return Convert.ToInt32(key == null ? 1 : key.GetValue("SystemUsesLightTheme", 1)) == 0 ? DarkTrayIcon : LightTrayIcon;
        }
        catch { return LightTrayIcon; }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr window);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint format, IntPtr data);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr memory);

    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (args.Length > 0 && args[0] == "--share")
        {
            ShareFile(args.Length > 1 ? args[1] : "");
            return 0;
        }
        bool created;
        using (var mutex = new Mutex(true, "Local\\RintasBackgroundApp", out created))
        {
            if (!created)
            {
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    try
                    {
                        using (var show = EventWaitHandle.OpenExisting("Local\\RintasShowDashboard")) show.Set();
                        break;
                    }
                    catch (WaitHandleCannotBeOpenedException) { Thread.Sleep(100); }
                }
                return 0;
            }
            using (var show = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\RintasShowDashboard"))
                Application.Run(new TrayContext(show));
        }
        return 0;
    }

    internal static void EnsureDaemon()
    {
        string response;
        int status;
        if (Request("GET", "/status", null, out response, out status) && status == 200) return;
        string node = Path.Combine(AppDir, "node.exe");
        if (!File.Exists(node)) node = "node.exe";
        string script = Path.Combine(AppDir, "share.js");
        var info = new ProcessStartInfo(node, "\"" + script + "\"")
        {
            WorkingDirectory = AppDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        info.EnvironmentVariables["RINTAS_DATA_DIR"] = DataDir;
        Process.Start(info);
    }

    internal static bool Request(string method, string route, string body, out string response, out int status)
    {
        response = "";
        status = 0;
        string tokenPath = Path.Combine(DataDir, "control.token");
        if (!File.Exists(tokenPath)) return false;
        try
        {
            var request = (HttpWebRequest)WebRequest.Create(ControlUrl + route);
            request.Method = method;
            request.Timeout = 2500;
            request.ReadWriteTimeout = 2500;
            request.Headers.Add("X-Rintas-Control", File.ReadAllText(tokenPath).Trim());
            if (method == "POST")
            {
                byte[] bytes = Encoding.UTF8.GetBytes(body ?? "");
                request.ContentType = "text/plain; charset=utf-8";
                request.ContentLength = bytes.Length;
                using (Stream stream = request.GetRequestStream()) stream.Write(bytes, 0, bytes.Length);
            }
            using (var result = (HttpWebResponse)request.GetResponse())
            using (var reader = new StreamReader(result.GetResponseStream()))
            {
                status = (int)result.StatusCode;
                response = reader.ReadToEnd();
            }
            return true;
        }
        catch (WebException error)
        {
            var webResponse = error.Response as HttpWebResponse;
            if (webResponse != null)
            {
                status = (int)webResponse.StatusCode;
                using (webResponse)
                using (var reader = new StreamReader(webResponse.GetResponseStream())) response = reader.ReadToEnd();
                return true;
            }
            return false;
        }
        catch { return false; }
    }

    private static void ShareFile(string file)
    {
        if (!File.Exists(file))
        {
            MessageBox.Show("File tidak ditemukan.", "Rintas", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        try { EnsureDaemon(); }
        catch (Exception error)
        {
            MessageBox.Show("Rintas gagal dimulai: " + error.Message, "Rintas", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        string response = "";
        int status = 0;
        for (int attempt = 0; attempt < 60; attempt++)
        {
            if (Request("POST", "/share", Path.GetFullPath(file), out response, out status) && status == 200)
            {
                if (!CopyLink(response.Trim()))
                {
                    MessageBox.Show("Tautan dibuat, tetapi clipboard sedang tidak tersedia. Salin tautan ini secara manual:\n\n" + response.Trim(), "Rintas", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                using (var notification = new NotifyIcon { Icon = AppIcon, Visible = true, Text = "Rintas" })
                {
                    notification.ShowBalloonTip(3000, "Tautan disalin", Path.GetFileName(file), ToolTipIcon.Info);
                    Thread.Sleep(3500);
                }
                return;
            }
            if (status == 400 || status == 403 || (status == 503 && response.StartsWith("Domain"))) break;
            Thread.Sleep(1000);
        }
        MessageBox.Show("Tautan belum dapat dibuat. " + (response.Length > 0 ? response : "Periksa koneksi Cloudflare Tunnel dan log Rintas."), "Rintas", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private static bool CopyLink(string link)
    {
        // The context-menu process can race with Explorer or clipboard managers.
        // Win32 clipboard data remains available after this short-lived process exits.
        byte[] bytes = Encoding.Unicode.GetBytes(link + "\0");
        const uint GmemMoveable = 0x0002;
        const uint CfUnicodeText = 13;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            if (!OpenClipboard(IntPtr.Zero))
            {
                Thread.Sleep(150);
                continue;
            }
            IntPtr memory = IntPtr.Zero;
            bool copied = false;
            try
            {
                memory = GlobalAlloc(GmemMoveable, (UIntPtr)bytes.Length);
                if (memory == IntPtr.Zero) return false;
                IntPtr destination = GlobalLock(memory);
                if (destination == IntPtr.Zero) return false;
                try { Marshal.Copy(bytes, 0, destination, bytes.Length); }
                finally { GlobalUnlock(memory); }
                if (!EmptyClipboard()) return false;
                if (SetClipboardData(CfUnicodeText, memory) == IntPtr.Zero) return false;
                memory = IntPtr.Zero; // Clipboard owns the memory now.
                copied = true;
            }
            finally
            {
                if (memory != IntPtr.Zero) GlobalFree(memory);
                CloseClipboard();
            }
            if (copied)
            {
                Thread.Sleep(150);
                try { if (Clipboard.GetText() == link) return true; }
                catch (ExternalException) { }
            }
        }
        return false;
    }

    private sealed class TrayContext : ApplicationContext
    {
        private readonly NotifyIcon icon;
        private readonly System.Windows.Forms.Timer timer;
        private readonly System.Windows.Forms.Timer activationTimer;
        private readonly EventWaitHandle showEvent;
        private Form dashboard;
        private Label dashboardStatus;
        private Label dashboardAddress;
        private Label dashboardCount;
        private DataGridView linksGrid;
        private readonly ToolStripMenuItem linksMenu;

        internal TrayContext(EventWaitHandle showEvent)
        {
            this.showEvent = showEvent;
            Directory.CreateDirectory(DataDir);
            var menu = new ContextMenuStrip();
            menu.Items.Add("Buka Rintas", null, (sender, args) => ShowDashboard());
            menu.Items.Add("Bagikan file...", null, (sender, args) => PickFile());
            linksMenu = new ToolStripMenuItem("Tautan aktif");
            linksMenu.DropDownOpening += (sender, args) => FillLinksMenu();
            menu.Items.Add(linksMenu);
            menu.Items.Add("Rintas aktif", null, (sender, args) => ShowStatus());
            menu.Items.Add("Pengaturan domain dan masa berlaku", null, (sender, args) => ShowSettings());
            menu.Items.Add("Hentikan semua tautan", null, (sender, args) => ClearShares());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Keluar", null, (sender, args) => Exit());
            icon = new NotifyIcon { Icon = TrayIconForTheme(), Text = "Rintas memulai...", Visible = true, ContextMenuStrip = menu };
            icon.MouseClick += (sender, args) => { if (args.Button == MouseButtons.Left) ShowDashboard(); };
            timer = new System.Windows.Forms.Timer { Interval = 3000 };
            timer.Tick += (sender, args) => RefreshStatus();
            timer.Start();
            activationTimer = new System.Windows.Forms.Timer { Interval = 250 };
            activationTimer.Tick += (sender, args) => { if (showEvent.WaitOne(0)) ShowDashboard(); };
            activationTimer.Start();
            try { EnsureDaemon(); }
            catch (Exception error) { icon.ShowBalloonTip(5000, "Rintas gagal dimulai", error.Message, ToolTipIcon.Error); }
        }

        private void RefreshStatus()
        {
            Icon themed = TrayIconForTheme();
            if (icon.Icon != themed) icon.Icon = themed;
            string response;
            int status;
            if (Request("GET", "/status", null, out response, out status) && status == 200)
                icon.Text = response.Contains("\"ready\":true") ? "Rintas siap berbagi" :
                    response.Contains("Domain belum") ? "Rintas: domain belum terhubung" : "Rintas menghubungkan tunnel...";
            else icon.Text = "Rintas tidak tersambung";
            if (dashboardStatus != null && !dashboardStatus.IsDisposed) dashboardStatus.Text = icon.Text;
            if (dashboardAddress != null && !dashboardAddress.IsDisposed && status == 200)
            {
                Match address = Regex.Match(response, "\"baseUrl\"\\s*:\\s*\"([^\"]*)\"");
                dashboardAddress.Text = address.Success && address.Groups[1].Value.Length > 0
                    ? "Alamat: " + address.Groups[1].Value : "Alamat publik sedang disiapkan";
            }
            if (dashboard != null && !dashboard.IsDisposed) RefreshLinks();
        }

        private void ShowDashboard()
        {
            if (dashboard != null && !dashboard.IsDisposed)
            {
                dashboard.Show();
                dashboard.WindowState = FormWindowState.Normal;
                dashboard.BringToFront();
                dashboard.Activate();
                return;
            }
            var form = new Form
            {
                Text = "Rintas", Icon = AppIcon, ClientSize = new Size(820, 520),
                MinimumSize = new Size(700, 500), StartPosition = FormStartPosition.CenterScreen,
                Font = new Font("Segoe UI", 10), BackColor = Color.FromArgb(244, 244, 246)
            };
            var header = new Panel { Dock = DockStyle.Top, Height = 80, BackColor = Color.FromArgb(20, 20, 30) };
            var logo = new PictureBox { Left = 24, Top = 16, Width = 46, Height = 46, SizeMode = PictureBoxSizeMode.Zoom };
            string mark = Path.Combine(AppDir, "Assets", "BrandMarkDark.png");
            if (File.Exists(mark))
            {
                using (var bitmap = Image.FromFile(mark)) logo.Image = new Bitmap(bitmap);
            }
            var title = new Label { Text = "RINTAS", Left = 84, Top = 20, Width = 210, Height = 35, ForeColor = Color.White, Font = new Font("Segoe UI", 19, FontStyle.Bold) };
            dashboardStatus = new Label
            {
                Text = icon.Text, Left = 470, Top = 27, Width = 320, Height = 28,
                ForeColor = Color.FromArgb(79, 209, 197), TextAlign = ContentAlignment.MiddleRight,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            header.Controls.AddRange(new Control[] { logo, title, dashboardStatus });
            dashboardAddress = new Label
            {
                Text = "Alamat publik sedang disiapkan", Left = 24, Top = 94, Width = 760, Height = 25,
                ForeColor = Color.FromArgb(89, 93, 105), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            var section = new Label { Text = "TAUTAN AKTIF", Left = 24, Top = 130, Width = 245, Height = 28, Font = new Font("Segoe UI", 12, FontStyle.Bold) };
            dashboardCount = new Label { Text = "0 tautan", Left = 265, Top = 133, Width = 130, Height = 25, ForeColor = Color.FromArgb(89, 93, 105) };
            var share = DashboardButton("+ Bagikan file", 650, 124, 146, 36, true);
            share.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            share.Click += (sender, args) => PickFile();

            linksGrid = new DataGridView
            {
                Left = 24, Top = 168, Width = 772, Height = 245,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
                ReadOnly = true, MultiSelect = false, RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White, BorderStyle = BorderStyle.FixedSingle,
                GridColor = Color.FromArgb(200, 203, 210), EnableHeadersVisualStyles = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeight = 38, RowTemplate = { Height = 42 }
            };
            linksGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(20, 20, 30);
            linksGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            linksGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            linksGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 247, 244);
            linksGrid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(20, 20, 30);
            linksGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "File", HeaderText = "FILE", FillWeight = 33 });
            linksGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Url", HeaderText = "TAUTAN", FillWeight = 43 });
            linksGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Expires", HeaderText = "BERLAKU HINGGA", FillWeight = 24 });

            var copy = DashboardButton("Salin tautan", 24, 428, 142, 36, false);
            var revoke = DashboardButton("Cabut tautan", 178, 428, 142, 36, false);
            var clear = DashboardButton("Cabut semua", 332, 428, 125, 36, false);
            var settings = DashboardButton("Pengaturan", 664, 428, 132, 36, false);
            foreach (Button button in new[] { copy, revoke, clear, settings }) button.Anchor = AnchorStyles.Bottom | (button == settings ? AnchorStyles.Right : AnchorStyles.Left);
            copy.Click += (sender, args) => CopySelectedLink();
            revoke.Click += (sender, args) => RevokeSelectedLink();
            clear.Click += (sender, args) => ClearShares();
            settings.Click += (sender, args) => ShowSettings();
            var footer = new Label
            {
                Text = "Tautan aktif selama server berjalan dan belum melewati masa berlaku.",
                Left = 24, Top = 481, Width = 760, Height = 25, ForeColor = Color.FromArgb(89, 93, 105),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            form.Controls.AddRange(new Control[] { header, dashboardAddress, section, dashboardCount, share, linksGrid, copy, revoke, clear, settings, footer });
            form.FormClosed += (sender, args) =>
            {
                if (logo.Image != null) logo.Image.Dispose();
                dashboard = null; dashboardStatus = null; dashboardAddress = null; dashboardCount = null; linksGrid = null;
            };
            dashboard = form;
            RefreshStatus();
            form.Show();
            form.Activate();
        }

        private static Button DashboardButton(string label, int left, int top, int width, int height, bool accent)
        {
            var button = new Button
            {
                Text = label, Left = left, Top = top, Width = width, Height = height,
                FlatStyle = FlatStyle.Flat, BackColor = accent ? Color.FromArgb(79, 209, 197) : Color.White,
                ForeColor = Color.FromArgb(20, 20, 30), Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(20, 20, 30);
            button.FlatAppearance.BorderSize = 1;
            return button;
        }

        private List<Dictionary<string, object>> ReadShares()
        {
            string response;
            int status;
            if (!Request("GET", "/shares", null, out response, out status) || status != 200) return new List<Dictionary<string, object>>();
            try { return new JavaScriptSerializer().Deserialize<List<Dictionary<string, object>>>(response); }
            catch { return new List<Dictionary<string, object>>(); }
        }

        private static string ShareField(Dictionary<string, object> share, string name)
        {
            object value;
            return share.TryGetValue(name, out value) ? Convert.ToString(value) : "";
        }

        private void RefreshLinks()
        {
            if (linksGrid == null || linksGrid.IsDisposed) return;
            string selected = SelectedLinkKey();
            var shares = ReadShares();
            linksGrid.Rows.Clear();
            foreach (var share in shares)
            {
                string expiry = ShareField(share, "expiresAt");
                DateTimeOffset date;
                if (DateTimeOffset.TryParse(expiry, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                    expiry = date.ToLocalTime().ToString("dd MMM yyyy HH:mm", CultureInfo.GetCultureInfo("id-ID"));
                int row = linksGrid.Rows.Add(ShareField(share, "name"), ShareField(share, "url"), expiry);
                linksGrid.Rows[row].Tag = ShareField(share, "key");
                if (linksGrid.Rows[row].Tag as string == selected) linksGrid.Rows[row].Selected = true;
            }
            if (linksGrid.SelectedRows.Count == 0 && linksGrid.Rows.Count > 0) linksGrid.Rows[0].Selected = true;
            dashboardCount.Text = shares.Count + " tautan";
        }

        private string SelectedLinkKey()
        {
            return linksGrid != null && linksGrid.SelectedRows.Count > 0 ? linksGrid.SelectedRows[0].Tag as string : null;
        }

        private void CopySelectedLink()
        {
            if (linksGrid == null || linksGrid.SelectedRows.Count == 0) return;
            string url = Convert.ToString(linksGrid.SelectedRows[0].Cells["Url"].Value);
            if (CopyLink(url)) icon.ShowBalloonTip(3000, "Tautan disalin", url, ToolTipIcon.Info);
            else MessageBox.Show("Clipboard sedang tidak tersedia. Salin tautan dari daftar.", "Rintas", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void RevokeSelectedLink()
        {
            string key = SelectedLinkKey();
            if (String.IsNullOrEmpty(key)) return;
            string response;
            int status;
            if (Request("POST", "/revoke", key, out response, out status) && status == 200)
            {
                RefreshLinks();
                icon.ShowBalloonTip(3000, "Tautan dicabut", "Tautan ini tidak dapat diakses lagi.", ToolTipIcon.Info);
            }
            else MessageBox.Show("Tautan tidak dapat dicabut. Periksa status server.", "Rintas", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void FillLinksMenu()
        {
            linksMenu.DropDownItems.Clear();
            var shares = ReadShares();
            if (shares.Count == 0) linksMenu.DropDownItems.Add(new ToolStripMenuItem("Belum ada tautan aktif") { Enabled = false });
            else
            {
                for (int i = 0; i < Math.Min(shares.Count, 8); i++)
                {
                    string name = ShareField(shares[i], "name");
                    if (name.Length > 38) name = name.Substring(0, 35) + "...";
                    string url = ShareField(shares[i], "url");
                    var item = new ToolStripMenuItem(name.Replace("&", "&&"));
                    item.Click += (sender, args) =>
                    {
                        if (CopyLink(url)) icon.ShowBalloonTip(3000, "Tautan disalin", name, ToolTipIcon.Info);
                    };
                    linksMenu.DropDownItems.Add(item);
                }
            }
            linksMenu.DropDownItems.Add(new ToolStripSeparator());
            linksMenu.DropDownItems.Add("Lihat semua tautan...", null, (sender, args) => ShowDashboard());
        }

        private void ShowStatus()
        {
            string response;
            int status;
            Request("GET", "/status", null, out response, out status);
            MessageBox.Show(status == 200 ? response : "Server tidak tersambung. Periksa rintas.log di folder aplikasi.", "Status Rintas", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void PickFile()
        {
            using (var picker = new OpenFileDialog { Title = "Pilih file untuk dibagikan", CheckFileExists = true })
            {
                if (picker.ShowDialog() == DialogResult.OK)
                {
                    ShareFile(picker.FileName);
                    RefreshLinks();
                }
            }
        }

        private void ClearShares()
        {
            string response;
            int status;
            if (Request("POST", "/clear", "", out response, out status) && status == 200)
            {
                RefreshLinks();
                icon.ShowBalloonTip(3000, "Semua tautan dihentikan", response + " tautan dicabut.", ToolTipIcon.Info);
            }
        }

        private void ShowSettings()
        {
            using (var form = new DomainForm())
            {
                if (form.ShowDialog() != DialogResult.OK) return;
                string response;
                int status;
                Request("POST", "/shutdown", "", out response, out status);
                Thread.Sleep(600);
                try { EnsureDaemon(); }
                catch (Exception error) { MessageBox.Show(error.Message, "Rintas", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                RefreshStatus();
            }
        }

        private void Exit()
        {
            timer.Stop();
            activationTimer.Stop();
            string response;
            int status;
            Request("POST", "/shutdown", "", out response, out status);
            icon.Visible = false;
            icon.Dispose();
            ExitThread();
        }
    }

    private sealed class DomainForm : Form
    {
        private readonly TextBox box;
        private readonly NumericUpDown expiry;
        internal DomainForm()
        {
            Text = "Pengaturan Rintas";
            Icon = AppIcon;
            ClientSize = new Size(500, 345);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            Font = new Font("Segoe UI", 10);
            BackColor = Color.FromArgb(244, 244, 246);
            var header = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Color.FromArgb(20, 20, 30) };
            header.Controls.Add(new Label { Text = "PENGATURAN RINTAS", Left = 20, Top = 16, Width = 450, Height = 30, ForeColor = Color.White, Font = new Font("Segoe UI", 15, FontStyle.Bold) });
            var label = new Label { Text = "DOMAIN HTTPS (KOSONG = TAUTAN SEMENTARA)", Left = 20, Top = 80, Width = 455, Height = 25, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            box = new TextBox { Left = 20, Top = 110, Width = 455, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White };
            var note = new Label { Text = "Gunakan hostname Cloudflare Tunnel yang sudah aktif, misalnya https://share.domainanda.com. Rute menuju http://127.0.0.1:8765.", Left = 20, Top = 145, Width = 455, Height = 45, ForeColor = Color.FromArgb(89, 93, 105) };
            var expiryLabel = new Label { Text = "Masa berlaku tautan baru (menit)", Left = 20, Top = 205, Width = 280, Height = 25 };
            expiry = new NumericUpDown { Left = 320, Top = 202, Width = 155, Minimum = 1, Maximum = 10080, Value = 1440, BorderStyle = BorderStyle.FixedSingle };
            var restartNote = new Label { Text = "Menyimpan pengaturan akan mencabut tautan yang sedang aktif.", Left = 20, Top = 246, Width = 455, Height = 30, ForeColor = Color.FromArgb(89, 93, 105) };
            var guide = new Button { Text = "Panduan domain", Left = 20, Top = 291, Width = 155, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = Color.White };
            guide.FlatAppearance.BorderColor = Color.FromArgb(20, 20, 30);
            guide.Click += (sender, args) => { using (var form = new DomainGuideForm()) form.ShowDialog(this); };
            var save = new Button { Text = "Simpan", Left = 355, Top = 291, Width = 120, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(79, 209, 197) };
            save.FlatAppearance.BorderColor = Color.FromArgb(20, 20, 30);
            save.Click += Save;
            Controls.AddRange(new Control[] { header, label, box, note, expiryLabel, expiry, restartNote, guide, save });
            AcceptButton = save;
            string config = Path.Combine(DataDir, "config.json");
            if (File.Exists(config))
            {
                Match match = Regex.Match(File.ReadAllText(config), "\"publicBaseUrl\"\\s*:\\s*\"(https://[^\"]*)\"");
                if (match.Success) box.Text = match.Groups[1].Value;
                Match expiryMatch = Regex.Match(File.ReadAllText(config), "\"defaultExpireMinutes\"\\s*:\\s*(\\d+)");
                decimal minutes;
                if (expiryMatch.Success && decimal.TryParse(expiryMatch.Groups[1].Value, out minutes) && minutes >= expiry.Minimum && minutes <= expiry.Maximum)
                    expiry.Value = minutes;
            }
        }

        private void Save(object sender, EventArgs args)
        {
            string value = box.Text.Trim().TrimEnd('/');
            if (value.Length > 0)
            {
                Uri uri;
                if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || uri.Scheme != "https" || !uri.Host.Contains(".") || uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.UserInfo.Length > 0 || value.IndexOf('\\') >= 0)
                {
                    MessageBox.Show("Masukkan domain HTTPS tanpa path.", "Rintas", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                value = uri.GetLeftPart(UriPartial.Authority);
            }
            Directory.CreateDirectory(DataDir);
            File.WriteAllText(Path.Combine(DataDir, "config.json"), "{\"publicBaseUrl\":\"" + value + "\",\"defaultExpireMinutes\":" + Convert.ToInt32(expiry.Value) + "}");
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    private sealed class DomainGuideForm : Form
    {
        internal DomainGuideForm()
        {
            Text = "Panduan domain sendiri - Rintas";
            Icon = AppIcon;
            Width = 630;
            Height = 575;
            MinimumSize = new Size(560, 480);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 10);
            BackColor = Color.FromArgb(244, 244, 246);

            var title = new Label
            {
                Text = "Hubungkan domain sendiri",
                Left = 20, Top = 18, Width = 570, Height = 38,
                Font = new Font("Segoe UI", 17, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            var steps = new TextBox
            {
                Left = 20, Top = 68, Width = 575, Height = 410,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle,
                Text =
                    "Domain di pengaturan Rintas hanya menentukan alamat tautan. Agar tautan bekerja, Cloudflare Tunnel juga harus diarahkan ke laptop ini.\r\n\r\n" +
                    "1. Siapkan domain di akun Cloudflare. Pilih subdomain khusus, misalnya share.domainanda.com, agar situs pada domain utama tetap berjalan.\r\n\r\n" +
                    "2. Di Cloudflare Dashboard, buka Networking > Tunnels. Buat tunnel, pilih konektor Windows, lalu pasang cloudflared sebagai layanan pada laptop yang menjalankan Rintas. Simpan token tunnel secara privat.\r\n\r\n" +
                    "3. Pada tunnel tersebut, tambahkan Published application. Isi hostname share.domainanda.com dan Service URL http://127.0.0.1:8765. Jangan tambahkan path pada Service URL.\r\n\r\n" +
                    "4. Pastikan tunnel berstatus tersambung. Kembali ke Pengaturan Rintas, isi https://share.domainanda.com, termasuk dua garis miring setelah https:, lalu klik Simpan.\r\n\r\n" +
                    "5. Buka https://share.domainanda.com/ di browser. Halaman Rintas bertuliskan 'Tautan tidak ditemukan' berarti rutenya sudah mencapai aplikasi. Buat tautan berbagi baru dan uji dari perangkat lain.\r\n\r\n" +
                    "Jika status menunjukkan domain belum terhubung, periksa DNS subdomain, status tunnel, dan Service URL. DNS baru mungkin perlu waktu untuk terbaca oleh jaringan Anda.\r\n\r\n" +
                    "Mengubah pengaturan Rintas mencabut tautan lama. Jika belum memakai domain sendiri, kosongkan kolom domain untuk memakai alamat sementara trycloudflare.com.\r\n\r\n" +
                    "File tetap berada di laptop. Laptop, Rintas, dan tunnel harus menyala selama tautan digunakan."
            };
            var cloudflare = new Button
            {
                Text = "Buka Cloudflare",
                Left = 20, Top = 488, Width = 155, Height = 34,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left, FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(79, 209, 197)
            };
            cloudflare.Click += (sender, args) =>
            {
                try { Process.Start(new ProcessStartInfo("https://dash.cloudflare.com/") { UseShellExecute = true }); }
                catch (Exception error) { MessageBox.Show(error.Message, "Rintas", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };
            var close = new Button
            {
                Text = "Tutup", Left = 495, Top = 488, Width = 100, Height = 34,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right, FlatStyle = FlatStyle.Flat,
                BackColor = Color.White
            };
            close.Click += (sender, args) => Close();
            Controls.AddRange(new Control[] { title, steps, cloudflare, close });
            CancelButton = close;
        }
    }
}
