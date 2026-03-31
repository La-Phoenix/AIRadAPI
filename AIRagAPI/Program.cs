using AIRagAPI.Domain.Persistence;
using AIRagAPI.Exceptions;
using AIRagAPI.Extensions;
using AIRagAPI.Generators;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

//For only production
// if (!builder.Environment.IsDevelopment())
//     builder.WebHost.UseUrls("http://+:8080");

builder.Services.AddOpenApi();

builder.WebHost.ConfigureKestrel((context, options) =>
{
    options.AddServerHeader = false;
});

// Add Semantic Kernel
builder.Services.AddSingleton(sp =>
{
    var kernelBuilder = Kernel.CreateBuilder();
    
    // Groq (LLM)
    kernelBuilder.AddOpenAIChatCompletion(
        modelId: "llama-3.3-70b-versatile",
        apiKey: builder.Configuration["Groq:APiKey"],
        endpoint: new Uri(builder.Configuration["Groq:ApiEndpoint"] ?? "")
    );
    
    return kernelBuilder.Build();
});

// Register Gemini Embedding Generator and it's base address
// Add HttpClient for Gemini API
builder.Services.AddHttpClient<GeminiEmbeddingGenerator>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Gemini:BaseUrl"]!);
    client.DefaultRequestHeaders.Add("x-goog-api-key", builder.Configuration["Gemini:ApiKey"]);
    client.Timeout = TimeSpan.FromMinutes(30); // Prevent request from hanging when Gemini req hangs
});

// Configure DBContext
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});


// Configure header for Render deployment
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Configure data protection
// var dataProtectionBuilder = builder.Services.AddDataProtection().SetApplicationName("LittlePhoenix");

// if (builder.Environment.IsProduction())
// {
//     var keysDir = Path.Combine(Path.GetTempPath(), "littlephoenix-keys");
//     
//     Directory.CreateDirectory(keysDir);
//     dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(keysDir)).
//         SetDefaultKeyLifetime(TimeSpan.FromDays(90));
// }
// else
// {
//     var keysDir = Path.Combine(Directory.GetCurrentDirectory(), "keys");
//     Directory.CreateDirectory(keysDir);
//     dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(keysDir)).
//         SetDefaultKeyLifetime(TimeSpan.FromDays(30));
// }

builder.Services
    .AddDataProtection()
    .SetApplicationName("LittlePhoenix")
    .PersistKeysToDbContext<AppDbContext>()
    .SetDefaultKeyLifetime(TimeSpan.FromDays(90));
// Remove the if/else for file system — no longer needed

// Configure Auth
builder.Services.AddAuthentication(options =>
{
    // Check if user is authenticated by looking for a cookie
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    
    // When user hits a [Authorize] protected page, redirect them to google login
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
    
    // Store user's identity here after successful sign in
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    
}) 
.AddCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;

    options.Cookie.Name = "littlephoenix_auth"; // App Auth cookie name
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;

    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = builder.Environment.IsProduction()
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;
})
.AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
{
    options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.ClientId = builder.Configuration["Google:ClientId"]!;
    options.ClientSecret = builder.Configuration["Google:ClientSecret"]!;
    options.CallbackPath = "/api/auth/google/callback";

    options.SaveTokens = true;
    options.ClaimActions.MapJsonKey("picture", "picture", "url");

    options.CorrelationCookie.SameSite = SameSiteMode.None;
    options.CorrelationCookie.SecurePolicy = builder.Environment.IsProduction()
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;
    
    options.Events.OnRedirectToAuthorizationEndpoint = context =>
    {
        Console.WriteLine("Redirecting to: " + context.RedirectUri);
        return Task.CompletedTask;
    };

    options.Events.OnRemoteFailure = context =>
    {
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError("Google OAuth failed: {Error}", context.Failure?.Message);

        context.HandleResponse();
        context.Response.StatusCode = 500;
        return Task.CompletedTask;
    };
});

//Register Application Services
builder.Services.AddApplicationServices();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(builder.Configuration["Frontend:BaseUrl"]!);
        policy.AllowAnyHeader();
        policy.AllowAnyMethod();
        policy.AllowCredentials(); // For Cookies/auth
    });
});
builder.Services.AddHealthChecks();
var app = builder.Build();

// Should be first middelware
app.UseForwardedHeaders();

app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Scheme: {Scheme}, RemoteIp: {Ip}",
        context.Request.Scheme,
        context.Connection.RemoteIpAddress);
    await next();
});

app.UseSwagger();
app.UseSwaggerUI();
app.UseExceptionHandler();

if (app.Environment.IsProduction())
{
    app.Use(async (context, next) =>
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        await next();
    });

    app.UseHttpsRedirection();
}

// if (!app.Environment.IsEnvironment("Docker"))
// {
//     app.UseHttpsRedirection();
// }
// app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Request: {Method} {Path}", context.Request.Method, context.Request.Path);

    if (context.Request.Headers.TryGetValue("Cookie", out var cookie))
        logger.LogInformation("Cookies: {Cookies}", cookie.ToString());
    else
        logger.LogInformation("No cookies sent");

    await next();
});
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

//Apply migrations automatically on startup
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
        
    } catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
    }
}
app.Run();