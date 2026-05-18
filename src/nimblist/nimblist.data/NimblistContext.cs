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
        public virtual DbSet<ListShare> ListShares { get; set; } = null!; // Initialize to avoid null warnings
        public virtual DbSet<Category> Categories { get; set; } = null!; // Initialize to avoid null warnings
        public virtual DbSet<SubCategory> SubCategories { get; set; } = null!; // Initialize to avoid null warnings
        public virtual DbSet<PreviousItemName> PreviousItemNames { get; set; }
        public virtual DbSet<ItemClassificationFeedback> ClassificationFeedback { get; set; } = null!;
        public virtual DbSet<Recipe> Recipes { get; set; } = null!;
        public virtual DbSet<RecipeIngredient> RecipeIngredients { get; set; } = null!;
        public virtual DbSet<RecipeShare> RecipeShares { get; set; } = null!;
        public virtual DbSet<MealPlan> MealPlans { get; set; } = null!;
        public virtual DbSet<MealPlanEntry> MealPlanEntries { get; set; } = null!;
        public virtual DbSet<MealPlanShare> MealPlanShares { get; set; } = null!;
        public virtual DbSet<LlmSettings> LlmSettings { get; set; } = null!;
        public virtual DbSet<UserPushSubscription> PushSubscriptions { get; set; } = null!;
        public virtual DbSet<Tag> Tags { get; set; } = null!;
        public virtual DbSet<UserSubscription> UserSubscriptions { get; set; } = null!;

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

            builder.Entity<Family>()
                .HasMany(f => f.ListShares)
                .WithOne(m => m.Family)
                .HasForeignKey(m => m.FamilyId)
                .OnDelete(DeleteBehavior.Cascade);

            // ApplicationUser to FamilyMember relationship
            builder.Entity<ApplicationUser>()
                .HasMany(u => u.ListShares)
                .WithOne(m => m.User)
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ShoppingList>()
                .HasMany(u => u.ListShares)
                .WithOne(m => m.List)
                .HasForeignKey(m => m.ListId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            // Index for performance
            builder.Entity<FamilyMember>()
                .HasIndex(m => new { m.UserId, m.FamilyId })
                .IsUnique()
                .HasDatabaseName("IX_FamilyMembers_UserId_FamilyId");

            builder.Entity<Category>()
                .HasMany(u => u.SubCategories)
                .WithOne(m => m.ParentCategory)
                .HasForeignKey(m => m.ParentCategoryId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<SubCategory>()
                .HasMany(u => u.Items)
                .WithOne(m => m.SubCategory)
                .HasForeignKey(m => m.SubCategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<Category>()
                .HasMany(u => u.Items)
                .WithOne(m => m.Category)
                .HasForeignKey(m => m.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<PreviousItemName>(entity =>
            {
                entity.HasKey(p => p.Id);
                entity.Property(p => p.Name).HasMaxLength(200).IsRequired();
                entity.Property(p => p.UserId).IsRequired();
                entity.HasIndex(p => new { p.UserId, p.Name }).IsUnique();
            });

            builder.Entity<ItemClassificationFeedback>(entity =>
            {
                entity.HasKey(f => f.Id);
                entity.Property(f => f.ItemName).HasMaxLength(200).IsRequired();
                entity.Property(f => f.UserId).IsRequired();
                entity.HasIndex(f => f.UserId).HasDatabaseName("IX_ClassificationFeedback_UserId");
            });

            builder.Entity<ItemClassificationFeedback>()
                .HasOne(f => f.Category)
                .WithMany()
                .HasForeignKey(f => f.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<ItemClassificationFeedback>()
                .HasOne(f => f.SubCategory)
                .WithMany()
                .HasForeignKey(f => f.SubCategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<ApplicationUser>()
                .HasMany<ItemClassificationFeedback>()
                .WithOne()
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ApplicationUser>()
                .HasMany(u => u.Recipes)
                .WithOne(r => r.User)
                .HasForeignKey(r => r.UserId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Recipe>(entity =>
            {
                entity.HasKey(r => r.Id);
                entity.Property(r => r.Title).HasMaxLength(300).IsRequired();
                entity.HasIndex(r => r.UserId).HasDatabaseName("IX_Recipes_UserId");
            });

            builder.Entity<Recipe>()
                .HasMany(r => r.Ingredients)
                .WithOne(i => i.Recipe)
                .HasForeignKey(i => i.RecipeId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<RecipeIngredient>(entity =>
            {
                entity.HasKey(i => i.Id);
                entity.Property(i => i.Text).HasMaxLength(500).IsRequired();
            });

            builder.Entity<Recipe>()
                .HasMany(r => r.Shares)
                .WithOne(rs => rs.Recipe)
                .HasForeignKey(rs => rs.RecipeId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<RecipeShare>(entity =>
            {
                entity.HasKey(rs => rs.Id);
                entity.HasIndex(rs => rs.RecipeId).HasDatabaseName("IX_RecipeShares_RecipeId");
            });

            builder.Entity<RecipeShare>()
                .HasOne(rs => rs.Family)
                .WithMany()
                .HasForeignKey(rs => rs.FamilyId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<RecipeShare>()
                .HasOne(rs => rs.User)
                .WithMany()
                .HasForeignKey(rs => rs.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // MealPlan relationships
            builder.Entity<ApplicationUser>()
                .HasMany(u => u.MealPlans)
                .WithOne(m => m.User)
                .HasForeignKey(m => m.UserId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<MealPlan>(entity =>
            {
                entity.HasKey(m => m.Id);
                entity.Property(m => m.Name).HasMaxLength(200).IsRequired();
                entity.HasIndex(m => m.UserId).HasDatabaseName("IX_MealPlans_UserId");
            });

            builder.Entity<MealPlan>()
                .HasMany(m => m.Entries)
                .WithOne(e => e.MealPlan)
                .HasForeignKey(e => e.MealPlanId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Recipe>()
                .HasMany<MealPlanEntry>()
                .WithOne(e => e.Recipe)
                .HasForeignKey(e => e.RecipeId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<MealPlanEntry>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.MealPlanId, e.PlannedDate }).HasDatabaseName("IX_MealPlanEntries_PlanId_Date");
            });

            builder.Entity<MealPlan>()
                .HasMany(m => m.Shares)
                .WithOne(s => s.MealPlan)
                .HasForeignKey(s => s.MealPlanId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<MealPlanShare>()
                .HasOne(s => s.Family)
                .WithMany()
                .HasForeignKey(s => s.FamilyId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<MealPlanShare>()
                .HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<MealPlanShare>(entity =>
            {
                entity.HasKey(s => s.Id);
                entity.HasIndex(s => s.MealPlanId).HasDatabaseName("IX_MealPlanShares_MealPlanId");
            });

            builder.Entity<ApplicationUser>()
                .HasMany(u => u.PushSubscriptions)
                .WithOne(s => s.User)
                .HasForeignKey(s => s.UserId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<UserPushSubscription>(entity =>
            {
                entity.HasKey(s => s.Id);
                entity.Property(s => s.Endpoint).HasMaxLength(2048).IsRequired();
                entity.Property(s => s.P256dh).HasMaxLength(512).IsRequired();
                entity.Property(s => s.Auth).HasMaxLength(256).IsRequired();
                entity.HasIndex(s => s.Endpoint).IsUnique().HasDatabaseName("IX_PushSubscriptions_Endpoint");
                entity.HasIndex(s => s.UserId).HasDatabaseName("IX_PushSubscriptions_UserId");
            });

            // Tag — user-scoped, many-to-many with Recipe
            builder.Entity<ApplicationUser>()
                .HasMany<Tag>()
                .WithOne(t => t.User)
                .HasForeignKey(t => t.UserId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Tag>(entity =>
            {
                entity.HasKey(t => t.Id);
                entity.Property(t => t.Name).HasMaxLength(50).IsRequired();
                entity.Property(t => t.Color).HasMaxLength(20);
                entity.HasIndex(t => t.UserId).HasDatabaseName("IX_Tags_UserId");
            });

            // Implicit many-to-many join table: RecipeTag
            builder.Entity<Recipe>()
                .HasMany(r => r.Tags)
                .WithMany(t => t.Recipes)
                .UsingEntity(j => j.ToTable("RecipeTag"));

            // UserSubscription — one active subscription per user
            builder.Entity<ApplicationUser>()
                .HasOne<UserSubscription>()
                .WithOne(s => s.User)
                .HasForeignKey<UserSubscription>(s => s.UserId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<UserSubscription>(entity =>
            {
                entity.HasKey(s => s.Id);
                entity.Property(s => s.UserId).IsRequired();
                entity.Property(s => s.PayPalSubscriptionId).HasMaxLength(100).IsRequired();
                entity.Property(s => s.Status).HasMaxLength(30).IsRequired();
                entity.HasIndex(s => s.UserId).IsUnique().HasDatabaseName("IX_UserSubscriptions_UserId");
                entity.HasIndex(s => s.PayPalSubscriptionId).HasDatabaseName("IX_UserSubscriptions_PayPalSubscriptionId");
            });

            builder.Entity<ApplicationUser>()
                .HasIndex(u => u.InviteCode)
                .IsUnique()
                .HasFilter("\"InviteCode\" IS NOT NULL")
                .HasDatabaseName("IX_AspNetUsers_InviteCode");

            // === PostgreSQL Specific Configuration (Optional Examples) ===

            // Npgsql provider generally maps .NET types well to PostgreSQL types (e.g., Guid to uuid, DateTimeOffset to timestamptz)
            // You usually don't need explicit configuration unless you want something specific.

            // Example: If you wanted to force snake_case naming for tables/columns (requires EFCore.NamingConventions package)
            // builder.UseSnakeCaseNamingConvention();
        }
    }
}