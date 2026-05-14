using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nimblist.api.DTO;
using Nimblist.Data;
using Nimblist.Data.Models;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Nimblist.api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TagsController : ControllerBase
    {
        private readonly NimblistContext _context;

        public TagsController(NimblistContext context)
        {
            _context = context;
        }

        // GET /api/tags
        [HttpGet]
        public async Task<ActionResult<List<TagDto>>> GetTags()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var tags = await _context.Tags
                .Where(t => t.UserId == userId)
                .OrderBy(t => t.Name)
                .Select(t => new TagDto(t.Id, t.Name, t.Color))
                .ToListAsync();

            return Ok(tags);
        }

        // POST /api/tags
        [HttpPost]
        public async Task<ActionResult<TagDto>> CreateTag([FromBody] TagInputDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { error = "Tag name is required." });

            // Prevent duplicate names for this user
            var exists = await _context.Tags.AnyAsync(t => t.UserId == userId && t.Name == dto.Name.Trim());
            if (exists) return Conflict(new { error = "A tag with this name already exists." });

            var tag = new Tag
            {
                Id = Guid.NewGuid(),
                Name = dto.Name.Trim(),
                Color = dto.Color?.Trim(),
                UserId = userId,
            };

            _context.Tags.Add(tag);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetTags), new TagDto(tag.Id, tag.Name, tag.Color));
        }

        // PUT /api/tags/{id}
        [HttpPut("{id}")]
        public async Task<ActionResult<TagDto>> UpdateTag(Guid id, [FromBody] TagInputDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var tag = await _context.Tags.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
            if (tag == null) return NotFound();

            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { error = "Tag name is required." });

            // Prevent duplicate names (excluding this tag)
            var duplicate = await _context.Tags.AnyAsync(t => t.UserId == userId && t.Name == dto.Name.Trim() && t.Id != id);
            if (duplicate) return Conflict(new { error = "A tag with this name already exists." });

            tag.Name = dto.Name.Trim();
            tag.Color = dto.Color?.Trim();
            await _context.SaveChangesAsync();

            return Ok(new TagDto(tag.Id, tag.Name, tag.Color));
        }

        // DELETE /api/tags/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTag(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var tag = await _context.Tags.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
            if (tag == null) return NotFound();

            _context.Tags.Remove(tag);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
