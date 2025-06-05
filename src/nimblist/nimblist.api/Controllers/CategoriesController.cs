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
    [Authorize] // Requires authentication to access categories
    public class CategoriesController : ControllerBase
    {
        private readonly NimblistContext _context;

        public CategoriesController(NimblistContext context)
        {
            _context = context;
        }

        // GET: api/Categories
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Category>>> GetCategories()
        {
            var result = await _context.Categories
                                 .Include(c => c.SubCategories) //
                                 .OrderBy(c => c.Name)
                                 .ToListAsync();

            return Ok(result);
        }

        // GET: api/Categories/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Category>> GetCategory(Guid id)
        {
            var category = await _context.Categories
                                         .Include(c => c.SubCategories) //
                                         .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
            {
                return NotFound();
            }

            return Ok(category);
        }
    }
}