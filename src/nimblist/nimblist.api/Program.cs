using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Nimblist.api.Hubs;
using Nimblist.Data;
using Nimblist.Data.Models;
using StackExchange.Redis;
using System.Text.Json.Serialization;


namespace Nimblist.api
{
    static public class Program
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
                                          policy.WithOrigins("https://localhost:5173") // Allow React dev server
                                                .AllowAnyHeader()
                                                .AllowAnyMethod()
                                                .AllowCredentials();
                                      }
                                      // In Production, if allowedOrigins is null/empty, CORS might block everything - ensure config is set!
                                  });
            });

            builder.Services.AddControllers()
                    .AddJsonOptions(options =>
                    {
                        // Add this line to ignore cycles during serialization
                        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;

                        // Optional: You might also configure other things like naming policy here
                        // options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                    });
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            builder.Services.AddDbContext<NimblistContext>(options =>
                options.UseNpgsql(connectionString));


            builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => options.SignIn.RequireConfirmedAccount = false)
                .AddEntityFrameworkStores<NimblistContext>()
                .AddDefaultTokenProviders()
                .AddDefaultUI();

            builder.Services.ConfigureApplicationCookie(options =>
            {
                // Configure the events for the application cookie (used by Identity)
                options.Events.OnRedirectToLogin = context =>
                {
                    // If the request path starts with /api, return 401 Unauthorized instead of redirecting
                    if (context.Request.Path.StartsWithSegments("/api"))
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        // Mark the response as complete to prevent the default redirect
                        return Task.CompletedTask;
                    }
                    else
                    {
                        // For non-API requests (like browser navigating to protected Razor Pages),
                        // perform the default redirect to the login page.
                        context.Response.Redirect(context.RedirectUri);
                        return Task.CompletedTask;
                    }
                };

                options.Events.OnRedirectToAccessDenied = context =>
                {
                    // If the request path starts with /api, return 403 Forbidden instead of redirecting
                    if (context.Request.Path.StartsWithSegments("/api"))
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    }
                    else
                    {
                        // For non-API requests, perform the default redirect to the access denied page.
                        context.Response.Redirect(context.RedirectUri);
                        return Task.CompletedTask;
                    }
                };

                // Set cookie expiration to 30 days and enable sliding expiration
                options.ExpireTimeSpan = TimeSpan.FromDays(30);
                options.SlidingExpiration = true;

                // Other cookie settings like LoginPath, LogoutPath, AccessDeniedPath
                // are usually configured by AddIdentity/AddDefaultIdentity automatically.
            });

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

                    // Save tokens to allow refresh if needed
                    googleOptions.SaveTokens = true;

                    // Optional: Configure callback path if different from default /signin-google
                    // googleOptions.CallbackPath = "/your-custom-signin-google";

                    // Optional: Request specific scopes (profile and email are often default/included)
                    // googleOptions.Scope.Add("profile");
                    // googleOptions.Scope.Add("email");
                }
            });


            builder.Services.AddRazorPages();

            builder.Services.AddTransient<IEmailSender, NoOpEmailSender>();

            var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
            if (!string.IsNullOrEmpty(redisConnectionString))
            {
                try
                {
                    Console.WriteLine("Connecting to Redis...");
                    // Use a single ConnectionMultiplexer for both Data Protection and SignalR
                    // Register as singleton for reuse
                    builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));

                    // Get the multiplexer instance for configuration methods below
                    var redis = builder.Services.BuildServiceProvider().GetRequiredService<IConnectionMultiplexer>();

                    // Configure Data Protection
                    builder.Services.AddDataProtection()
                        .PersistKeysToStackExchangeRedis(redis, "DataProtection-Keys-Nimblist")
                        .SetApplicationName("NimblistApp");
                    Console.WriteLine("Data Protection configured to use Redis.");

                    // Configure SignalR and add Redis backplane using the same connection string
                    builder.Services.AddSignalR()
                        .AddJsonProtocol(options => // Configure System.Text.Json specifically for SignalR
                        {
                            // *** Add this line for SignalR cycle handling ***
                            options.PayloadSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;

                            // Optional: Configure other SignalR serialization settings if needed
                            // options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                        })
                        .AddStackExchangeRedis(redisConnectionString, options =>
                        {
                            options.Configuration.ChannelPrefix = RedisChannel.Literal("Nimblist.SignalR."); // Updated prefix usage for clarity
                        });
                    Console.WriteLine("SignalR configured with Redis backplane.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error configuring Redis. SignalR/DataProtection will be ephemeral. Error: {ex.Message}");
                    // Fallback configurations if Redis fails
                    builder.Services.AddDataProtection().SetApplicationName("NimblistApp");
                    builder.Services.AddSignalR()
                                    .AddJsonProtocol(options => // Configure System.Text.Json specifically for SignalR
                                    {
                                        // *** Add this line for SignalR cycle handling ***
                                        options.PayloadSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;

                                        // Optional: Configure other SignalR serialization settings if needed
                                        // options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                                    });
                }
            }
            else
            {
                Console.WriteLine("Warning: Redis connection string not configured. SignalR/DataProtection will be ephemeral.");
                // Fallback configurations without Redis
                builder.Services.AddDataProtection().SetApplicationName("NimblistApp");
                builder.Services.AddSignalR()
                                .AddJsonProtocol(options => // Configure System.Text.Json specifically for SignalR
                                {
                                    // *** Add this line for SignalR cycle handling ***
                                    options.PayloadSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;

                                    // Optional: Configure other SignalR serialization settings if needed
                                    // options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                                }); // Add SignalR without Redis backplane
            }

            var keyPath = "/keys"; // The path defined in the docker-compose volume mount
            try
            {
                Console.WriteLine($"Configuring Data Protection to use file system path: {keyPath}");
                builder.Services.AddDataProtection()
                    .PersistKeysToFileSystem(new DirectoryInfo(keyPath))
                    // Optional: Set a unique application name
                    .SetApplicationName("NimblistApp");
                // Optional: Configure key protection if needed (e.g., ProtectKeysWith* - may require specific setup in Linux containers)
                Console.WriteLine("Data Protection configured to use File System.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error configuring Data Protection with File System path {keyPath}. Keys will be ephemeral. Error: {ex.Message}");
                // Fallback to default ephemeral keys if file system setup fails
                builder.Services.AddDataProtection()
                       .SetApplicationName("NimblistApp");
            }

            builder.Services.AddHttpClient();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseStaticFiles();
            app.UseRouting();
            app.UseCors("AllowSpecificOrigins");
            app.MapHub<ShoppingListHub>("/hubs/shoppinglist"); // <-- MUST be after UseRouting

            app.UseAuthentication();
            app.UseAuthorization();


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
            app.MapControllers();

            app.Run();
        }
    }
}
