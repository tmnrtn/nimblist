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
    public class FamilyMembersController : ControllerBase
    {
        private readonly NimblistContext _context;

        public FamilyMembersController(NimblistContext context)
        {
            _context = context;
        }

        private static FamilyMemberDetailDto ToDto(FamilyMember m, string ownerId) =>
            new(m.Id, m.UserId, m.User?.Email, m.Role, m.JoinedAt, m.UserId == ownerId);

        [HttpPost]
        public async Task<ActionResult<FamilyMemberDetailDto>> PostFamilyMember(FamilyMemberInputDto dto)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

            var family = await _context.Families.FirstOrDefaultAsync(f => f.Id == dto.FamilyId);
            if (family == null) return NotFound("Family not found.");
            if (family.UserId != currentUserId) return Forbid();

            var userToAdd = await _context.Users.FindAsync(dto.UserIdToAdd);
            if (userToAdd == null) return BadRequest("User not found.");

            var alreadyMember = await _context.FamilyMembers
                .AnyAsync(fm => fm.FamilyId == dto.FamilyId && fm.UserId == dto.UserIdToAdd);
            if (alreadyMember) return Conflict("User is already a member.");

            var member = new FamilyMember { FamilyId = dto.FamilyId, UserId = dto.UserIdToAdd };
            _context.FamilyMembers.Add(member);
            await _context.SaveChangesAsync();

            await _context.Entry(member).Reference(m => m.User).LoadAsync();
            return CreatedAtAction(nameof(GetFamilyMember), new { id = member.Id }, ToDto(member, family.UserId));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFamilyMember(Guid id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

            var member = await _context.FamilyMembers
                .Include(m => m.Family)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (member == null) return NotFound();
            if (member.Family!.UserId != currentUserId && member.UserId != currentUserId) return Forbid();

            _context.FamilyMembers.Remove(member);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<FamilyMemberDetailDto>> GetFamilyMember(Guid id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

            var member = await _context.FamilyMembers
                .Include(m => m.User)
                .Include(m => m.Family)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (member == null) return NotFound();

            bool isOwner = member.Family!.UserId == currentUserId;
            bool isSelf = member.UserId == currentUserId;
            bool isMember = await _context.FamilyMembers
                .AnyAsync(m => m.FamilyId == member.FamilyId && m.UserId == currentUserId);

            if (!isOwner && !isSelf && !isMember) return Forbid();

            return Ok(ToDto(member, member.Family.UserId));
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<FamilyMemberDetailDto>>> GetFamilyMembersForFamily([FromQuery] Guid familyId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

            var family = await _context.Families.FindAsync(familyId);
            if (family == null) return NotFound();

            bool isOwner = family.UserId == currentUserId;
            bool isMember = await _context.FamilyMembers.AnyAsync(m => m.FamilyId == familyId && m.UserId == currentUserId);
            if (!isOwner && !isMember) return Forbid();

            var members = await _context.FamilyMembers
                .Where(m => m.FamilyId == familyId)
                .Include(m => m.User)
                .ToListAsync();

            return Ok(members.Select(m => ToDto(m, family.UserId)));
        }
    }
}
