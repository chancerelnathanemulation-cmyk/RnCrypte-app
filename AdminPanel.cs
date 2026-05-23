using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RNPerf;

public class AdminPanel : UserControl
{
    private DataGridView _grid = null!;
    private Button _btnRefresh = null!;
    private Button _btnApprove = null!;
    private Button _btnUnapprove = null!;
    private Button _btnDelete = null!;
    private Button _btnResetPwd = null!;
    private Button _btnToggleAdmin = null!;
    private Label _lblStatus = null!;
    private Button _btnToggleCrypt = null!;

    public AdminPanel()
    {
        InitUI();
        _ = LoadUsersAsync();
    }

    private void InitUI()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(30, 30, 40);

        // ===== HEADER (titre + status) =====
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 80,
            BackColor = Color.FromArgb(30, 30, 40),
            Padding = new Padding(20, 10, 20, 10)
        };

        var lblTitle = new Label
        {
            Text = "🛡️ PANEL ADMINISTRATEUR",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(20, 10),
            AutoSize = true
        };
        headerPanel.Controls.Add(lblTitle);

        _lblStatus = new Label
        {
            Text = "Chargement...",
            Font = new Font("Segoe UI", 9),
            ForeColor = Color.LightGray,
            Location = new Point(20, 48),
            AutoSize = true
        };
        headerPanel.Controls.Add(_lblStatus);

        // ===== FOOTER (boutons) =====
        var footerPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            BackColor = Color.FromArgb(25, 25, 35),
            Padding = new Padding(15, 10, 15, 10),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true
        };

        _btnRefresh = MakeBtn("🔄 Actualiser", Color.FromArgb(70, 130, 180));
        _btnRefresh.Click += async (s, e) => await LoadUsersAsync();

        _btnApprove = MakeBtn("✅ Approuver", Color.FromArgb(40, 167, 69));
        _btnApprove.Click += async (s, e) => await ApproveSelectedAsync();

        _btnUnapprove = MakeBtn("⛔ Désapprouver", Color.FromArgb(255, 140, 0));
        _btnUnapprove.Click += async (s, e) => await UnapproveSelectedAsync();

        _btnDelete = MakeBtn("🗑️ Supprimer", Color.FromArgb(220, 53, 69));
        _btnDelete.Click += async (s, e) => await DeleteSelectedAsync();

        _btnResetPwd = MakeBtn("🔑 Reset MDP", Color.FromArgb(108, 117, 125));
        _btnResetPwd.Click += async (s, e) => await ResetPasswordSelectedAsync();

        _btnToggleAdmin = MakeBtn("👑 Toggle Admin", Color.FromArgb(138, 43, 226));
        _btnToggleAdmin.Click += async (s, e) => await ToggleAdminSelectedAsync();
       
        _btnToggleCrypt = MakeBtn("🔐 Toggle Crypt", Color.FromArgb(0, 150, 136));
        _btnToggleCrypt.Click += async (s, e) => await ToggleCryptSelectedAsync();
        
        footerPanel.Controls.Add(_btnRefresh);
        footerPanel.Controls.Add(_btnApprove);
        footerPanel.Controls.Add(_btnUnapprove);
        footerPanel.Controls.Add(_btnDelete);
        footerPanel.Controls.Add(_btnResetPwd);
        footerPanel.Controls.Add(_btnToggleAdmin);
        footerPanel.Controls.Add(_btnToggleCrypt);

        // ===== GRID (centre, remplit l'espace) =====
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.FromArgb(40, 40, 55),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.None,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            EnableHeadersVisualStyles = false,
            GridColor = Color.FromArgb(60, 60, 80)
        };
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(50, 50, 70);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        _grid.DefaultCellStyle.BackColor = Color.FromArgb(40, 40, 55);
        _grid.DefaultCellStyle.ForeColor = Color.White;
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(80, 120, 200);
        _grid.ColumnHeadersHeight = 35;
        _grid.RowTemplate.Height = 30;

        _grid.Columns.Add("username", "Utilisateur");
        _grid.Columns.Add("approved", "Approuvé");
        _grid.Columns.Add("isAdmin", "Admin");
        _grid.Columns.Add("canCrypt", "Peut Crypter");

        // Wrapper pour padding autour du grid
        var gridWrapper = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(30, 30, 40),
            Padding = new Padding(20, 10, 20, 10)
        };
        gridWrapper.Controls.Add(_grid);

        // ===== ORDRE D'AJOUT IMPORTANT (Fill en premier dans le code = en DERNIER dans Dock) =====
        Controls.Add(gridWrapper);   // Fill (centre)
        Controls.Add(footerPanel);   // Bottom
        Controls.Add(headerPanel);   // Top
    }

    private Button MakeBtn(string text, Color color)
    {
        return new Button
        {
            Text = text,
            Size = new Size(130, 38),
            Margin = new Padding(3, 0, 3, 0),
            BackColor = color,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
    }

    private async Task LoadUsersAsync()
    {
        try
        {
            _lblStatus.Text = "⏳ Chargement...";
            _grid.Rows.Clear();

            var users = await UserManager.GetAllUsersAsync();

            foreach (var u in users)
            {
                _grid.Rows.Add(
                    u.username,
                    u.approved ? "✅ Oui" : "❌ Non",
                    u.isAdmin ? "👑 Oui" : "—",
                    u.canCrypt ? "✅ Oui" : "❌ Non"
                );
            }

            _lblStatus.Text = $"📊 {users.Count} utilisateur(s) chargé(s)";
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"❌ Erreur : {ex.Message}";
        }
    }

    private string? GetSelectedUsername()
    {
        if (_grid.SelectedRows.Count == 0)
        {
            MessageBox.Show("Sélectionne un utilisateur dans la liste !", "Info",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return null;
        }
        return _grid.SelectedRows[0].Cells["username"].Value?.ToString();
    }

    private async Task ApproveSelectedAsync()
    {
        var username = GetSelectedUsername();
        if (username == null) return;

        if (await UserManager.ApproveUserAsync(username))
        {
            MessageBox.Show($"✅ {username} approuvé !");
            await LoadUsersAsync();
        }
        else MessageBox.Show("❌ Échec");
    }

    private async Task UnapproveSelectedAsync()
    {
        var username = GetSelectedUsername();
        if (username == null) return;

        if (await UserManager.UnapproveUserAsync(username))
        {
            MessageBox.Show($"⛔ {username} désapprouvé");
            await LoadUsersAsync();
        }
    }

    private async Task DeleteSelectedAsync()
    {
        var username = GetSelectedUsername();
        if (username == null) return;

        if (username == UserManager.CurrentUser)
        {
            MessageBox.Show("Tu peux pas te supprimer toi-même 😅");
            return;
        }

        var confirm = MessageBox.Show(
            $"Vraiment supprimer '{username}' ?",
            "Confirmation",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes) return;

        if (await UserManager.DeleteUserAsync(username))
        {
            MessageBox.Show($"🗑️ {username} supprimé");
            await LoadUsersAsync();
        }
    }

    private async Task ResetPasswordSelectedAsync()
    {
        var username = GetSelectedUsername();
        if (username == null) return;

        var newPwd = Microsoft.VisualBasic.Interaction.InputBox(
            $"Nouveau mot de passe pour {username} :",
            "Reset Password", "");

        if (string.IsNullOrWhiteSpace(newPwd)) return;

        if (await UserManager.ResetPasswordAsync(username, newPwd))
        {
            MessageBox.Show($"🔑 Mot de passe de {username} réinitialisé !");
        }
    }

    private async Task ToggleAdminSelectedAsync()
    {
        var username = GetSelectedUsername();
        if (username == null) return;

        if (username == UserManager.CurrentUser)
        {
            MessageBox.Show("Tu peux pas modifier ton propre statut admin 😅");
            return;
        }

        if (await UserManager.ToggleAdminAsync(username))
        {
            MessageBox.Show($"👑 Statut admin de {username} modifié");
            await LoadUsersAsync();
        }
    }
    private async Task ToggleCryptSelectedAsync()
    {
        var username = GetSelectedUsername();
        if (username == null) return;

        if (await UserManager.ToggleCryptAsync(username))
        {
            MessageBox.Show($"🔐 Accès Crypter de {username} modifié !");
            await LoadUsersAsync();
        }
    }
}
