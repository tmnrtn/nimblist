using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nimblist.api.DTO;
using Nimblist.Data;
using Nimblist.Data.Models;
using System.Security.Claims;

namespace Nimblist.api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class FamiliesController : ControllerBase
    {
        private readonly NimblistContext _context;

        public FamiliesController(NimblistContext context)
        {
            _context = context;
        }

        private static FamilyWithMembersDto ToDto(Family family) =>
            new(
                family.Id,
                family.Name,
                family.UserId,
                family.Members
                    .Select(m => new FamilyMemberDetailDto(
                        m.Id, m.UserId, m.User?.Email, m.Role, m.JoinedAt, m.UserId == family.UserId))
                    .ToList()
            );

        [HttpGet]
        public async Task<ActionResult<IEnumerable<FamilyWithMembersDto>>> GetFamilies()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var families = await _context.Families
                .Where(f => f.UserId == userId)
                .Include(f => f.Members).ThenInclude(m => m.User)
                .OrderBy(f => f.Name)
                .ToListAsync();

            return Ok(families.Select(ToDto));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<FamilyWithMembersDto>> GetFamily(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var family = await _context.Families
                .Include(f => f.Members).ThenInclude(m => m.User)
                .FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);

            if (family == null) return NotFound();
            return Ok(ToDto(family));
        }

        [HttpPost]
        public async Task<ActionResult<FamilyWithMembersDto>> PostFamily(FamilyInputDto familyDto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var family = new Family { Name = familyDto.Name, UserId = userId };
            _context.Families.Add(family);
            await _context.SaveChangesAsync();

            _context.FamilyMembers.Add(new FamilyMember { FamilyId = family.Id, UserId = userId });
            await _context.SaveChangesAsync();

            var created = await _context.Families
                .Include(f => f.Members).ThenInclude(m => m.User)
                .FirstOrDefaultAsync(f => f.Id == family.Id);

            return CreatedAtAction(nameof(GetFamily), new { id = family.Id }, ToDto(created!));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutFamily(Guid id, FamilyUpdateDto familyDto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var existing = await _context.Families.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);
            if (existing == null) return NotFound();

            existing.Name = familyDto.Name;
            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { return Conflict(); }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFamily(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var family = await _context.Families.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);
            if (family == null) return NotFound();

            _context.Families.Remove(family);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
