using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Diagnostics;
using Microsoft.Win32;

internal static class Installer
{
    private const string AppName = "Rintas";

    [STAThread]
    private static int Main(string[] args)
    {
        bool testMode = args.Length > 0 && args[0] == "--test-install";
        if (testMode)
        {
            try
            {
                string target = Environment.GetEnvironmentVariable("FILE_SHARE_INSTALL_DIR");
                if (String.IsNullOrWhiteSpace(target)) throw new InvalidOperationException("FILE_SHARE_INSTALL_DIR diperlukan untuk tes.");
                Install(Path.GetFullPath(target), Environment.GetEnvironmentVariable("FILE_SHARE_TEST_DOMAIN") ?? "", false);
                File.WriteAllText(Path.Combine(target, "install-test-ok.txt"), "OK");
                return 0;
            }
            catch { return 1; }
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new SetupForm());
        return 0;
    }

    private sealed class SetupForm : Form
    {
        private readonly TextBox domainBox;
        private readonly Button installButton;

        internal SetupForm()
        {
            Text = AppName + " - Instalasi";
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Width = 570;
            Height = 330;
            MinimumSize = new System.Drawing.Size(570, 330);
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Font = new System.Drawing.Font("Segoe UI", 10);

            var title = new Label { Text = "Rintas - bagikan file dari laptop", Left = 28, Top = 24, Width = 490, Height = 30, Font = new System.Drawing.Font("Segoe UI", 15, System.Drawing.FontStyle.Bold) };
            var intro = new Label { Text = "Rintas berjalan di system tray. Klik kanan file untuk menyalin tautan tanpa terminal.", Left = 28, Top = 65, Width = 500, Height = 42 };
            var domainLabel = new Label { Text = "Domain berbagi (opsional)", Left = 28, Top = 118, Width = 500, Height = 26 };
            domainBox = new TextBox { Left = 28, Top = 148, Width = 495, Height = 28 };
            var help = new Label { Text = "Kosongkan untuk tautan sementara. Domain tetap memerlukan Cloudflare Named Tunnel yang diarahkan ke 127.0.0.1:8765.", Left = 28, Top = 184, Width = 495, Height = 55, ForeColor = System.Drawing.Color.DimGray };
            installButton = new Button { Text = "Pasang aplikasi", Left = 365, Top = 245, Width = 158, Height = 34 };
            var cancelButton = new Button { Text = "Batal", Left = 268, Top = 245, Width = 88, Height = 34 };
            installButton.Click += OnInstall;
            cancelButton.Click += (sender, args) => Close();
            Controls.AddRange(new Control[] { title, intro, domainLabel, domainBox, help, installButton, cancelButton });
            AcceptButton = installButton;
            CancelButton = cancelButton;

            string existing = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FileShareTunnel", "config.json");
            if (File.Exists(existing))
            {
                Match match = Regex.Match(File.ReadAllText(existing), "\"publicBaseUrl\"\\s*:\\s*\"(https://[^\"]*)\"");
                if (match.Success) domainBox.Text = match.Groups[1].Value;
            }
        }

        private void OnInstall(object sender, EventArgs args)
        {
            string domain = domainBox.Text.Trim().TrimEnd('/');
            if (domain.Length > 0 && !ValidDomain(domain))
            {
                MessageBox.Show("Masukkan alamat HTTPS tanpa path, misalnya https://bagi.domainanda.com.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (domain.Length > 0) domain = new Uri(domain).GetLeftPart(UriPartial.Authority);
            installButton.Enabled = false;
            Cursor = Cursors.WaitCursor;
            try
            {
                string target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FileShareTunnel");
                Install(target, domain, true);
                MessageBox.Show("Rintas terpasang dan berjalan di system tray. Klik kanan file lalu pilih 'Bagikan dengan Rintas'.\n\nDi Windows 11, menu ini masih berada di 'Tampilkan opsi lainnya' pada installer EXE.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception error)
            {
                MessageBox.Show("Pemasangan gagal: " + error.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { Cursor = Cursors.Default; installButton.Enabled = true; }
        }
    }

    private static bool ValidDomain(string value)
    {
        Uri uri;
        return Uri.TryCreate(value, UriKind.Absolute, out uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && uri.Host.Contains(".")
            && uri.AbsolutePath == "/"
            && String.IsNullOrEmpty(uri.Query)
            && String.IsNullOrEmpty(uri.Fragment)
            && uri.UserInfo.Length == 0
            && value.IndexOf('\\') < 0;
    }

    private static void Install(string target, string domain, bool register)
    {
        target = Path.GetFullPath(target);
        Directory.CreateDirectory(target);
        ExtractPayload(target);
        string configPath = Path.Combine(target, "config.json");
        int expireMinutes = 1440;
        if (File.Exists(configPath))
        {
            Match expiry = Regex.Match(File.ReadAllText(configPath), "\"defaultExpireMinutes\"\\s*:\\s*(\\d+)");
            int parsed;
            if (expiry.Success && Int32.TryParse(expiry.Groups[1].Value, out parsed) && parsed >= 1 && parsed <= 10080)
                expireMinutes = parsed;
        }
        File.WriteAllText(configPath, "{\"publicBaseUrl\":\"" + domain + "\",\"defaultExpireMinutes\":" + expireMinutes + "}");
        if (register)
        {
            RegisterApp(target);
            Process.Start(new ProcessStartInfo(Path.Combine(target, "Rintas.exe")) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
        }
    }

    private static void ExtractPayload(string target)
    {
        using (Stream resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload"))
        {
            if (resource == null) throw new InvalidOperationException("Paket aplikasi tidak ditemukan di installer.");
            using (var archive = new ZipArchive(resource, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string destination = Path.GetFullPath(Path.Combine(target, entry.FullName));
                    if (!destination.StartsWith(target.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Isi paket memiliki jalur yang tidak aman.");
                    if (String.IsNullOrEmpty(entry.Name)) continue;
                    if (entry.Name == "config.json" && File.Exists(destination)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    using (Stream input = entry.Open())
                    using (Stream output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None))
                        input.CopyTo(output);
                }
            }
        }
    }

    private static void RegisterApp(string target)
    {
        string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
        string app = Path.Combine(target, "Rintas.exe");
        string command = "\"" + app + "\" --share \"%1\"";
        using (RegistryKey menu = Registry.CurrentUser.CreateSubKey(@"Software\Classes\*\shell\FileShareTunnel"))
        {
            menu.SetValue("", "Bagikan dengan Rintas");
            menu.SetValue("Icon", Path.Combine(target, "Assets", "RintasLight.ico"));
        }
        using (RegistryKey menuCommand = Registry.CurrentUser.CreateSubKey(@"Software\Classes\*\shell\FileShareTunnel\command")) menuCommand.SetValue("", command);
        using (RegistryKey run = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) run.SetValue("Rintas", "\"" + app + "\"");
        using (RegistryKey uninstall = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\FileShareTunnel"))
        {
            uninstall.SetValue("DisplayName", AppName);
            uninstall.SetValue("DisplayVersion", "1.0.9.0");
            uninstall.SetValue("Publisher", "mfarzz");
            uninstall.SetValue("InstallLocation", target);
            uninstall.SetValue("UninstallString", "\"" + powershell + "\" -NoProfile -ExecutionPolicy Bypass -File \"" + Path.Combine(target, "uninstall-app.ps1") + "\"");
            uninstall.SetValue("NoModify", 1, RegistryValueKind.DWord);
            uninstall.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }
    }
}
