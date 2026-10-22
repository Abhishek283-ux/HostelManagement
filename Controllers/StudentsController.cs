
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Npgsql;
using HostelManagementSystem.Models;

namespace HostelManagementSystem.Controllers;

[Authorize(AuthenticationSchemes = "StudentCookie")]
public class StudentsController : Controller
{
    private readonly string _connectionString;

    public StudentsController(IConfiguration configuration)
    {
        _connectionString =
            configuration.GetConnectionString("DefaultConnection")!;
    }

    [HttpGet]
    public IActionResult Index() => View(new Student());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Student student)
    {
        await using var connection =
            new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = """
            INSERT INTO students
                (name, email, phone, enrollment_number, department, course)
            VALUES
                (@name, @email, @phone, @enrollment, @department, @course);
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("name", student.Name);
        command.Parameters.AddWithValue("email", student.Email);
        command.Parameters.AddWithValue(
            "phone", (object?)student.Phone ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "enrollment", student.EnrollmentNumber);
        command.Parameters.AddWithValue(
            "department", (object?)student.Department ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "course", (object?)student.Course ?? DBNull.Value);

        await command.ExecuteNonQueryAsync();
        return RedirectToAction(nameof(List));
    }

    [HttpGet]
    public IActionResult Dashboard() => View();

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var student = await GetLoggedInStudent();
        if (student == null)
            return NotFound("Student profile not found.");

        return View(student);
    }

    private async Task<Student?> GetLoggedInStudent()
    {
        var studentIdValue =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(studentIdValue, out var studentId))
            return null;

        await using var connection =
            new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = """
            SELECT id, name, email, phone,
                   enrollment_number, department, course
            FROM students
            WHERE id = @id
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", studentId);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return new Student
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            Email = reader.GetString(2),
            Phone = reader.IsDBNull(3) ? "" : reader.GetString(3),
            EnrollmentNumber = reader.GetString(4),
            Department = reader.IsDBNull(5) ? "" : reader.GetString(5),
            Course = reader.IsDBNull(6) ? "" : reader.GetString(6)
        };
    }

    [HttpGet]
    public IActionResult RoomDetails() => View((Room?)null);

    [HttpGet]
    public IActionResult Fees() => View();

    [HttpGet]
    public async Task<IActionResult> LeaveRequests()
    {
        var studentIdValue =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!long.TryParse(studentIdValue, out var studentId))
            return Challenge();

        var requests = new List<Dictionary<string, object>>();

        await using var connection =
            new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = """
            SELECT id, from_date, to_date, reason, status, created_at
            FROM leave_requests
            WHERE student_id = @studentId
            ORDER BY created_at DESC;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("studentId", studentId);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            requests.Add(new Dictionary<string, object>
            {
                ["Id"] = reader.GetInt64(0),
                ["StartDate"] = reader.GetDateTime(1),
                ["EndDate"] = reader.GetDateTime(2),
                ["Reason"] = reader.GetString(3),
                ["Status"] = reader.IsDBNull(4)
                    ? "Pending"
                    : reader.GetString(4),
                ["SubmittedAt"] = reader.GetDateTime(5)
            });
        }

        return View(requests);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitLeaveRequest(
        DateTime startDate,
        DateTime endDate,
        string reason)
    {
        var studentIdValue =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!long.TryParse(studentIdValue, out var studentId))
            return Challenge();

        if (startDate == default ||
            endDate == default ||
            endDate.Date < startDate.Date ||
            string.IsNullOrWhiteSpace(reason))
        {
            TempData["LeaveError"] =
                "Please enter valid dates and a reason for your leave.";

            return RedirectToAction(nameof(LeaveRequests));
        }

        await using var connection =
            new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = """
            INSERT INTO leave_requests
                (student_id, from_date, to_date, reason, status)
            VALUES
                (@studentId, @startDate, @endDate, @reason, 'Pending');
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("studentId", studentId);
        command.Parameters.AddWithValue("startDate", startDate.Date);
        command.Parameters.AddWithValue("endDate", endDate.Date);
        command.Parameters.AddWithValue("reason", reason.Trim());

        await command.ExecuteNonQueryAsync();

        TempData["LeaveSuccess"] =
            "Your leave request has been submitted successfully.";

        return RedirectToAction(nameof(LeaveRequests));
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var students = new List<Student>();

        await using var connection =
            new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = """
            SELECT id, name, email, phone,
                   enrollment_number, department, course
            FROM students
            ORDER BY id DESC;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            students.Add(new Student
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Email = reader.GetString(2),
                Phone = reader.IsDBNull(3) ? "" : reader.GetString(3),
                EnrollmentNumber = reader.GetString(4),
                Department = reader.IsDBNull(5) ? "" : reader.GetString(5),
                Course = reader.IsDBNull(6) ? "" : reader.GetString(6)
            });
        }

        return View(students);
    }
}
