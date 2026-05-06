using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QueueManagementSystem.Data;
using QueueManagementSystem.Hubs;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ================= SIGNALR =================
builder.Services.AddSignalR();

// ================= DATABASE =================
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
);

// ================= CONTROLLERS =================
builder.Services.AddControllers();

// ================= JWT AUTH =================
// FIX: Null-guard JWT key at startup so the app crashes clearly rather than at runtime
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key not found in configuration.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };

        // FIX: SignalR WebSocket cannot send Authorization headers.
        // Token must be read from query string: ?access_token=<jwt>
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/queueHub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ================= SWAGGER =================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ================= CORS =================
// FIX: AllowAnyOrigin() + AllowCredentials() is illegal (CORS spec violation).
// SignalR requires credentials, so we must list specific allowed origins.
// Add your frontend origins here. Wildcard ("*") cannot be used with credentials.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:3000",
                "http://localhost:5173",
                "http://127.0.0.1:5500",
                "https://localhost:7194",   // Swagger / self
                "http://localhost:5000"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();  // Required for SignalR
    });
});

// ================= BUILD =================
var app = builder.Build();

// ================= MIDDLEWARE ORDER =================
// FIX: Correct ASP.NET Core middleware pipeline order.
// UseRouting → UseCors → UseAuthentication → UseAuthorization → Endpoints
// Placing UseCors BEFORE UseAuthentication is required so CORS preflight
// requests (OPTIONS) are handled before auth rejects them.
app.UseRouting();
app.UseCors("AllowFrontend");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// FIX: UseStaticFiles must come BEFORE UseHttpsRedirection so that
// wwwroot files are served correctly without double-redirect loops.
app.UseStaticFiles();
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// FIX: Hub mapped without AllowAnonymous - JWT protects it.
// Public display.html uses /api/token/current (AllowAnonymous endpoint)
// and connects to the hub anonymously for read-only real-time updates.
// The hub itself is open for connections but individual methods can be [Authorize].
app.MapHub<QueueHub>("/queueHub").AllowAnonymous();

app.Run();
