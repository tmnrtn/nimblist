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
    public class ClassificationFeedbackController : ControllerBase
    {
        private readonly NimblistContext _context;

        public ClassificationFeedbackController(NimblistContext context)
        {
            _context = context;
        }

        public record FeedbackRequest(string ItemName, Guid? CategoryId, Guid? SubCategoryId);

        [HttpPost]
        public async Task<IActionResult> PostFeedback([FromBody] FeedbackRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.ItemName)) return BadRequest("ItemName is required.");

            var existing = await _context.ClassificationFeedback
                .FirstOrDefaultAsync(f => f.UserId == userId && f.ItemName == request.ItemName);

            if (existing != null)
            {
                existing.CategoryId = request.CategoryId;
                existing.SubCategoryId = request.SubCategoryId;
                existing.CreatedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                _context.ClassificationFeedback.Add(new ItemClassificationFeedback
                {
                    Id = Guid.NewGuid(),
                    ItemName = request.ItemName,
                    CategoryId = request.CategoryId,
                    SubCategoryId = request.SubCategoryId,
                    UserId = userId,
                    CreatedAt = DateTimeOffset.UtcNow,
                });
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // Returns all feedback as newline-delimited JSON for ML training pipeline consumption.
        [HttpGet("export")]
        public async Task<IActionResult> Export()
        {
            var rows = await _context.ClassificationFeedback
                .Include(f => f.Category)
                .Include(f => f.SubCategory)
                .Select(f => new
                {
                    item_name = f.ItemName,
                    category = f.Category != null ? f.Category.Name : null,
                    sub_category = f.SubCategory != null ? f.SubCategory.Name : null,
                    recorded_at = f.CreatedAt,
                })
                .ToListAsync();

            return Ok(rows);
        }
    }
}
