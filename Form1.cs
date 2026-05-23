using System;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using RNPerf;

namespace RNPerf;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}

static class Theme
{
    public static readonly Color BG = Color.FromArgb(15, 15, 20);
    public static readonly Color Panel = Color.FromArgb(25, 25, 35);
    public static readonly Color Card = Color.FromArgb(32, 32, 45);
    public static readonly Color CardHi = Color.FromArgb(40, 33, 65);
    public static readonly Color Purple = Color.FromArgb(108, 60, 210);
    public static readonly Color Purple2 = Color.FromArgb(140, 90, 255);
    public static readonly Color PurpleHi = Color.FromArgb(160, 110, 255);
    public static readonly Color FG = Color.FromArgb(240, 240, 255);
    public static readonly Color FGDim = Color.FromArgb(130, 130, 160);
    public static readonly Color Green = Color.FromArgb(80, 200, 120);
    public static readonly Color Red = Color.FromArgb(220, 70, 70);
    public static readonly Color Border = Color.FromArgb(55, 55, 75);
}

// ═══════════════════════════════════════════════
//  APP CONFIG (persisted INI in %APPDATA%\RNPerf)
// ═══════════════════════════════════════════════
static class AppConfig
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RNPerf");
    private static readonly string File = Path.Combine(Dir, "config.ini");

    public static string OutputFolder { get; set; } = "";
    public static string MppsPath { get; set; } = "";

    public static void Load()
    {
        try
        {
            if (!System.IO.File.Exists(File)) return;
            foreach (var line in System.IO.File.ReadAllLines(File))
            {
                var i = line.IndexOf('=');
                if (i < 0) continue;
                var k = line[..i].Trim();
                var v = line[(i + 1)..].Trim();
                if (k == "OutputFolder") OutputFolder = v;
                else if (k == "MppsPath") MppsPath = v;
            }
        }
        catch { }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            System.IO.File.WriteAllLines(File, new[]
            {
                $"OutputFolder={OutputFolder}",
                $"MppsPath={MppsPath}",
            });
        }
        catch { }
    }
}

// ═══════════════════════════════════════════════
//  RNPERF FORMAT (inchangé)
// ═══════════════════════════════════════════════
static class RnperfFormat
{
    private static (byte[] key, byte[] iv) DeriveKeyIv(string password, byte[] salt)
    {
        var derived = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt,
            200_000, HashAlgorithmName.SHA256, 48);
        return (derived[..32], derived[32..48]);
    }

    public static byte[] Encrypt(byte[] plainData, string password)
    {
        using var compMs = new MemoryStream();
        using (var zlib = new ZLibStream(compMs, CompressionLevel.Optimal))
            zlib.Write(plainData);
        var compressed = compMs.ToArray();

        var salt = RandomNumberGenerator.GetBytes(16);
        var (key, iv) = DeriveKeyIv(password, salt);

        using var aes = Aes.Create();
        aes.Key = key; aes.IV = iv; aes.Mode = CipherMode.CBC;

        using var encMs = new MemoryStream();
        using (var enc = aes.CreateEncryptor())
        using (var cs = new CryptoStream(encMs, enc, CryptoStreamMode.Write))
            cs.Write(compressed);
        var encrypted = encMs.ToArray();

        var hmacKey = RandomNumberGenerator.GetBytes(32);
        using var hmac = new HMACSHA256(hmacKey);
        var tag = hmac.ComputeHash(encrypted);

        using var outMs = new MemoryStream();
        using var bw = new BinaryWriter(outMs);
        bw.Write(Encoding.ASCII.GetBytes("RNPERF"));
        bw.Write((byte)2);
        bw.Write(salt);
        bw.Write(iv);
        bw.Write(hmacKey);
        bw.Write(tag);
        bw.Write(BitConverter.GetBytes(encrypted.Length));
        bw.Write(encrypted);
        return outMs.ToArray();
    }

    public static byte[] Decrypt(byte[] fileData, string password)
    {
        using var ms = new MemoryStream(fileData);
        using var br = new BinaryReader(ms);

        var magic = Encoding.ASCII.GetString(br.ReadBytes(6));
        if (magic != "RNPERF") throw new Exception("Fichier invalide (mauvais magic)");
        var version = br.ReadByte();
        if (version != 2) throw new Exception($"Version {version} non supportée");

        var salt = br.ReadBytes(16);
        var iv = br.ReadBytes(16);
        var hmacKey = br.ReadBytes(32);
        var tag = br.ReadBytes(32);
        var encLen = BitConverter.ToInt32(br.ReadBytes(4));
        var encrypted = br.ReadBytes(encLen);

        using var hmac = new HMACSHA256(hmacKey);
        var expectedTag = hmac.ComputeHash(encrypted);

        if (!CryptographicOperations.FixedTimeEquals(tag, expectedTag))
            throw new Exception("❌ Le mot de passe est incorrect !");

        var (key, _) = DeriveKeyIv(password, salt);
        using var aes = Aes.Create();
        aes.Key = key; aes.IV = iv; aes.Mode = CipherMode.CBC;

        byte[] compressed;
        try
        {
            using var decMs = new MemoryStream();
            using (var dec = aes.CreateDecryptor())
            using (var cs = new CryptoStream(new MemoryStream(encrypted), dec, CryptoStreamMode.Read))
                cs.CopyTo(decMs);
            compressed = decMs.ToArray();
        }
        catch
        {
            throw new Exception("❌ Le mot de passe est incorrect !");
        }

        using var decompMs = new MemoryStream();
        using (var zlib = new ZLibStream(new MemoryStream(compressed), CompressionMode.Decompress))
            zlib.CopyTo(decompMs);
        return decompMs.ToArray();
    }  // ✅ ACCOLADE QUI FERME LA MÉTHODE
}  // ✅ ACCOLADE QUI FERME LA CLASSE RnperfFormat
// ═══════════════════════════════════════════════
//  ROUNDED BUTTON
// ═══════════════════════════════════════════════
public class RoundedButton : Button
{
    public int BorderRadius { get; set; } = 12;
    public Color NormalColor { get; set; } = Theme.Purple;
    public Color HoverColor { get; set; } = Theme.Purple2;
    public bool EnableGlow { get; set; } = true;

    private bool _hover = false;
    private float _glow = 0f;
    private readonly System.Windows.Forms.Timer _timer;

    public RoundedButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        ForeColor = Color.White;
        Font = new Font("Segoe UI Semibold", 11F);
        Cursor = Cursors.Hand;
        BackColor = Theme.BG;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);

        _timer = new System.Windows.Forms.Timer { Interval = 15 };
        _timer.Tick += (_, _) =>
        {
            float target = _hover ? 1f : 0f;
            float diff = target - _glow;
            if (Math.Abs(diff) < 0.02f) { _glow = target; _timer.Stop(); }
            else _glow += diff * 0.25f;
            Invalidate();
        };
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; _timer.Start(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _timer.Start(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.BG);

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);

        if (EnableGlow && _glow > 0.01f)
        {
            for (int i = 6; i >= 1; i--)
            {
                using var glowPath = RoundedRect(Rectangle.Inflate(rect, i, i), BorderRadius + i);
                int alpha = (int)(18 * _glow * (7 - i) / 6f);
                using var pen = new Pen(Color.FromArgb(Math.Clamp(alpha, 0, 80), Theme.Purple2), 2);
                g.DrawPath(pen, glowPath);
            }
        }

        using var path = RoundedRect(rect, BorderRadius);
        var c1 = Lerp(NormalColor, HoverColor, _glow);
        var c2 = Lerp(Color.FromArgb(85, 45, 180), Theme.PurpleHi, _glow);
        using var brush = new LinearGradientBrush(rect, c1, c2, 90f);
        g.FillPath(brush, path);

        TextRenderer.DrawText(g, Text, Font, rect, ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Color Lerp(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));
    }
}

// ═══════════════════════════════════════════════
//  GEAR ICON BUTTON (custom)
// ═══════════════════════════════════════════════
public class GearButton : Control
{
    private bool _hover = false;
    private float _angle = 0f;
    private readonly System.Windows.Forms.Timer _timer;

    public GearButton()
    {
        Size = new Size(36, 36);
        BackColor = Theme.BG;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);

        _timer = new System.Windows.Forms.Timer { Interval = 16 };
        _timer.Tick += (_, _) =>
        {
            if (_hover) { _angle += 2.5f; if (_angle >= 360) _angle -= 360; Invalidate(); }
            else _timer.Stop();
        };
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; _timer.Start(); Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

        var color = _hover ? Theme.Purple2 : Theme.FGDim;

        // Rotation autour du centre
        var state = g.Save();
        g.TranslateTransform(Width / 2f, Height / 2f);
        g.RotateTransform(_angle);
        g.TranslateTransform(-Width / 2f, -Height / 2f);

        using var font = new Font("Segoe UI Symbol", 18f);
        TextRenderer.DrawText(g, "⚙", font,
            new Rectangle(0, 0, Width, Height), color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        g.Restore(state);
    }
}

// ═══════════════════════════════════════════════
//  STYLED TEXTBOX
// ═══════════════════════════════════════════════
public class StyledTextBox : Panel
{
    public TextBox Inner { get; }
    private bool _focused = false;

    public StyledTextBox(string placeholder, bool isPassword = false, bool readOnly = false)
    {
        Height = 38;
        BackColor = Theme.Card;
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);

        Inner = new TextBox
        {
            PlaceholderText = placeholder,
            BackColor = Theme.Card,
            ForeColor = Theme.FG,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 10),
            UseSystemPasswordChar = isPassword,
            ReadOnly = readOnly,
            Left = 12,
            Top = 12,
        };
        Inner.Enter += (_, _) => { _focused = true; Invalidate(); };
        Inner.Leave += (_, _) => { _focused = false; Invalidate(); };

        Controls.Add(Inner);
        Resize += (_, _) => { Inner.Width = Width - 24; Inner.Top = (Height - Inner.Height) / 2; };
    }

    public string TextValue { get => Inner.Text; set => Inner.Text = value; }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = RoundRect(rect, 8);

        using var bg = new SolidBrush(_focused ? Theme.CardHi : Theme.Card);
        g.FillPath(bg, path);
        Inner.BackColor = _focused ? Theme.CardHi : Theme.Card;

        using var pen = new Pen(_focused ? Theme.Purple2 : Theme.Border, _focused ? 2 : 1);
        g.DrawPath(pen, path);
    }

    private static GraphicsPath RoundRect(Rectangle r, int radius)
    {
        var p = new GraphicsPath();
        int d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}

// ═══════════════════════════════════════════════
//  POPUP DIALOG
// ═══════════════════════════════════════════════
public class PopupDialog : Form
{
    public enum PopupType { Success, Error, Info }

    public static void Show(IWin32Window owner, string message, PopupType type)
    {
        using var p = new PopupDialog(message, type);
        p.ShowDialog(owner);
    }

    private PopupDialog(string message, PopupType type)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Theme.Panel;
        Size = new Size(440, 240);
        ShowInTaskbar = false;
        DoubleBuffered = true;
        Padding = new Padding(2);

        (string icon, Color color, string title) = type switch
        {
            PopupType.Success => ("✓", Theme.Green, "Succès"),
            PopupType.Error => ("✗", Theme.Red, "Erreur"),
            _ => ("ℹ", Theme.Purple2, "Information"),
        };

        var topBar = new Panel { Dock = DockStyle.Top, Height = 5, BackColor = color };

        var lblIcon = new Label
        {
            Text = icon,
            Font = new Font("Segoe UI", 42, FontStyle.Bold),
            ForeColor = color,
            AutoSize = false,
            Size = new Size(80, 80),
            Location = new Point(30, 35),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Theme.Panel,  // ✅ PANEL au lieu de BG
        };

        var lblTitle = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = Theme.FG,
            AutoSize = false,
            Location = new Point(125, 38),
            Size = new Size(290, 30),
            BackColor = Theme.Panel,  // ✅ PANEL
        };

        var lblMsg = new Label
        {
            Text = message,
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Theme.FGDim,
            AutoSize = false,
            Location = new Point(125, 72),
            Size = new Size(290, 80),
            BackColor = Theme.Panel,  // ✅ PANEL
        };

        var btnOk = new RoundedButton
        {
            Text = "OK",
            Width = 110,
            Height = 38,
            Location = new Point(310, 180),
            NormalColor = color,
            HoverColor = ControlPaint.Light(color, 0.2f),
            Font = new Font("Segoe UI Semibold", 10),
        };
        btnOk.Click += (_, _) => Close();

        Controls.Add(lblIcon);
        Controls.Add(lblTitle);
        Controls.Add(lblMsg);
        Controls.Add(btnOk);
        Controls.Add(topBar);

        AcceptButton = btnOk;
        KeyPreview = true;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Theme.Border, 1);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
}

// ═══════════════════════════════════════════════
//  OPTIONS FORM (modal dark)
// ═══════════════════════════════════════════════
public class OptionsForm : Form
{
    private readonly StyledTextBox _tbFolder;
    private readonly StyledTextBox _tbMpps;

    public OptionsForm()
    {
        Text = "Options";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Theme.Panel;
        Size = new Size(560, 360);
        ShowInTaskbar = false;
        DoubleBuffered = true;

        var topBar = new Panel { Dock = DockStyle.Top, Height = 5, BackColor = Theme.Purple };

        var lblTitle = new Label
        {
            Text = "⚙   Options",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = Theme.Purple2,
            AutoSize = false,
            Location = new Point(30, 22),
            Size = new Size(400, 32),
            BackColor = Theme.Panel,  // ✅ PANEL
        };

        var lblFolder = new Label
        {
            Text = "📁  Dossier de sortie (.rnperf)",
            Font = new Font("Segoe UI Semibold", 9),
            ForeColor = Theme.FGDim,
            AutoSize = true,
            Location = new Point(30, 78),
            BackColor = Theme.Panel,  // ✅ PANEL
        };

        _tbFolder = new StyledTextBox("Aucun dossier sélectionné…", readOnly: true)
        {
            Location = new Point(30, 102),
            Width = 420,
            TextValue = AppConfig.OutputFolder,
        };

        var btnFolder = new RoundedButton
        {
            Text = "📂",
            Width = 48,
            Height = 42,
            BorderRadius = 10,
            Location = new Point(460, 102),
            Font = new Font("Segoe UI", 12),
        };
        btnFolder.Click += (_, _) =>
        {
            using var dlg = new FolderBrowserDialog();
            if (dlg.ShowDialog() == DialogResult.OK) _tbFolder.TextValue = dlg.SelectedPath;
        };

        var lblMpps = new Label
        {
            Text = "🛠️   Chemin MPPS.exe (par défaut)",
            Font = new Font("Segoe UI Semibold", 9),
            ForeColor = Theme.FGDim,
            AutoSize = true,
            Location = new Point(30, 168),
            BackColor = Theme.Panel,  // ✅ PANEL
        };

        _tbMpps = new StyledTextBox("C:\\LOGICIEL REPROG\\mpps\\Mpps.exe", readOnly: true)
        {
            Location = new Point(30, 192),
            Width = 420,
            TextValue = AppConfig.MppsPath,
        };

        var btnMpps = new RoundedButton
        {
            Text = "📂",
            Width = 48,
            Height = 42,
            BorderRadius = 10,
            Location = new Point(460, 192),
            Font = new Font("Segoe UI", 12),
        };
        btnMpps.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog { Filter = "Executable|*.exe" };
            if (dlg.ShowDialog() == DialogResult.OK) _tbMpps.TextValue = dlg.FileName;
        };

        var btnCancel = new RoundedButton
        {
            Text = "Annuler",
            Width = 120,
            Height = 40,
            Location = new Point(280, 300),
            NormalColor = Theme.Card,
            HoverColor = Theme.CardHi,
            Font = new Font("Segoe UI Semibold", 10),
        };
        btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        var btnSave = new RoundedButton
        {
            Text = "💾  Enregistrer",
            Width = 140,
            Height = 40,
            Location = new Point(410, 300),
            Font = new Font("Segoe UI Semibold", 10),
        };
        btnSave.Click += (_, _) =>
        {
            AppConfig.OutputFolder = _tbFolder.TextValue;
            AppConfig.MppsPath = _tbMpps.TextValue;
            AppConfig.Save();
            DialogResult = DialogResult.OK;
            Close();
        };

        Controls.Add(lblTitle);
        Controls.Add(lblFolder);
        Controls.Add(_tbFolder);
        Controls.Add(btnFolder);
        Controls.Add(lblMpps);
        Controls.Add(_tbMpps);
        Controls.Add(btnMpps);
        Controls.Add(btnCancel);
        Controls.Add(btnSave);
        Controls.Add(topBar);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Theme.Border, 1);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
}
// ═══════════════════════════════════════════════
//  MAIN FORM
// ═══════════════════════════════════════════════
public partial class MainForm : Form
{
    private Button _btnCrypter = null!;
    private Button _btnFlash = null!;
    private Button _btnAdmin = null!;        // ✅ NOUVEAU
    private Panel _pgCrypter = null!;
    private Panel _pgFlash = null!;
    private Panel _pgAdmin = null!;           // ✅ NOUVEAU

    public MainForm()
    {
        // ✅ INIT FIREBASE + LOGIN
        UserManager.Init();
        ShowLoginDialog();

        if (!UserManager.IsLoggedIn)
        {
            Application.Exit();
            return;
        }

        AppConfig.Load();
        Text = $"RNPerf Tool - {UserManager.CurrentUser}";
        Size = new Size(840, 500);
        MinimumSize = new Size(740, 430);
        BackColor = Theme.BG;
        ForeColor = Theme.FG;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BuildUI();
    }

    // ✅ AJOUTE CETTE MÉTHODE
    private void ShowLoginDialog()
    {
        using var loginForm = new LoginForm();
        if (loginForm.ShowDialog() != DialogResult.OK)
        {
            Application.Exit();
        }
    }

    private void BuildUI()
    {
        // Header
        var header = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = Theme.Panel };
        var title = new Label
        {
            Text = "⚡  RNPerf Tool",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Theme.Purple2,
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            BackColor = Theme.BG,
        };
        var headerLine = new Panel { Dock = DockStyle.Bottom, Height = 3, BackColor = Theme.Purple };

        // ⚙ Engrenage en haut à droite
        var gear = new GearButton();
        gear.Click += (_, _) =>
        {
            using var opt = new OptionsForm();
            opt.ShowDialog(this);
        };
        header.Resize += (_, _) =>
        {
            gear.Left = header.Width - gear.Width - 18;
            gear.Top = (header.Height - gear.Height) / 2 - 1;
        };

        header.Controls.Add(gear);
        header.Controls.Add(title);
        header.Controls.Add(headerLine);
        gear.BringToFront();

        // Tab bar
        var tabBar = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Theme.Panel };

        var segmented = new Panel
        {
            Width = 360,
            Height = 42,
            BackColor = Theme.Card,
        };

        _btnCrypter = MakeTabButton("🔐   Crypter");
        _btnFlash = MakeTabButton("⚡   Flash MPPS");

        // ✅ Si admin → 3 onglets, sinon 2
        if (UserManager.IsAdmin)
        {
            segmented.Width = 540;  // Plus large pour 3 boutons
            _btnAdmin = MakeTabButton("🛡️   Admin");
            _btnCrypter.SetBounds(3, 3, 177, 36);
            _btnFlash.SetBounds(180, 3, 177, 36);
            _btnAdmin.SetBounds(357, 3, 177, 36);

            _btnCrypter.Click += (_, _) => SwitchTab(0);
            _btnFlash.Click += (_, _) => SwitchTab(1);
            _btnAdmin.Click += (_, _) => SwitchTab(2);

            segmented.Controls.Add(_btnCrypter);
            segmented.Controls.Add(_btnFlash);
            segmented.Controls.Add(_btnAdmin);
        }
        else
        {
            _btnCrypter.SetBounds(3, 3, 177, 36);
            _btnFlash.SetBounds(180, 3, 177, 36);

            _btnCrypter.Click += (_, _) => SwitchTab(0);
            _btnFlash.Click += (_, _) => SwitchTab(1);

            segmented.Controls.Add(_btnCrypter);
            segmented.Controls.Add(_btnFlash);
        }


        tabBar.Resize += (_, _) =>
        {
            segmented.Left = (tabBar.Width - segmented.Width) / 2;
            segmented.Top = (tabBar.Height - segmented.Height) / 2;
        };

        segmented.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, segmented.Width - 1, segmented.Height - 1);
            using var path = new GraphicsPath();
            int r = 10, d = r * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            segmented.Region = new Region(path);
            using var pen = new Pen(Theme.Border, 1);
            g.DrawPath(pen, path);
        };

        var tabLine = new Panel { Dock = DockStyle.Bottom, Height = 2, BackColor = Theme.Border };
        tabBar.Controls.Add(segmented);
        tabBar.Controls.Add(tabLine);

        _pgCrypter = BuildCrypterPage();
        _pgFlash = BuildFlashPage();
        _pgCrypter.Enabled = UserManager.IsLoggedIn;
        _pgFlash.Enabled = true;
        _pgCrypter.Dock = DockStyle.Fill;
        _pgFlash.Dock = DockStyle.Fill;

        var pageHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.BG };
        pageHost.Controls.Add(_pgCrypter);
        pageHost.Controls.Add(_pgFlash);

        // ✅ Ajoute le panel admin si admin
        if (UserManager.IsAdmin)
        {
            _pgAdmin = BuildAdminPage();
            _pgAdmin.Dock = DockStyle.Fill;
            pageHost.Controls.Add(_pgAdmin);
        }


        Controls.Add(pageHost);
        Controls.Add(tabBar);
        Controls.Add(header);

        SwitchTab(UserManager.CanCrypt || UserManager.IsAdmin ? 0 : 1);
    }

    private void SwitchTab(int index)
    {
        // Bloquer Crypter si pas autorisé
        if (index == 0 && !UserManager.CanCrypt && !UserManager.IsAdmin)
        {
            PopupDialog.Show(this,
                "Tu n'as pas accès à la fonction Crypter.\nContacte l'admin.",
                PopupDialog.PopupType.Error);
            return;
        }

        _pgCrypter.Visible = (index == 0);
        _pgFlash.Visible = (index == 1);
        if (_pgAdmin != null)
            _pgAdmin.Visible = (index == 2);

        SetTabActive(_btnCrypter, index == 0);
        SetTabActive(_btnFlash, index == 1);
        if (_btnAdmin != null)
            SetTabActive(_btnAdmin, index == 2);
    }

    private static void SetTabActive(Button btn, bool active)
    {
        btn.BackColor = active ? Theme.Purple : Theme.Card;
        btn.ForeColor = active ? Color.White : Theme.FGDim;
        btn.Font = new Font("Segoe UI Semibold", 10, active ? FontStyle.Bold : FontStyle.Regular);
    }

    private static Button MakeTabButton(string text)
    {
        var btn = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Card,
            ForeColor = Theme.FGDim,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI Semibold", 10),
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 40, 80);

        btn.Resize += (_, _) =>
        {
            var path = new GraphicsPath();
            int r = 8, d = r * 2;
            var rect = new Rectangle(0, 0, btn.Width, btn.Height);
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            btn.Region = new Region(path);
        };
        return btn;
    }

    // ─── PAGE CRYPTER ──────────────────────────────
    private Panel BuildCrypterPage()
    {
        var page = MakePage();
        var layout = MakeLayout(page);

        var (tbSrc, pSrc) = MakeFileRow("Fichier source (.bin / .hex)…", true);
        var (tbPass, pPass) = MakeTextRow("Mot de passe…", true);

        var btn = MakeBigButton("🔒   Chiffrer", () => TryCatch(() =>
        {
            if (string.IsNullOrWhiteSpace(tbSrc.TextValue)) throw new Exception("Sélectionne un fichier source !");
            if (string.IsNullOrWhiteSpace(tbPass.TextValue)) throw new Exception("Entre un mot de passe !");

            using var dlg = new SaveFileDialog
            {
                Filter = "RNPerf|*.rnperf",
                FileName = Path.GetFileNameWithoutExtension(tbSrc.TextValue) + ".rnperf",
                InitialDirectory = !string.IsNullOrWhiteSpace(AppConfig.OutputFolder) &&
                                   Directory.Exists(AppConfig.OutputFolder)
                                   ? AppConfig.OutputFolder
                                   : Path.GetDirectoryName(tbSrc.TextValue) ?? "",
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            var data = File.ReadAllBytes(tbSrc.TextValue);
            var enc = RnperfFormat.Encrypt(data, tbPass.TextValue);
            File.WriteAllBytes(dlg.FileName, enc);
            ShowOk($"Fichier chiffré avec succès !\n📁 {Path.GetFileName(dlg.FileName)}");
        }));

        layout.Controls.Add(SectionLabel("📂  Fichier firmware source"));
        layout.Controls.Add(pSrc);
        layout.Controls.Add(SectionLabel("🔑  Mot de passe"));
        layout.Controls.Add(pPass);
        layout.Controls.Add(Spacer(5));
        layout.Controls.Add(btn);

        var hint = new Label
        {
            Text = "💡  Le dossier de sortie par défaut peut être configuré via ⚙ Options.",
            ForeColor = Theme.FGDim,
            BackColor = Theme.BG,
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
            Margin = new Padding(2, 16, 0, 0),
        };
        layout.Controls.Add(hint);

        return page;
    }

    // ─── PAGE FLASH ────────────────────────────────
    private Panel BuildFlashPage()
    {
        var page = MakePage();
        var layout = MakeLayout(page);

        var (tbRnperf, pRnperf) = MakeFileRow("Fichier .rnperf…", true);
        var (tbPass, pPass) = MakeTextRow("Mot de passe…", true);

        var lblStatus = new Label
        {
            Text = "En attente…",
            ForeColor = Theme.FGDim,
            BackColor = Theme.BG,
            AutoSize = true,
            Font = new Font("Segoe UI", 10),
        };

        var btn = MakeBigButton("⚡   Flash MPPS", () => TryCatch(() =>
        {
            if (string.IsNullOrWhiteSpace(tbRnperf.TextValue))
                throw new Exception("Sélectionne un fichier .rnperf !");
            if (string.IsNullOrWhiteSpace(tbPass.TextValue))
                throw new Exception("Entre le mot de passe !");

            lblStatus.Text = "🔓  Déchiffrement…";
            lblStatus.ForeColor = Theme.FGDim;
            Application.DoEvents();

            var enc = File.ReadAllBytes(tbRnperf.TextValue);
            var data = RnperfFormat.Decrypt(enc, tbPass.TextValue);

            // ✅ CORRECTION DES CHECKSUMS AUTOMATIQUE
            lblStatus.Text = "🔧  Correction des checksums…";
            Application.DoEvents();

            var calc = new EDC15ChecksumCalculator();
            if (!calc.FixAllChecksums(data, out string checksumReport))
            {
                lblStatus.Text = "⚠️  Checksum incomplet (fichier non-EDC15?)";
                lblStatus.ForeColor = Color.FromArgb(255, 180, 0);  // Orange
            }
            else
            {
                lblStatus.Text = "✅  Checksums corrigés";
                lblStatus.ForeColor = Theme.Green;
                Application.DoEvents();
                System.Threading.Thread.Sleep(500);  // Petit délai pour voir le message
            }

            // Sauvegarde temporaire avec checksums corrigés
            var tmp = Path.Combine(Path.GetTempPath(), $"rnperf_{Guid.NewGuid():N}.bin");
            File.WriteAllBytes(tmp, data);

            lblStatus.Text = "🚀  Lancement MPPS…";
            lblStatus.ForeColor = Theme.FGDim;
            Application.DoEvents();

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-Command \"Start-Process '{AppConfig.MppsPath}' -ArgumentList '\\\"{tmp}\\\"' -Verb RunAs\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            Process.Start(psi);

            lblStatus.Text = "✅  MPPS lancé avec succès !";
            lblStatus.ForeColor = Theme.Green;

            // Nettoyage après 60 secondes
            Task.Delay(60_000).ContinueWith(_ => { try { File.Delete(tmp); } catch { } });
        }));

        layout.Controls.Add(SectionLabel("📁  Fichier .rnperf"));
        layout.Controls.Add(pRnperf);
        layout.Controls.Add(SectionLabel("🔑  Mot de passe"));
        layout.Controls.Add(pPass);
        layout.Controls.Add(Spacer(5));
        layout.Controls.Add(btn);
        layout.Controls.Add(Spacer(8));
        layout.Controls.Add(lblStatus);

        return page;
    }

    // ─── HELPERS ───────────────────────────────────
    private static Panel MakePage() => new() { BackColor = Theme.BG, Visible = false };

    private static FlowLayoutPanel MakeLayout(Panel page)
    {
        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(20, 15, 15, 20),
            BackColor = Theme.BG,
        };
        layout.Resize += (_, _) =>
        {
            foreach (Control c in layout.Controls)
                if (c is not Label { AutoSize: true })
                    c.Width = layout.ClientSize.Width - 84;
        };
        page.Controls.Add(layout);
        return layout;
    }

    private static Label SectionLabel(string text) => new()
    {
        Text = text,
        ForeColor = Theme.FGDim,
        BackColor = Theme.BG,
        AutoSize = true,
        Font = new Font("Segoe UI Semibold", 9),
        Margin = new Padding(2, 8, 0, 4),
    };

    private static Panel Spacer(int h) => new() { Height = h, BackColor = Theme.BG };

    private static (StyledTextBox tb, Panel panel) MakeFileRow(string placeholder, bool isOpen)
    {
        var tb = new StyledTextBox(placeholder);
        var btn = new RoundedButton
        {
            Text = "📂",
            Width = 48,
            Height = 42,
            BorderRadius = 10,
            Font = new Font("Segoe UI", 12),
        };

        btn.Click += (_, _) =>
        {
            if (isOpen)
            {
                using var dlg = new OpenFileDialog();
                if (dlg.ShowDialog() == DialogResult.OK) tb.TextValue = dlg.FileName;
            }
            else
            {
                using var dlg = new SaveFileDialog { Filter = "RNPerf|*.rnperf" };
                if (dlg.ShowDialog() == DialogResult.OK) tb.TextValue = dlg.FileName;
            }
        };

        var panel = new Panel { Height = 46, BackColor = Theme.BG };
        panel.Controls.Add(tb);
        panel.Controls.Add(btn);
        panel.Resize += (_, _) =>
        {
            tb.Width = panel.Width - btn.Width - 10;
            tb.Top = 2;
            btn.Left = tb.Right + 10;
            btn.Top = 2;
        };
        return (tb, panel);
    }

    private static (StyledTextBox tb, Panel panel) MakeTextRow(string placeholder, bool isPassword)
    {
        var tb = new StyledTextBox(placeholder, isPassword);
        var panel = new Panel { Height = 46, BackColor = Theme.BG };
        panel.Controls.Add(tb);
        panel.Resize += (_, _) => { tb.Width = panel.Width; tb.Top = 2; };
        return (tb, panel);
    }

    private static RoundedButton MakeBigButton(string text, Action onClick)
    {
        var btn = new RoundedButton
        {
            Text = text,
            Height = 58,
            BorderRadius = 14,
            Font = new Font("Segoe UI Semibold", 13),
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private Panel BuildAdminPage()
    {
        var page = MakePage();
        var adminPanel = new AdminPanel { Dock = DockStyle.Fill };
        page.Controls.Add(adminPanel);
        return page;
    }

    private void TryCatch(Action action)
    {
        try { action(); }
        catch (Exception ex) { PopupDialog.Show(this, ex.Message, PopupDialog.PopupType.Error); }
    }

    private void ShowOk(string msg) => PopupDialog.Show(this, msg, PopupDialog.PopupType.Success);
}
