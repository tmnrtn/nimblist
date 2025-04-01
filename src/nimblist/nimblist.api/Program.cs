using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nimblist.Data;
using Nimblist.Data.Models;

namespace Nimblist.api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            builder.Services.AddDbContext<NimblistContext>(options =>
                options.UseNpgsql(connectionString));

            builder.Services.AddIdentity<ApplicationUser, IdentityRole>() // Assuming ApplicationUser is in .Data
                .AddEntityFrameworkStores<NimblistContext>()
                .AddDefaultTokenProviders();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            using (var scope = app.Services.CreateScope())
            {
                try
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<NimblistContext>();
                    // Check if DB exists, create if not (optional, MigrateAsync handles creation)
                    // dbContext.Database.EnsureCreated(); // Use ONLY if not using migrations OR for initial creation
                    Console.WriteLine("Applying database migrations...");
                    dbContext.Database.Migrate(); // Applies pending migrations
                    Console.WriteLine("Database migrations applied successfully.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred while migrating the database: {ex.Message}");
                    // Optionally, rethrow or handle failure to prevent app start
                    // throw;
                }
            }

            app.Run();
        }
    }
}
