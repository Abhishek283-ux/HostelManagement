
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using Npgsql;

namespace HostelManagementSystem.Controllers;

public class AccountController : Controller
{
    private readonly string _connectionString;
    private readonly PasswordHasher<string> _passwordHasher = new();

    public AccountController(IConfiguration configuration)
    {
        _connectionString =
            configuration.GetConnectionString("DefaultConnection")!;
    }

    [HttpGet]
    public IActionResult Login() => View();

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(
        string name,
        string email,
        string phone,
        string enrollmentNumber,
        string department,
        string course,
        string password,
        string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(enrollmentNumber) ||
            string.IsNullOrWhiteSpace(password))
        {
            ViewBag.Error = "Please fill in all required fields.";
            return View();
        }

        if (password.Length < 8)
        {
            ViewBag.Error = "Password must contain at least 8 characters.";
            return View();
        }

        if (password != confirmPassword)
        {
            ViewBag.Error = "Passwords do not match.";
            return View();
        }

        await using var connection =
            new NpgsqlConnection(_connectionString);

        await connection.OpenAsync();
        await using var transaction =
            await connection.BeginTransactionAsync();

        try
        {
            const string studentSql = """
                INSERT INTO students
                    (name, email, phone, enrollment_number,
                     department, course)
                VALUES
                    (@name, @email, @phone, @enrollment,
                     @department, @course)
                RETURNING id;
                """;

            int studentId;

            await using (var command =
                new NpgsqlCommand(studentSql, connection, transaction))
            {
                command.Parameters.AddWithValue("name", name.Trim());
                command.Parameters.AddWithValue("email", email.Trim());
                command.Parameters.AddWithValue(
                    "phone", (object?)phone ?? "");
                command.Parameters.AddWithValue(
                    "enrollment", enrollmentNumber.Trim());
                command.Parameters.AddWithValue(
                    "department", (object?)department ?? "");
                command.Parameters.AddWithValue(
                    "course", (object?)course ?? "");

                studentId = (int)(await command.ExecuteScalarAsync())!;
            }

            string passwordHash =
                _passwordHasher.HashPassword(email.Trim(), password);

            const string accountSql = """
                INSERT INTO student_accounts
                    (student_id, password_hash)
                VALUES
                    (@studentId, @passwordHash);
                """;

            await using (var command =
                new NpgsqlCommand(accountSql, connection, transaction))
            {
                command.Parameters.AddWithValue("studentId", studentId);
                command.Parameters.AddWithValue(
                    "passwordHash", passwordHash);

                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();

            TempData["Message"] =
                "Registration successful. Please log in.";

            return RedirectToAction(nameof(Login));
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            await transaction.RollbackAsync();
            ViewBag.Error =
                "This email or enrollment number is already registered.";
            return View();
        }
        catch
        {
            await transaction.RollbackAsync();
            ViewBag.Error =
                "Registration failed. Please check your details and try again.";
            return View();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            ViewBag.Error = "Enter your login details.";
            return View();
        }

        await using var connection =
            new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = """
            SELECT s.id, s.name, s.email, a.password_hash
            FROM students s
            JOIN student_accounts a ON a.student_id = s.id
            WHERE LOWER(s.email) = LOWER(@login)
               OR LOWER(s.enrollment_number) = LOWER(@login)
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("login", email.Trim());

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            ViewBag.Error = "Invalid login details.";
            return View();
        }

        var studentId = reader.GetInt32(0);
        var studentName = reader.GetString(1);
        var studentEmail = reader.GetString(2);
        var passwordHash = reader.GetString(3);

        var result = _passwordHasher.VerifyHashedPassword(
            studentEmail, passwordHash, password);

        if (result == PasswordVerificationResult.Failed)
        {
            ViewBag.Error = "Invalid login details.";
            return View();
        }

        await reader.CloseAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, studentId.ToString()),
            new(ClaimTypes.Name, studentName),
            new(ClaimTypes.Email, studentEmail)
        };

        var identity = new ClaimsIdentity(claims, "StudentCookie");
        await HttpContext.SignInAsync(
            "StudentCookie", new ClaimsPrincipal(identity));

        return RedirectToAction("Dashboard", "Students");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("StudentCookie");
        return RedirectToAction(nameof(Login));
    }
}
