using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using QueueManagementSystem.Data;
using QueueManagementSystem.Hubs;
using QueueManagementSystem.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace QueueManagementSystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly IHubContext<QueueHub> _hub;

        public AuthController(AppDbContext context, IConfiguration config, IHubContext<QueueHub> hub)
        {
            _context = context;
            _config = config;
            _hub = hub;
        }

        // POST api/auth/register
        [HttpPost("register")]
        public IActionResult Register([FromBody] User user)
        {
            // FIX: Validate required fields first
            if (string.IsNullOrWhiteSpace(user.Username) || string.IsNullOrWhiteSpace(user.Password))
                return BadRequest(new { message = "Username and Password are required." });

            // FIX: Role validation – reject anything other than known roles
            if (user.Role != "Admin" && user.Role != "Staff")
                return BadRequest(new { message = "Role must be 'Admin' or 'Staff'." });

            // FIX: Staff without a counter is a data integrity error
            if (user.Role == "Staff" && user.CounterNumber == null)
                return BadRequest(new { message = "Staff must have a counter number." });

            // FIX: Duplicate check (DB also enforces unique index as a safety net)
            if (_context.Users.Any(u => u.Username == user.Username))
                return Conflict(new { message = "Username already exists." });

            // TODO (Production): Hash the password before saving.
            // user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);

            _context.Users.Add(user);
            _context.SaveChanges();

            return Ok(new { message = "User registered successfully." });
        }

        // POST api/auth/login
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { message = "Username and Password are required." });

            // FIX: Use string comparison that works correctly; avoid null reference warnings
            var user = _context.Users
                .FirstOrDefault(u =>
                    u.Username == request.Username &&
                    u.Password == request.Password);

            if (user == null)
                return Unauthorized(new { message = "Invalid username or password." });

            var token = GenerateJwtToken(user);

            return Ok(new
            {
                token,
                role = user.Role,
                counter = user.CounterNumber
            });
        }

        // GET api/auth/profile  (requires valid JWT)
        [Authorize]
        [HttpGet("profile")]
        public IActionResult GetProfile()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
                return Unauthorized(new { message = "Token identity missing." });

            var user = _context.Users.FirstOrDefault(u => u.Username == username);
            if (user == null)
                return NotFound(new { message = "User not found." });

            return Ok(new
            {
                user.Id,
                user.Username,
                user.Role,
                user.CounterNumber
            });
        }

        // ─── Private Helpers ──────────────────────────────────────────────────

        private string GenerateJwtToken(User user)
        {
            // FIX: Crash at startup if missing (guarded in Program.cs), but
            // double-check here for safety and to satisfy nullable analysis.
            var rawKey = _config["Jwt:Key"]
                ?? throw new InvalidOperationException("JWT Key is not configured.");

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(rawKey));
            var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            // FIX: CounterNumber can be null (Admin has no counter), so we
            // safely convert to string; "0" is used as the sentinel for Admin.
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("Counter", user.CounterNumber?.ToString() ?? "0")
            };

            var tokenDescriptor = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(2), // FIX: Use UtcNow
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
        }
    }
}
