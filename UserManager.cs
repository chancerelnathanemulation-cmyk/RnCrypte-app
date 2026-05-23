using Firebase.Database;
using Firebase.Database.Query;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RNPerf;

public class User
{
    public string username { get; set; } = "";
    public string password { get; set; } = "";
    public bool isAdmin { get; set; } = false;
    public bool approved { get; set; } = false;
    public bool canCrypt { get; set; } = false;
}

static class UserManager
{
    private static readonly string FirebaseUrl =
        "https://rnperfcrypte-default-rtdb.europe-west1.firebasedatabase.app";

    private static FirebaseClient _fb = null!;

    public static string CurrentUser { get; private set; } = "";
    public static bool IsLoggedIn { get; private set; } = false;
    public static bool IsAdmin { get; private set; } = false;
    public static bool CanCrypt { get; private set; } = false;

    public static void Init()
    {
        _fb = new FirebaseClient(FirebaseUrl);
    }

    // ============================
    // REGISTER
    // ============================
    public static async Task<bool> RegisterAsync(string username, string password)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return false;

            var existing = await _fb.Child("users").Child(username).OnceSingleAsync<User>();
            if (existing != null)
            {
                MessageBox.Show("❌ Utilisateur existe déjà", "Erreur");
                return false;
            }

            var newUser = new User
            {
                username = username,
                password = password,
                isAdmin = false,
                approved = false,
                canCrypt = false
            };

            await _fb.Child("users").Child(username).PutAsync(newUser);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erreur Register:\n{ex.Message}", "Error");
            return false;
        }
    }

    // ============================
    // LOGIN
    // ============================
    public static async Task<bool> LoginAsync(string username, string password)
    {
        try
        {
            var user = await _fb.Child("users").Child(username).OnceSingleAsync<User>();

            if (user == null)
            {
                MessageBox.Show($"Utilisateur '{username}' non trouvé", "Error");
                return false;
            }

            if (user.password != password)
            {
                MessageBox.Show("Mot de passe incorrect", "Error");
                return false;
            }

            if (!user.approved)
            {
                MessageBox.Show("⏳ Ton compte n'est pas encore approuvé par l'admin", "Info");
                return false;
            }

            CurrentUser = username;
            IsLoggedIn = true;
            IsAdmin = user.isAdmin;
            CanCrypt = user.isAdmin || user.canCrypt; // Admin a toujours accès
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erreur Login:\n{ex.Message}", "Error");
            return false;
        }
    }

    // ============================
    // LOGOUT
    // ============================
    public static void Logout()
    {
        CurrentUser = "";
        IsLoggedIn = false;
        IsAdmin = false;
        CanCrypt = false;
    }

    // ============================
    // GET CURRENT USER
    // ============================
    public static async Task<User?> GetCurrentUserAsync()
    {
        try
        {
            return await _fb.Child("users").Child(CurrentUser).OnceSingleAsync<User>();
        }
        catch { return null; }
    }

    // ============================
    // ADMIN - GET ALL USERS
    // ============================
    public static async Task<List<User>> GetAllUsersAsync()
    {
        var users = new List<User>();
        try
        {
            var response = await _fb.Child("users").OnceAsync<User>();
            foreach (var item in response)
            {
                if (item.Object != null)
                {
                    item.Object.username = item.Key;
                    users.Add(item.Object);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erreur GetAll:\n{ex.Message}", "Error");
        }
        return users;
    }

    // ============================
    // ADMIN - APPROUVER
    // ============================
    public static async Task<bool> ApproveUserAsync(string username)
    {
        try
        {
            var user = await _fb.Child("users").Child(username).OnceSingleAsync<User>();
            if (user == null) return false;

            user.approved = true;
            await _fb.Child("users").Child(username).PutAsync(user);
            return true;
        }
        catch { return false; }
    }

    // ============================
    // ADMIN - DÉSAPPROUVER
    // ============================
    public static async Task<bool> UnapproveUserAsync(string username)
    {
        try
        {
            var user = await _fb.Child("users").Child(username).OnceSingleAsync<User>();
            if (user == null) return false;

            user.approved = false;
            await _fb.Child("users").Child(username).PutAsync(user);
            return true;
        }
        catch { return false; }
    }

    // ============================
    // ADMIN - SUPPRIMER
    // ============================
    public static async Task<bool> DeleteUserAsync(string username)
    {
        try
        {
            await _fb.Child("users").Child(username).DeleteAsync();
            return true;
        }
        catch { return false; }
    }

    // ============================
    // ADMIN - RESET PASSWORD
    // ============================
    public static async Task<bool> ResetPasswordAsync(string username, string newPassword)
    {
        try
        {
            var user = await _fb.Child("users").Child(username).OnceSingleAsync<User>();
            if (user == null) return false;

            user.password = newPassword;
            await _fb.Child("users").Child(username).PutAsync(user);
            return true;
        }
        catch { return false; }
    }

    // ============================
    // ADMIN - TOGGLE ADMIN
    // ============================
    public static async Task<bool> ToggleAdminAsync(string username)
    {
        try
        {
            var user = await _fb.Child("users").Child(username).OnceSingleAsync<User>();
            if (user == null) return false;

            user.isAdmin = !user.isAdmin;
            await _fb.Child("users").Child(username).PutAsync(user);
            return true;
        }
        catch { return false; }
    }

    // ============================
    // ADMIN - TOGGLE CRYPT
    // ============================
    public static async Task<bool> ToggleCryptAsync(string username)
    {
        try
        {
            var user = await _fb.Child("users").Child(username).OnceSingleAsync<User>();
            if (user == null) return false;

            user.canCrypt = !user.canCrypt;
            await _fb.Child("users").Child(username).PutAsync(user);
            return true;
        }
        catch { return false; }
    }
}
