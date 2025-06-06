using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nimblist.Data;
using Nimblist.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Nimblist.api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PreviousItemNamesController : ControllerBase
    {
        private readonly NimblistContext _context;

        public PreviousItemNamesController(NimblistContext context)
        {
            _context = context;
        }

        // GET: api/PreviousItemNames
        [HttpGet]
        public async Task<ActionResult<IEnumerable<string>>> GetPreviousItemNames()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID claim not found.");
            var names = await _context.PreviousItemNames
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.LastUsed)
                .Select(p => p.Name)
                .ToListAsync();
            return Ok(names);
        }

        // DELETE: api/PreviousItemNames/{name}
        [HttpDelete("{name}")]
        public async Task<IActionResult> DeletePreviousItemName(string name)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID claim not found.");
            var prevName = await _context.PreviousItemNames
                .FirstOrDefaultAsync(p => p.UserId == userId && p.Name == name);
            if (prevName == null)
            {
                return NotFound();
            }
            _context.PreviousItemNames.Remove(prevName);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
