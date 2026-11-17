using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json.Serialization;
using FinTrack.Api.Identity;
using FinTrack.Api.WriteTransactions;
using FinTrack.ModuleContracts;
using FinTrack.Modules.CashInflows.Endpoints;
using FinTrack.Modules.CreditCards.Endpoints;
using FinTrack.Modules.Debts.Endpoints;
using FinTrack.Modules.Expenses.Endpoints;
using FinTrack.Modules.Reporting.Endpoints;
using FinTrack.SharedKernel;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

var jwtOptions = JwtOptions.Resolve(builder.Configuration, builder.Environment);

var connectionString = builder.Configuration.GetConnectionString("FinTrackDb");
if (string.IsNullOrWhiteSpace(connectionString))
{
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "ConnectionStrings:FinTrackDb yapılandırılmalıdır. Bağlantı dizesini ortam değişkeni olarak verin.");
    }

    connectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=FinTrackDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";
}

builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Gövde bağlama hataları (geçersiz JSON, bilinmeyen alan) ortamdan bağımsız
// olarak 400 döner. Development varsayılanı istisna fırlatıp 500'e dönüşür.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<AuthTokenService>();

// ADR-0009: yazma yapan modüller aynı bağlantıyı ve açık transaction'ı paylaşır.
builder.Services.AddScoped<IWriteConnectionProvider>(_ => new SqlWriteConnectionProvider(connectionString));
builder.Services.AddScoped<IWriteTransactionScopeFactory, SqlWriteTransactionScopeFactory>();

builder.Services.AddDbContext<FinTrackIdentityDbContext>(options =>
    IdentityDbContextOptions.UseFinTrackSqlServer(options, connectionString));

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequiredLength = AuthValidation.PasswordMinLength;
        options.Password.RequiredUniqueChars = 1;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;

        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = false;
        options.Lockout.AllowedForNewUsers = false;
        options.ClaimsIdentity.UserIdClaimType = JwtRegisteredClaimNames.Sub;
    })
    .AddEntityFrameworkStores<FinTrackIdentityDbContext>()
    .AddDefaultTokenProviders();

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = "role",
        };

        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                if (context.Response.HasStarted)
                {
                    return;
                }

                context.HandleResponse();
                await AuthProblems.Unauthorized(context.HttpContext).ExecuteAsync(context.HttpContext);
            },
            OnForbidden = async context =>
                await AuthProblems.Forbidden(context.HttpContext).ExecuteAsync(context.HttpContext),
        };
    });

builder.Services.AddAuthorization();

// Modüller kendi servislerini kendileri kaydeder. Host, modül iç tiplerini görmez.
builder.Services
    .AddExpensesModule(connectionString)
    .AddCashInflowsModule(connectionString)
    .AddCreditCardsModule(connectionString)
    .AddDebtsModule(connectionString)
    .AddReportingModule();

var app = builder.Build();

var migrateOnly = args.Contains("--migrate", StringComparer.OrdinalIgnoreCase);
if (migrateOnly || app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var identityDb = scope.ServiceProvider.GetRequiredService<FinTrackIdentityDbContext>();
    await identityDb.Database.MigrateAsync();

    // Modül migration'ları kendi DbContext'leri üzerinden, kayıt sırasıyla uygulanır.
    foreach (var moduleMigration in scope.ServiceProvider.GetServices<IModuleMigration>())
    {
        await moduleMigration.MigrateAsync(CancellationToken.None);
    }
}

if (migrateOnly)
{
    await app.DisposeAsync();
    return;
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapHealthChecks("/health");

app.MapAuthEndpoints();
app.MapExpensesEndpoints();
app.MapCashInflowsEndpoints();
app.MapCreditCardsEndpoints();
app.MapDebtsEndpoints();
app.MapReportingEndpoints();

await app.RunAsync();

/// <summary>
/// <c>WebApplicationFactory</c> tarafından kullanılabilmesi için erişilebilir olmalıdır.
/// </summary>
public partial class Program;
