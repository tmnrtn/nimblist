using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nimblist.Data;
using Nimblist.Data.Models;
using System.Security.Claims;

namespace Nimblist.api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PushSubscriptionsController : ControllerBase
    {
        private readonly NimblistContext _context;

        public PushSubscriptionsController(NimblistContext context)
        {
            _context = context;
        }

        public record SubscriptionDto(string Endpoint, SubscriptionKeysDto Keys);
        public record SubscriptionKeysDto(string P256dh, string Auth);

        [HttpPost]
        public async Task<IActionResult> Subscribe([FromBody] SubscriptionDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var existing = await _context.PushSubscriptions
                .FirstOrDefaultAsync(s => s.Endpoint == dto.Endpoint);

            if (existing != null)
            {
                existing.P256dh = dto.Keys.P256dh;
                existing.Auth = dto.Keys.Auth;
            }
            else
            {
                _context.PushSubscriptions.Add(new UserPushSubscription
                {
                    UserId = userId,
                    Endpoint = dto.Endpoint,
                    P256dh = dto.Keys.P256dh,
                    Auth = dto.Keys.Auth,
                });
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete]
        public async Task<IActionResult> Unsubscribe([FromBody] SubscriptionDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var sub = await _context.PushSubscriptions
                .FirstOrDefaultAsync(s => s.UserId == userId && s.Endpoint == dto.Endpoint);

            if (sub != null)
            {
                _context.PushSubscriptions.Remove(sub);
                await _context.SaveChangesAsync();
            }

            return NoContent();
        }
    }
}
