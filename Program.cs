
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Add MVC services.
builder.Services.AddControllersWithViews();

// Configure cookie authentication.
builder.Services.AddAuthentication("StudentCookie")
    .AddCookie("StudentCookie", options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.Cookie.Name = "HostelManagementAuth";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

// Default route: open the Login page first.
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}")
    .WithStaticAssets();

// Test Neon database connection.
app.MapGet("/test-db", async (IConfiguration config) =>
{
    await using var connection = new NpgsqlConnection(
        config.GetConnectionString("DefaultConnection"));

    await connection.OpenAsync();

    return Results.Ok("Neon database connected successfully!");
});

// Create students table.
app.MapGet("/create-table", async (IConfiguration config) =>
{
    await using var connection = new NpgsqlConnection(
        config.GetConnectionString("DefaultConnection"));

    await connection.OpenAsync();

    const string sql = """
        CREATE TABLE IF NOT EXISTS students (
            id SERIAL PRIMARY KEY,
            name VARCHAR(100) NOT NULL,
            email VARCHAR(150) NOT NULL,
            phone VARCHAR(20),
            enrollment_number VARCHAR(50) UNIQUE NOT NULL,
            department VARCHAR(100),
            course VARCHAR(100)
        );
        """;

    await using var command = new NpgsqlCommand(sql, connection);
    await command.ExecuteNonQueryAsync();

    return Results.Ok("Students table created successfully!");
});

// Create rooms table.
app.MapGet("/create-rooms-table", async (IConfiguration config) =>
{
    await using var connection = new NpgsqlConnection(
        config.GetConnectionString("DefaultConnection"));

    await connection.OpenAsync();

    const string sql = """
        CREATE TABLE IF NOT EXISTS rooms (
            id SERIAL PRIMARY KEY,
            room_number VARCHAR(20) UNIQUE NOT NULL,
            room_type VARCHAR(50) NOT NULL,
            floor INTEGER NOT NULL,
            total_beds INTEGER NOT NULL
        );
        """;

    await using var command = new NpgsqlCommand(sql, connection);
    await command.ExecuteNonQueryAsync();

    return Results.Ok("Rooms table created successfully!");
});

// Create room allocations table.
app.MapGet("/create-room-allocations-table", async (IConfiguration config) =>
{
    await using var connection = new NpgsqlConnection(
        config.GetConnectionString("DefaultConnection"));

    await connection.OpenAsync();

    const string sql = """
        CREATE TABLE IF NOT EXISTS room_allocations (
            id SERIAL PRIMARY KEY,
            student_id INTEGER UNIQUE NOT NULL
                REFERENCES students(id),
            room_id INTEGER NOT NULL
                REFERENCES rooms(id),
            bed_number VARCHAR(20),
            allocation_date DATE NOT NULL DEFAULT CURRENT_DATE
        );
        """;

    await using var command = new NpgsqlCommand(sql, connection);
    await command.ExecuteNonQueryAsync();

    return Results.Ok("Room allocations table created successfully!");
});

// Create student accounts table.
app.MapGet("/create-accounts-table", async (IConfiguration config) =>
{
    await using var connection = new NpgsqlConnection(
        config.GetConnectionString("DefaultConnection"));

    await connection.OpenAsync();

    const string sql = """
        CREATE TABLE IF NOT EXISTS student_accounts (
            id SERIAL PRIMARY KEY,
            student_id INTEGER UNIQUE NOT NULL
                REFERENCES students(id) ON DELETE CASCADE,
            password_hash TEXT NOT NULL,
            created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
        );
        """;

    await using var command = new NpgsqlCommand(sql, connection);
    await command.ExecuteNonQueryAsync();

    return Results.Ok(
        "Student accounts table created successfully!");
});

// Create leave requests table.
app.MapGet("/create-leave-requests-table", async (IConfiguration config) =>
{
    await using var connection = new NpgsqlConnection(
        config.GetConnectionString("DefaultConnection"));

    await connection.OpenAsync();

    const string sql = """
        CREATE TABLE IF NOT EXISTS leave_requests (
            id SERIAL PRIMARY KEY,
            student_id INTEGER NOT NULL
                REFERENCES students(id) ON DELETE CASCADE,
            start_date DATE NOT NULL,
            end_date DATE NOT NULL,
            reason TEXT NOT NULL,
            status VARCHAR(20) NOT NULL DEFAULT 'Pending',
            submitted_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
            CONSTRAINT valid_leave_dates
                CHECK (end_date >= start_date)
        );
        """;

    await using var command = new NpgsqlCommand(sql, connection);
    await command.ExecuteNonQueryAsync();

    return Results.Ok("Leave requests table created successfully!");
});

app.Run();
