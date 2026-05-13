using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using MyMvcApp.Data;
using MyMvcApp.Models;
using MyMvcApp.Services;

// Fix for Npgsql 6.0+ DateTime behavior
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register HttpClient
builder.Services.AddHttpClient();

// Register DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString)
           .UseSnakeCaseNamingConvention());

// Register services
builder.Services.AddScoped<JudgeService>();
builder.Services.AddScoped<CodeforcesService>();

// Add session services
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(7);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".CodeArena.Session";
});

// IMPORTANT: Add Authentication with Cookie scheme
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Home/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

var app = builder.Build();

// Apply migrations and seed admin user
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    try
    {
        // Apply any pending EF Core migrations instead of executing raw SQL
        context.Database.Migrate();
    }
    catch (Exception ex)
    {
        Console.WriteLine("Migration warning: " + ex.Message);
    }

    // Ensure user statistic fields are not NULL by updating via EF
    try
    {
        var usersWithNullProblems = context.Users
            .Where(u => EF.Property<int?>(u, nameof(User.ProblemsSolved)) == null)
            .ToList();
        foreach (var u in usersWithNullProblems) u.ProblemsSolved = 0;

        var usersWithNullContests = context.Users
            .Where(u => EF.Property<int?>(u, nameof(User.ContestsParticipated)) == null)
            .ToList();
        foreach (var u in usersWithNullContests) u.ContestsParticipated = 0;

        var usersWithNullPoints = context.Users
            .Where(u => EF.Property<int?>(u, nameof(User.TotalPoints)) == null)
            .ToList();
        foreach (var u in usersWithNullPoints) u.TotalPoints = 0;

        if (usersWithNullProblems.Any() || usersWithNullContests.Any() || usersWithNullPoints.Any())
        {
            context.SaveChanges();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("User stats migration warning: " + ex.Message);
    }

    if (!context.Users.Any(u => u.Username == "admin"))
    {
        string HashPassword(string password)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        var seedAdmin = new User
        {
            FirstName = "System",
            LastName = "Admin",
            Username = "admin",
            Email = "admin@example.com",
            PasswordHash = HashPassword("pass123"),
            Role = "Admin",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            StudentId = "ADMIN_ID_000"
        };
        context.Users.Add(seedAdmin);
        context.SaveChanges();
    }

    SeedDemoProblems(context);
}

void SeedDemoProblems(AppDbContext context)
{
    if (context.Problems.Any(p => p.Source == "demo")) return;

    // 1. A + B Problem
    var p1 = new Problem
    {
        Title = "A + B Problem",
        Description = "Read two integers A and B from standard input and output their sum.",
        InputFormat = "Two integers A and B separated by space.",
        OutputFormat = "A single integer: the sum of A and B.",
        SampleInput = "5 3",
        SampleOutput = "8",
        Constraints = "-10^9 <= A, B <= 10^9",
        Difficulty = 1,
        Points = 10,
        Category = "Basic",
        TimeLimit = 1,
        MemoryLimit = 256000,
        CreatedAt = DateTime.UtcNow,
        IsActive = true,
        Source = "demo",
        SourceId = "demo1"
    };
    context.Problems.Add(p1);
    context.SaveChanges();

    context.TestCases.AddRange(
        new TestCase { ProblemId = p1.Id, Input = "5 3", ExpectedOutput = "8", IsSample = true, Order = 1, Points = 5 },
        new TestCase { ProblemId = p1.Id, Input = "10 20", ExpectedOutput = "30", IsSample = false, Order = 2, Points = 5 },
        new TestCase { ProblemId = p1.Id, Input = "-5 5", ExpectedOutput = "0", IsSample = false, Order = 3, Points = 5 }
    );

    // 2. Primality Test
    var p2 = new Problem
    {
        Title = "Primality Test",
        Description = "Given an integer N, determine if it is a prime number.",
        InputFormat = "A single integer N.",
        OutputFormat = "Output 'YES' if N is prime, otherwise 'NO'.",
        SampleInput = "7",
        SampleOutput = "YES",
        Constraints = "1 <= N <= 10^9",
        Difficulty = 2,
        Points = 30,
        Category = "Mathematics",
        TimeLimit = 1,
        MemoryLimit = 128000,
        CreatedAt = DateTime.UtcNow,
        IsActive = true,
        Source = "demo",
        SourceId = "demo2"
    };
    context.Problems.Add(p2);
    context.SaveChanges();

    context.TestCases.AddRange(
        new TestCase { ProblemId = p2.Id, Input = "7", ExpectedOutput = "YES", IsSample = true, Order = 1, Points = 10 },
        new TestCase { ProblemId = p2.Id, Input = "4", ExpectedOutput = "NO", IsSample = false, Order = 2, Points = 10 },
        new TestCase { ProblemId = p2.Id, Input = "999999937", ExpectedOutput = "YES", IsSample = false, Order = 3, Points = 10 }
    );

    context.SaveChanges();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// IMPORTANT: Order matters - Authentication before Authorization
app.UseAuthentication();  // This must come before UseAuthorization
app.UseAuthorization();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();