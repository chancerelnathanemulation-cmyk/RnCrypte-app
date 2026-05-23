using RNPerf;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

public class LoginForm : Form
{
    private StyledTextBox _tbUsername = null!;
    private StyledTextBox _tbPassword = null!;
    private Label _lblStatus = null!;
    private RoundedButton _btnLogin = null!;
    private RoundedButton _btnRegister = null!;

    public LoginForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.BG;
        Size = new Size(400, 450);
        ShowInTaskbar = false;
        DoubleBuffered = true;

        BuildUI();
    }

    private void BuildUI()
    {
        // Header
        var topBar = new Panel { Dock = DockStyle.Top, Height = 80, BackColor = Theme.Panel };
        var lblTitle = new Label
        {
            Text = "🔐  Connexion",
            Font = new Font("Segoe UI", 24, FontStyle.Bold),
            ForeColor = Theme.Purple2,
            AutoSize = false,
            Size = new Size(400, 70),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Theme.Panel,
        };
        topBar.Controls.Add(lblTitle);

        // Main layout
        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(25, 20, 25, 20),
            BackColor = Theme.BG,
            AutoScroll = true,
        };

        var (tbUser, pUser) = MakeTextRow("Username…", false);
        _tbUsername = tbUser;

        var (tbPass, pPass) = MakeTextRow("Password…", true);
        _tbPassword = tbPass;

        _lblStatus = new Label
        {
            Text = "",
            ForeColor = Theme.Red,
            BackColor = Theme.BG,
            AutoSize = true,
            Font = new Font("Segoe UI", 9),
            Margin = new Padding(0, 10, 0, 0),
        };

        _btnLogin = new RoundedButton
        {
            Text = "Se connecter",
            Height = 48,
            Width = 350,
            BorderRadius = 10,
            Font = new Font("Segoe UI Semibold", 11),
            Margin = new Padding(0, 20, 0, 10),
        };
        _btnLogin.Click += async (_, _) => await OnLoginClick();

        _btnRegister = new RoundedButton
        {
            Text = "Créer un compte",
            Height = 48,
            Width = 350,
            BorderRadius = 10,
            Font = new Font("Segoe UI Semibold", 11),
            BackColor = Theme.Card,
            ForeColor = Theme.FGDim,
            Margin = new Padding(0, 0, 0, 0),
        };
        _btnRegister.Click += async (_, _) => await OnRegisterClick();

        layout.Controls.Add(new Label { Height = 10, BackColor = Theme.BG });
        layout.Controls.Add(pUser);
        layout.Controls.Add(pPass);
        layout.Controls.Add(_lblStatus);
        layout.Controls.Add(_btnLogin);
        layout.Controls.Add(_btnRegister);

        Controls.Add(layout);
        Controls.Add(topBar);
    }

    private async Task OnLoginClick()
    {
        try
        {
            _lblStatus.Text = "⏳  Connexion…";
            _lblStatus.ForeColor = Theme.FGDim;
            Application.DoEvents();

            var success = await UserManager.LoginAsync(_tbUsername.TextValue, _tbPassword.TextValue);
            if (success)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                _lblStatus.Text = "❌ Identifiants incorrects";
                _lblStatus.ForeColor = Theme.Red;
            }
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"❌ Erreur : {ex.Message}";
            _lblStatus.ForeColor = Theme.Red;
        }
    }

    private async Task OnRegisterClick()
    {
        try
        {
            _lblStatus.Text = "⏳  Inscription…";
            _lblStatus.ForeColor = Theme.FGDim;
            Application.DoEvents();

            var success = await UserManager.RegisterAsync(_tbUsername.TextValue, _tbPassword.TextValue);
            if (success)
            {
                _lblStatus.Text = "✅ Compte créé ! En attente d'approbation de l'admin…";
                _lblStatus.ForeColor = Theme.Green;
                _tbUsername.TextValue = "";
                _tbPassword.TextValue = "";
            }
            else
            {
                _lblStatus.Text = "❌ Cet utilisateur existe déjà";
                _lblStatus.ForeColor = Theme.Red;
            }
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"❌ Erreur : {ex.Message}";
            _lblStatus.ForeColor = Theme.Red;
        }
    }

    private static (StyledTextBox tb, Panel panel) MakeTextRow(string placeholder, bool isPassword)
    {
        var tb = new StyledTextBox(placeholder, isPassword);
        var panel = new Panel { Height = 50, BackColor = Theme.BG, Width = 350 };
        panel.Controls.Add(tb);
        panel.Resize += (_, _) => { tb.Width = panel.Width; tb.Top = 4; };
        return (tb, panel);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Theme.Border, 1);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
}
