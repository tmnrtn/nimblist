using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
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

            // *** 1. Define CORS Policy ***
            const string MyAllowSpecificOrigins = "AllowSpecificOrigins"; // Define a name for the policy

            var corsSettings = builder.Configuration.GetSection("CorsSettings");
            var allowedOrigins = corsSettings["AllowedOrigins"]?.Split(';', StringSplitOptions.RemoveEmptyEntries);

            builder.Services.AddCors(options =>
            {
                options.AddPolicy(name: MyAllowSpecificOrigins,
                                  policy =>
                                  {
                                      if (allowedOrigins != null && allowedOrigins.Length > 0)
                                      {
                                          Console.WriteLine($"Configuring CORS for origins: {string.Join(", ", allowedOrigins)}");
                                          policy.WithOrigins(allowedOrigins) // Allow specific origins from config
                                                .AllowAnyHeader()           // Allow any standard header
                                                .AllowAnyMethod()           // Allow common HTTP methods (GET, POST, PUT, DELETE, etc.)
                                                .AllowCredentials();        // IMPORTANT: Allow cookies/auth tokens to be sent from frontend
                                                                            // NOTE: When using AllowCredentials(), you MUST specify origins via WithOrigins(), you cannot use AllowAnyOrigin().
                                      }
                                      else if (builder.Environment.IsDevelopment()) // Fallback for development if config missing
                                      {
                                          Console.WriteLine("Warning: CORS AllowedOrigins not configured. Allowing localhost:3000 for Development.");
                                          policy.WithOrigins("http://localhost:3000") // Allow React dev server
                                                .AllowAnyHeader()
                                                .AllowAnyMethod()
                                                .AllowCredentials();
                                      }
                                      // In Production, if allowedOrigins is null/empty, CORS might block everything - ensure config is set!
                                  });
            });

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            builder.Services.AddDbContext<NimblistContext>(options =>
                options.UseNpgsql(connectionString));

            builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => options.SignIn.RequireConfirmedAccount = false)
                .AddEntityFrameworkStores<NimblistContext>()
                .AddDefaultTokenProviders();

            builder.Services.AddAuthentication(options =>
            {
                // Optional: Configure default schemes if needed
                // options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                // options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme; // Example challenge
            })
            .AddCookie(options => // Assuming cookie authentication for Identity
            {
                options.LoginPath = "/Identity/Account/Login"; // Or your login path
                options.AccessDeniedPath = "/Identity/Account/AccessDenied";
            })
            .AddGoogle(googleOptions =>
            {
                // Read configuration from appsettings/user secrets/environment variables
                IConfigurationSection googleAuthNSection =
                    builder.Configuration.GetSection("Authentication:Google");

                if (!googleAuthNSection.Exists() || string.IsNullOrEmpty(googleAuthNSection["ClientId"]) || string.IsNullOrEmpty(googleAuthNSection["ClientSecret"]))
                {
                    Console.WriteLine("Warning: Google Authentication credentials not found in configuration (Authentication:Google:ClientId, Authentication:Google:ClientSecret). Google login will likely fail.");
                    // Optionally disable the provider if config is missing:
                    // return; // Exit AddGoogle configuration if keys are missing
                }
                else
                {
                    googleOptions.ClientId = googleAuthNSection["ClientId"]!; // Use null-forgiving operator if confident or check nulls properly
                    googleOptions.ClientSecret = googleAuthNSection["ClientSecret"]!;

                    // Optional: Configure callback path if different from default /signin-google
                    // googleOptions.CallbackPath = "/your-custom-signin-google";

                    // Optional: Request specific scopes (profile and email are often default/included)
                    // googleOptions.Scope.Add("profile");
                    // googleOptions.Scope.Add("email");

                    // Optional: Save tokens if needed for calling Google APIs later
                    // googleOptions.SaveTokens = true;
                }
            });

            builder.Services.AddRazorPages();

            builder.Services.AddTransient<IEmailSender, NoOpEmailSender>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthentication();
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

            app.MapRazorPages();

            app.Run();
        }
    }
}
