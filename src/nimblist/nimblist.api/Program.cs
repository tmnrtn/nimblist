using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Nimblist.api.Hubs;
using Nimblist.api.Services;
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

                // Enforce Secure flag regardless of request scheme
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;

                // Other cookie settings like LoginPath, LogoutPath, AccessDeniedPath
                // are usually configured by AddIdentity/AddDefaultIdentity automatically.
            });

            var authBuilder = builder.Services.AddAuthentication()
            .AddCookie(options =>
            {
                options.LoginPath = "/Identity/Account/Login";
                options.AccessDeniedPath = "/Identity/Account/AccessDenied";
            });

            var googleSection = builder.Configuration.GetSection("Authentication:Google");
            if (googleSection.Exists() && !string.IsNullOrEmpty(googleSection["ClientId"]) && !string.IsNullOrEmpty(googleSection["ClientSecret"]))
            {
                authBuilder.AddGoogle(options =>
                {
                    options.ClientId = googleSection["ClientId"]!;
                    options.ClientSecret = googleSection["ClientSecret"]!;
                    options.SaveTokens = true;
                });
            }
            else
            {
                Console.WriteLine("Warning: Google Authentication credentials not configured. Google login will be unavailable.");
            }

            var facebookSection = builder.Configuration.GetSection("Authentication:Facebook");
            if (facebookSection.Exists() && !string.IsNullOrEmpty(facebookSection["AppId"]) && !string.IsNullOrEmpty(facebookSection["AppSecret"]))
            {
                authBuilder.AddFacebook(options =>
                {
                    options.AppId = facebookSection["AppId"]!;
                    options.AppSecret = facebookSection["AppSecret"]!;
                    options.SaveTokens = true;
                });
            }
            else
            {
                Console.WriteLine("Warning: Facebook Authentication credentials not configured. Facebook login will be unavailable.");
            }

            var microsoftSection = builder.Configuration.GetSection("Authentication:Microsoft");
            if (microsoftSection.Exists() && !string.IsNullOrEmpty(microsoftSection["ClientId"]) && !string.IsNullOrEmpty(microsoftSection["ClientSecret"]))
            {
                authBuilder.AddMicrosoftAccount(options =>
                {
                    options.ClientId = microsoftSection["ClientId"]!;
                    options.ClientSecret = microsoftSection["ClientSecret"]!;
                    options.SaveTokens = true;
                });
            }
            else
            {
                Console.WriteLine("Warning: Microsoft Authentication credentials not configured. Microsoft login will be unavailable.");
            }


            builder.Services.AddRazorPages();

            builder.Services.AddTransient<IEmailSender, Nimblist.api.Services.NoOpEmailSender>();

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
            builder.Services.AddScoped<IClassificationService, ClassificationService>();
            builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();

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
                    Console.WriteLine("Applying database migrations...");
                    dbContext.Database.Migrate();
                    Console.WriteLine("Database migrations applied successfully.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred while migrating the database: {ex.Message}");
                }

                try
                {
                    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

                    foreach (var role in new[] { "Admin", "Standard" })
                    {
                        if (!roleManager.RoleExistsAsync(role).GetAwaiter().GetResult())
                            roleManager.CreateAsync(new IdentityRole(role)).GetAwaiter().GetResult();
                    }

                    var adminEmail = app.Configuration["AdminSettings:AdminEmail"];
                    if (!string.IsNullOrEmpty(adminEmail))
                    {
                        var adminUser = userManager.FindByEmailAsync(adminEmail).GetAwaiter().GetResult();
                        if (adminUser != null && !userManager.IsInRoleAsync(adminUser, "Admin").GetAwaiter().GetResult())
                        {
                            userManager.AddToRoleAsync(adminUser, "Admin").GetAwaiter().GetResult();
                            Console.WriteLine($"Admin role assigned to {adminEmail}.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred while seeding roles: {ex.Message}");
                }
            }

            app.MapRazorPages();
            app.MapControllers();

            app.Run();
        }
    }
}
