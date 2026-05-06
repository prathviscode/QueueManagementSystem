using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using QueueManagementSystem.Data;
using QueueManagementSystem.Hubs;
using QueueManagementSystem.Models;
using System.Security.Claims;

namespace QueueManagementSystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TokenController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<QueueHub> _hub;

        public TokenController(AppDbContext context, IHubContext<QueueHub> hub)
        {
            _context = context;
            _hub = hub;
        }

        // ─── Helper: read counter number from JWT claims ──────────────────────
        private int GetCounterFromToken()
        {
            var counterClaim = User.Claims.FirstOrDefault(c => c.Type == "Counter");
            if (counterClaim != null && int.TryParse(counterClaim.Value, out int counter))
                return counter;
            return 0; // Admin or unassigned → 0
        }

        // ─── POST api/token/generate  (Admin only) ────────────────────────────
        [Authorize(Roles = "Admin")]
        [HttpPost("generate")]
        public async Task<IActionResult> GenerateToken()
        {
            // FIX: Max() throws on empty sequence – use DefaultIfEmpty(0) to avoid it.
            int nextNumber = _context.Tokens.Any()
                ? _context.Tokens.Max(t => t.TokenNumber) + 1
                : 1;

            var token = new Token
            {
                TokenNumber = nextNumber,
                Department = "General",
                Status = "Waiting",
                CreatedTime = DateTime.UtcNow, // FIX: Use UtcNow
                CounterNumber = null
            };

            _context.Tokens.Add(token);

            // FIX: Use async version to avoid blocking the thread pool
            await _context.SaveChangesAsync();

            // Notify all connected clients (display, staff, admin)
            await _hub.Clients.All.SendAsync("ReceiveUpdate");

            return Ok(new
            {
                message = "Token generated.",
                tokenNumber = token.TokenNumber
            });
        }

        // ─── POST api/token/next  (Admin + Staff) ─────────────────────────────
        [Authorize(Roles = "Admin,Staff")]
        [HttpPost("next")]
        public async Task<IActionResult> NextToken()
        {
            int counter = GetCounterFromToken();

            // FIX: counter == 0 means Admin without a counter assignment –
            // calling Next would assign a token to counter 0, which makes no
            // sense on the display. Reject such calls.
            if (counter == 0)
                return BadRequest(new { message = "No counter assigned to your account. Only Staff with a counter can call Next." });

            // Step 1: Mark the token currently serving at THIS counter as Done
            var current = _context.Tokens
                .FirstOrDefault(t => t.Status == "Serving" && t.CounterNumber == counter);

            if (current != null)
                current.Status = "Done";

            // Step 2: Pick the oldest waiting token
            var next = _context.Tokens
                .Where(t => t.Status == "Waiting")
                .OrderBy(t => t.TokenNumber)
                .FirstOrDefault();

            if (next == null)
            {
                // Still save the Done status update even if no next token
                await _context.SaveChangesAsync();
                await _hub.Clients.All.SendAsync("ReceiveUpdate");
                return Ok(new { message = "Queue is empty. No more tokens waiting." });
            }

            // Step 3: Assign token to this counter
            next.Status = "Serving";
            next.CounterNumber = counter;

            // FIX: Use async SaveChanges
            await _context.SaveChangesAsync();
            await _hub.Clients.All.SendAsync("ReceiveUpdate");

            return Ok(new
            {
                message = "Next token called.",
                tokenNumber = next.TokenNumber,
                counter
            });
        }

        // ─── POST api/token/reset  (Admin only) ───────────────────────────────
        [Authorize(Roles = "Admin")]
        [HttpPost("reset")]
        public async Task<IActionResult> ResetQueue()
        {
            _context.Tokens.RemoveRange(_context.Tokens);

            // FIX: Use async SaveChanges
            await _context.SaveChangesAsync();
            await _hub.Clients.All.SendAsync("ReceiveUpdate");

            return Ok(new { message = "Queue fully reset. All tokens deleted." });
        }

        // ─── GET api/token/all  (Public – no auth needed for display) ─────────
        [AllowAnonymous]
        [HttpGet("all")]
        public IActionResult GetAll()
        {
            var tokens = _context.Tokens
                .OrderBy(t => t.TokenNumber)
                .Select(t => new
                {
                    t.TokenNumber,
                    t.Status,
                    t.CounterNumber
                })
                .ToList();

            return Ok(tokens);
        }

        // ─── GET api/token/current  (Public – display board) ─────────────────
        [AllowAnonymous]
        [HttpGet("current")]
        public IActionResult GetCurrent()
        {
            var current = _context.Tokens
                .Where(t => t.Status == "Serving")
                .OrderBy(t => t.CounterNumber)
                .Select(t => new
                {
                    t.TokenNumber,
                    t.CounterNumber
                })
                .ToList();

            return Ok(current);
        }
    }
}
