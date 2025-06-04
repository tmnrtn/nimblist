using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nimblist.Data;
using Nimblist.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace Nimblist.api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Requires authentication to access subcategories
    public class SubCategoriesController : ControllerBase
    {
        private readonly NimblistContext _context;

        public SubCategoriesController(NimblistContext context)
        {
            _context = context;
        }

        // GET: api/SubCategories
        // Optionally filter by parentCategoryId: e.g., /api/SubCategories?parentCategoryId=GUID
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SubCategory>>> GetSubCategories([FromQuery] Guid? parentCategoryId)
        {
            var query = _context.SubCategories.AsQueryable();

            if (parentCategoryId.HasValue)
            {
                query = query.Where(sc => sc.ParentCategoryId == parentCategoryId.Value); //
            }

            return await query.OrderBy(sc => sc.Name).ToListAsync();
        }

        // GET: api/SubCategories/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<SubCategory>> GetSubCategory(Guid id)
        {
            var subCategory = await _context.SubCategories
                                            .Include(sc => sc.ParentCategory) //
                                            .FirstOrDefaultAsync(sc => sc.Id == id);

            if (subCategory == null)
            {
                return NotFound();
            }

            return Ok(subCategory);
        }
    }
}