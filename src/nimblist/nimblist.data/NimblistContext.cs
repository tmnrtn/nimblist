using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Nimblist.Data.Models;

namespace Nimblist.Data
{
    // Inherit from IdentityDbContext<ApplicationUser> to include Identity tables (Users, Roles, Claims, etc.)
    // along with your custom DbSets. Pass your custom user class.
    public class NimblistContext : IdentityDbContext<ApplicationUser>
    {
        // Define DbSet properties for each of your custom entities.
        // These represent the tables in your database.
        public virtual DbSet<ShoppingList> ShoppingLists { get; set; }
        public virtual DbSet<Item> Items { get; set; }
        public virtual DbSet<Family> Families { get; set; }
        public virtual DbSet<FamilyMember> FamilyMembers { get; set; }

        // Constructor needed for dependency injection.
        // It accepts DbContextOptions, allowing the configuration (like connection string)
        // to be specified in your main API project (Program.cs).
        public NimblistContext(DbContextOptions<NimblistContext> options)
            : base(options)
        {
        }

        // Optional but recommended: Override OnModelCreating to configure the model using the Fluent API.
        // This gives you more control than Data Annotations ([Key], [Required], etc.) and keeps entities cleaner.
        protected override void OnModelCreating(ModelBuilder builder)
        {
            // IMPORTANT: Call the base method first when inheriting from IdentityDbContext
            // This ensures Identity models are configured correctly.
            base.OnModelCreating(builder);

            // --- Configure Relationships using Fluent API ---

            // ApplicationUser (1) to ShoppingList (*) relationship
            builder.Entity<ApplicationUser>()
                .HasMany(user => user.ShoppingLists) // User navigation property
                .WithOne(list => list.User)          // ShoppingList navigation property
                .HasForeignKey(list => list.UserId)  // Foreign key on ShoppingList
                .IsRequired()                        // Makes FK non-nullable
                .OnDelete(DeleteBehavior.Cascade);   // Example: If user is deleted, delete their lists (use Restrict if you don't want this)

            // ShoppingList (1) to Item (*) relationship
            builder.Entity<ShoppingList>()
                .HasMany(list => list.Items)         // ShoppingList navigation property
                .WithOne(item => item.List)          // Item navigation property
                .HasForeignKey(item => item.ShoppingListId) // Foreign key on Item
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);   // Example: If list is deleted, delete its items

            // --- Configure Entities / Tables ---

            builder.Entity<ShoppingList>(entity =>
            {
                entity.HasKey(l => l.Id); // Define primary key
                entity.Property(l => l.Name).HasMaxLength(100).IsRequired();
                entity.HasIndex(l => l.UserId).HasDatabaseName("IX_ShoppingLists_UserId"); // Example index for performance
            });

            builder.Entity<Item>(entity =>
            {
                entity.HasKey(i => i.Id);
                entity.Property(i => i.Name).HasMaxLength(200).IsRequired();
                entity.Property(i => i.Quantity).HasMaxLength(50);
                entity.Property(i => i.IsChecked).HasDefaultValue(false); // Define default value
                entity.HasIndex(i => i.ShoppingListId).HasDatabaseName("IX_Items_ShoppingListId");
            });

            // Family to FamilyMember relationship
            builder.Entity<Family>()
                .HasMany(f => f.Members)
                .WithOne(m => m.Family)
                .HasForeignKey(m => m.FamilyId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            // ApplicationUser to FamilyMember relationship
            builder.Entity<ApplicationUser>()
                .HasMany(u => u.Families)
                .WithOne(m => m.User)
                .HasForeignKey(m => m.UserId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            // Index for performance
            builder.Entity<FamilyMember>()
                .HasIndex(m => new { m.UserId, m.FamilyId })
                .IsUnique()
                .HasDatabaseName("IX_FamilyMembers_UserId_FamilyId");

            // === PostgreSQL Specific Configuration (Optional Examples) ===

            // Npgsql provider generally maps .NET types well to PostgreSQL types (e.g., Guid to uuid, DateTimeOffset to timestamptz)
            // You usually don't need explicit configuration unless you want something specific.

            // Example: If you wanted to force snake_case naming for tables/columns (requires EFCore.NamingConventions package)
            // builder.UseSnakeCaseNamingConvention();
        }
    }
}