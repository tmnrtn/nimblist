using System;

namespace Nimblist.api.DTO
{
    public class ItemWithCategoryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Quantity { get; set; }
        public bool IsChecked { get; set; }
        public DateTimeOffset AddedAt { get; set; }
        public Guid ShoppingListId { get; set; }
        
        // Category information
        public Guid? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        
        // SubCategory information
        public Guid? SubCategoryId { get; set; }
        public string? SubCategoryName { get; set; }
    }
}