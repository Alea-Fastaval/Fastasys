using System.Text;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Fastasys.ApiService.helpers;

using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsEnvironment("Testing"))
{
    builder.Logging.ClearProviders();
}

builder.Services.AddHealthChecks();

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<AmountPaidLogInterceptor>();

builder.Services.AddDbContext<InfosysDbContext>((services, options) =>
{
    // Logs every saved change to a participant's amount paid
    options.AddInterceptors(services.GetRequiredService<AmountPaidLogInterceptor>());

    if (builder.Environment.IsEnvironment("Testing"))
    {
        options.UseInMemoryDatabase("InfosysDbFallback");
        return;
    }

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("ConnectionStrings:DefaultConnection must be configured outside the Testing environment.");
    }

    options.UseNpgsql(connectionString);
});

// The convention's dates (Infosys con.*)
builder.Services.Configure<ConventionOptions>(builder.Configuration.GetSection(ConventionOptions.Section));

// The clock, so tests can set the time (used by the signup's opening)
builder.Services.AddSingleton(TimeProvider.System);

// Register custom services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<KarmaService>();
builder.Services.AddSingleton<IMockPaymentService, MockPaymentService>();
builder.Services.AddSingleton<IMockNotificationService, MockNotificationService>();

// JWT Authentication Setup
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "FastasysSuperSecretSecurityKey2026!FormatLengthForHS256";
var key = Encoding.UTF8.GetBytes(jwtSecret);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false,
        ClockSkew = TimeSpan.Zero
    };
});

// Authorization Policies
// There should be a binding between every Privilege in the DBseeder, and a policy name this builder
builder.Services.AddAuthorizationBuilder()
    // Infosys: the Read-only role may view and search participants; Infonaut, Participant admin and the other
    // roles with ParticipantController/* may also change them
    .AddPolicy("ParticipantView", "participant_view", "participant_edit")
    .AddPolicy("ParticipantManagement", "participant_edit")
    .AddPolicy("ActivityView", "activity_view", "activity_edit")
    .AddPolicy("ActivityManagement", "activity_edit")
    // Opening the signup; later also editing the signup form (agreed 2026-11-24)
    .AddPolicy("SignupManagement", "signup_form_edit")
    // Infosys: the Indgang pages (EntranceController/*) are for superuser and Infonaut; any role with
    // ParticipantController/* may change a participant's tickets. Fastasys uses its entrance privileges for them.
    .AddPolicy("TicketTypeView", "entrance_view", "entrance_edit")
    .AddPolicy("TicketTypeManagement", "entrance_edit")
    .AddPolicy("ParticipantTicketManagement", "entrance_participant_edit")
    // Infosys: the Mad pages (FoodController) are for roles with FoodController/*, and only Infonaut and admin may
    // create, change and delete food types or change a participant's meals. Fastasys uses its food privileges.
    .AddPolicy("FoodView", "food_view", "food_edit")
    .AddPolicy("FoodManagement", "food_edit")
    .AddPolicy("FoodStatsView", "food_stats_view", "food_view", "food_edit")
    .AddPolicy("ParticipantFoodManagement", "food_participant_edit")
    .AddPolicy("HeroForceManagement", "hero_force_management")
    .AddPolicy("ShopManagement", "shop_management");

// Add Controllers, EndpointsApiExplorer & SwaggerGen documentation metadata
builder.Services.AddControllers()
    .AddApplicationPart(typeof(Fastasys.ApiService.Controllers.ParticipantsController).Assembly);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddProblemDetails();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "Fastasys API",
            Version = "v1",
            Description = "Fastaval Convention Management System - ASP.NET Core Web API"
        };

        var securityScheme = new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Description = "Enter JWT Bearer token format: `Bearer {your_token}`",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        };

        document.Components ??= new OpenApiComponents();
        var schemes = document.Components.SecuritySchemes ?? new Dictionary<string, IOpenApiSecurityScheme>();
        schemes["Bearer"] = securityScheme;
        document.Components.SecuritySchemes = schemes;

        return Task.CompletedTask;
    });
});

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Fastasys API",
        Version = "v1",
        Description = "Fastaval Convention Management System - ASP.NET Core Web API"
    });

    var jwtSecurityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter JWT Bearer token format: `Bearer {your_token}`",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    };

    options.AddSecurityDefinition("Bearer", jwtSecurityScheme);
    options.AddSecurityRequirement((doc) => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer"), new List<string>() }
    });



});

var app = builder.Build();

// Configure HTTP pipeline
app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

// Map controllers first so endpoint routing metadata is available
app.MapControllers();
app.MapHealthChecks("/health");

// Enable Swagger JSON & OpenAPI
app.UseSwagger();
app.MapOpenApi();

// Modern Scalar API Reference UI at /scalar/v1
app.MapScalarApiReference(options =>
{
    options.Title = "Fastasys API Reference";
    options.Theme = ScalarTheme.Purple;
    options.WithOpenApiRoutePattern("/swagger/v1/swagger.json");
});

// Classic Swagger UI at /swagger
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Fastasys API v1");
    options.RoutePrefix = "swagger";
});


// Friendly redirects so visiting /, /scalar, or /docs takes you straight to Scalar UI
app.MapGet("/", () => Results.Redirect("/scalar/v1"));
app.MapGet("/scalar", () => Results.Redirect("/scalar/v1"));
app.MapGet("/docs", () => Results.Redirect("/scalar/v1"));


if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<InfosysDbContext>();
    await db.Database.MigrateAsync();
    DbSeeder.Seed(db);
}

app.Run();

namespace Fastasys.ApiService
{
    public partial class Program { }
}
