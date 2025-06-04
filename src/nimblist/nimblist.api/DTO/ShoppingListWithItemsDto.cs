using Nimblist.Data.Models;
using System;
using System.Collections.Generic;

namespace Nimblist.api.DTO
{
    public class ShoppingListWithItemsDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public List<ItemWithCategoryDto> Items { get; set; } = new List<ItemWithCategoryDto>();
    }
}