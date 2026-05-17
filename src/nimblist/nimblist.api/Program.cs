using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Nimblist.api.Hubs;
using Nimblist.api.Services;
using Nimblist.Data;
using Nimblist.Data.Models;
using Resend;
using StackExchange.Redis;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;


namespace Nimblist.api
{
    static public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Logging.ClearProviders();
            if (builder.Environment.IsDevelopment())
                builder.Logging.AddConsole();
            else
                builder.Logging.AddJsonConsole(opts =>
                {
                    opts.IncludeScopes = true;
                    opts.TimestampFormat = "o";
                });

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


            builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
            })
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

            var resendApiKey = builder.Configuration["Resend:ApiKey"];
            if (!string.IsNullOrEmpty(resendApiKey))
            {
                builder.Services.AddResend(options => options.ApiToken = resendApiKey);
                builder.Services.AddTransient<IEmailSender, Nimblist.api.Services.ResendEmailSender>();
            }
            else
            {
                Console.WriteLine("Warning: Resend:ApiKey not configured. Emails will not be sent.");
                builder.Services.AddTransient<IEmailSender, Nimblist.api.Services.NoOpEmailSender>();
            }

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
                    Console.WriteLine($"Error configuring Redis. SignalR will run without backplane; Data Protection will use file system. Error: {ex.Message}");
                    builder.Services.AddDataProtection()
                        .PersistKeysToFileSystem(new DirectoryInfo("/keys"))
                        .SetApplicationName("NimblistApp");
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
                Console.WriteLine("Warning: Redis connection string not configured. SignalR will run without backplane; Data Protection will use file system.");
                builder.Services.AddDataProtection()
                    .PersistKeysToFileSystem(new DirectoryInfo("/keys"))
                    .SetApplicationName("NimblistApp");
                builder.Services.AddSignalR()
                                .AddJsonProtocol(options => // Configure System.Text.Json specifically for SignalR
                                {
                                    // *** Add this line for SignalR cycle handling ***
                                    options.PayloadSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;

                                    // Optional: Configure other SignalR serialization settings if needed
                                    // options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                                }); // Add SignalR without Redis backplane
            }

            builder.Services.AddHttpClient();
            builder.Services.AddHttpClient("BraveSearch").ConfigurePrimaryHttpMessageHandler(() =>
                new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.All });
            builder.Services.AddHttpClient("PayPal");
            builder.Services.AddScoped<IClassificationService, ClassificationService>();
            builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();
            builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
            builder.Services.AddScoped<IPayPalService, PayPalService>();
            builder.Services.AddTransient<ISubscriptionEmailService, SubscriptionEmailService>();

            var healthChecks = builder.Services.AddHealthChecks()
                .AddNpgSql(connectionString ?? "", name: "postgres", tags: ["ready"]);
            if (!string.IsNullOrEmpty(redisConnectionString))
                healthChecks.AddRedis(redisConnectionString, name: "redis", tags: ["ready"]);

            var rl = builder.Configuration.GetSection("RateLimits");
            var authIpLimit   = rl.GetValue("AuthIp:PermitLimit", 10);
            var authIpWindow  = rl.GetValue("AuthIp:WindowMinutes", 5);
            var recipeLimit   = rl.GetValue("RecipeImport:PermitLimit", 10);
            var recipeWindow  = rl.GetValue("RecipeImport:WindowMinutes", 60);
            var imgLimit      = rl.GetValue("ImageImport:PermitLimit", 5);
            var imgWindow     = rl.GetValue("ImageImport:WindowMinutes", 60);
            var searchLimit   = rl.GetValue("ImageSearch:PermitLimit", 30);
            var searchWindow  = rl.GetValue("ImageSearch:WindowMinutes", 60);
            var subLimit      = rl.GetValue("SubscriptionActivate:PermitLimit", 5);
            var subWindow     = rl.GetValue("SubscriptionActivate:WindowMinutes", 60);

            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                // Per-IP: login and register pages (unauthenticated)
                options.AddPolicy("auth-ip", ctx =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            Window = TimeSpan.FromMinutes(authIpWindow),
                            PermitLimit = authIpLimit,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 0,
                        }));

                // Per-user: recipe URL import (calls external scraper + LLM)
                options.AddPolicy("recipe-import", ctx =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anon",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            Window = TimeSpan.FromMinutes(recipeWindow),
                            PermitLimit = recipeLimit,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 0,
                        }));

                // Per-user: image import (calls vision LLM — most expensive)
                options.AddPolicy("image-import", ctx =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anon",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            Window = TimeSpan.FromMinutes(imgWindow),
                            PermitLimit = imgLimit,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 0,
                        }));

                // Per-user: image search (Brave API — 2,000 calls/month free tier)
                options.AddPolicy("image-search", ctx =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anon",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            Window = TimeSpan.FromMinutes(searchWindow),
                            PermitLimit = searchLimit,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 0,
                        }));

                // Per-user: subscription activation
                options.AddPolicy("subscription-activate", ctx =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anon",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            Window = TimeSpan.FromMinutes(subWindow),
                            PermitLimit = subLimit,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 0,
                        }));
            });

            var app = builder.Build();

            // Honour X-Forwarded-For / X-Forwarded-Proto from the reverse proxy so that
            // rate-limit partitioning and HSTS use the real client IP and scheme.
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            });

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            else
            {
                app.UseExceptionHandler(errApp =>
                {
                    errApp.Run(async ctx =>
                    {
                        ctx.Response.StatusCode = 500;
                        ctx.Response.ContentType = "application/json";
                        await ctx.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred." });
                    });
                });
            }

            app.UseHttpsRedirection();

            app.Use(async (ctx, next) =>
            {
                ctx.Response.Headers["X-Frame-Options"] = "DENY";
                ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
                ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                ctx.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
                if (ctx.Request.IsHttps)
                {
                    ctx.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
                }
                await next();
            });

            app.UseStaticFiles();
            app.UseRouting();
            app.UseCors("AllowSpecificOrigins");
            app.MapHub<ShoppingListHub>("/hubs/shoppinglist"); // <-- MUST be after UseRouting

            app.UseAuthentication();
            app.UseAuthorization();
            app.UseRateLimiter();

            var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

            using (var scope = app.Services.CreateScope())
            {
                try
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<NimblistContext>();
                    startupLogger.LogInformation("Applying database migrations...");

                    // Advisory lock prevents concurrent migration runs when multiple replicas start
                    // simultaneously. Lock is session-scoped: released automatically on connection close.
                    using var lockConn = new Npgsql.NpgsqlConnection(connectionString);
                    lockConn.Open();
                    using (var lockCmd = lockConn.CreateCommand())
                    {
                        lockCmd.CommandText = "SELECT pg_advisory_lock(887236419)";
                        lockCmd.ExecuteNonQuery();
                    }
                    try
                    {
                        dbContext.Database.Migrate();
                    }
                    finally
                    {
                        using var unlockCmd = lockConn.CreateCommand();
                        unlockCmd.CommandText = "SELECT pg_advisory_unlock(887236419)";
                        unlockCmd.ExecuteNonQuery();
                    }

                    startupLogger.LogInformation("Database migrations applied successfully.");
                }
                catch (Exception ex)
                {
                    startupLogger.LogError(ex, "An error occurred while migrating the database.");
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
                            startupLogger.LogInformation("Admin role assigned to {AdminEmail}.", adminEmail);
                        }
                    }
                }
                catch (Exception ex)
                {
                    startupLogger.LogError(ex, "An error occurred while seeding roles.");
                }
            }

            app.MapRazorPages();
            app.MapControllers();

            // Liveness: is the process up?
            app.MapHealthChecks("/healthz").AllowAnonymous();
            // Readiness: are dependencies (Postgres, Redis) reachable?
            app.MapHealthChecks("/healthz/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("ready"),
            }).AllowAnonymous();

            app.Run();
        }
    }
}
