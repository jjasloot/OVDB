using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SpaServices.AngularCli;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;
using Microsoft.AspNetCore.OData;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using OVDB_database.Database;
using OVDB_database.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using OV_DB.Services;
using OV_DB.Hubs;
using Microsoft.AspNetCore.Http;
using Telegram.Bot.AspNetCore;

namespace OV_DB
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<IISServerOptions>(options =>
            {
                options.AllowSynchronousIO = true;
            });
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Toegangspassen_backend", Version = "v1" });
                c.AddSecurityDefinition("Bearer",
                    new OpenApiSecurityScheme
                    {
                        Description = "JWT Authorization header using the Bearer scheme.",
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer"
                    }
                );
                c.AddSecurityRequirement(new OpenApiSecurityRequirement{
                    {
                        new OpenApiSecurityScheme{
                            Reference = new OpenApiReference{
                                Id = "Bearer",
                                Type = ReferenceType.SecurityScheme
                            }
                        },new List<string>()
                    }
                });
            });
            services.AddDbContext<OVDBDatabaseContext>(options =>
            {
                var connectionString = Configuration["DBCONNECTIONSTRING"];
                options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
                    options => options
                    .UseNetTopologySuite()
                    .EnableRetryOnFailure()
                    );
#if DEBUG
                //Log all sql commands
                //options.LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Information);
                options.EnableSensitiveDataLogging();
#endif

            });
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "bearer";
                options.DefaultChallengeScheme = "bearer";
            }).AddJwtBearer("bearer", options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = true,
                    ValidAudience = "OVDB",
                    ValidateIssuer = true,
                    ValidIssuer = Configuration["Tokens:Issuer"],
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration["JWTSigningKey"])),
                    ValidateLifetime = true
                };
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                        {
                            context.Response.Headers.Append("Token-Expired", "true");
                        }
                        return Task.CompletedTask;
                    }
                };
            });
            services.AddControllers()
                .AddOData(r => r.Select().Filter().AddRouteComponents("odata", GetEdmModel()))
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Converters.Add(new NetTopologySuite.IO.Converters.GeoJsonConverterFactory());
                });

            // Add response caching
            services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true; // Required — default is false, so HTTPS responses are not compressed
                options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
                options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
            });
            services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(options =>
                options.Level = System.IO.Compression.CompressionLevel.Fastest);
            services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(options =>
                options.Level = System.IO.Compression.CompressionLevel.Optimal);

            services.AddSpaStaticFiles(configuration =>
            {
                configuration.RootPath = "OVDBFrontend/dist/OVDBFrontend/browser";
            });
            services.AddCors(c =>
            {
                c.AddDefaultPolicy(p =>
                {
                    p.WithOrigins("http://localhost:4200", "https://ovdb.infinityx.nl");
                    p.AllowAnyHeader();
                    p.AllowAnyMethod();
                    p.AllowCredentials();
                });
            });
            services.AddMvc(options =>
            {
                options.EnableEndpointRouting = false;
            }).AddNewtonsoftJson(ops => ops.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore);
            services.ConfigureTelegramBotMvc();
            services.AddSignalR();
            services.AddTransient<IRouteRegionsService, RouteRegionsService>();
            services.AddTransient<IStationRegionsService, StationRegionsService>();
            services.AddTransient<ITimezoneService, TimezoneService>();
            services.AddSingleton<IFontLoader, FontLoader>();
            services.AddScoped<TelegramBotService>();
            services.AddHttpClient(TrawellingService.HTTP_CLIENT_NAME,client =>
            {
                client.DefaultRequestHeaders.Add("User-Agent", "OVDB/1.0 (https://github.com/jjasloot/OVDB; contact-me jaapslootbeek@gmail.com)");
            });
            services.AddScoped<ITrawellingService, TrawellingService>();
            // Singleton so the Träwelling rate-limit budget is shared across request scopes.
            services.AddSingleton<ITraewellingRateLimiter, TraewellingRateLimiter>();
            services.AddHostedService<TraewellingTokenRefreshService>();
            services.AddHostedService<TraewellingInboxSweepService>();

            // Register named HttpClients for different services to avoid socket exhaustion
            services.AddHttpClient("OSM", client =>
            {
                client.DefaultRequestHeaders.Add("User-Agent", "OVDB/1.0 (contact-me jaapslootbeek@gmail.com)");
                client.Timeout = TimeSpan.FromSeconds(240);
            }).ConfigurePrimaryHttpMessageHandler(() => new System.Net.Http.SocketsHttpHandler
            {
                // Overpass responses are large JSON bodies that compress ~10x.
                AutomaticDecompression = System.Net.DecompressionMethods.All
            });
            // Singleton so the per-endpoint failure cooldowns are shared across requests.
            services.AddSingleton<IOverpassService, OverpassService>();
            // Bounded cache for parsed OSM relation data; entries are sized by element
            // count so a handful of huge routes can't grow memory without limit.
            services.AddKeyedSingleton<Microsoft.Extensions.Caching.Memory.IMemoryCache>("OsmCache",
                (_, _) => new Microsoft.Extensions.Caching.Memory.MemoryCache(
                    new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions { SizeLimit = 100_000 }));

            services.AddHostedService<UpdateRegionService>();
            services.AddHostedService<RefreshRoutesService>();
            services.AddHostedService<RefreshRoutesWithoutRegionsService>();

            NetTopologySuite.NtsGeometryServices.Instance = new NetTopologySuite.NtsGeometryServices(
   NetTopologySuite.Geometries.GeometryOverlay.NG);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // Add response compression early in the pipeline
            app.UseResponseCompression();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "OVDB"));
            }
            else
            {
                //app.UseExceptionHandler("/Error");
                //The default HSTS value is 30 days.You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHttpsRedirection();
                app.UseHsts();
            }
            app.UseStaticFiles();
            if (!env.IsDevelopment())
            {
                app.UseSpaStaticFiles();
            }

            app.UseRouting();
            app.UseCors();
            app.UseXfo(o => o.SameOrigin());
            app.UseXContentTypeOptions();
            app.UseXXssProtection(options => options.EnabledWithBlockMode());
            app.UseCors();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(r =>
            {
                r.MapHub<MapGenerationHub>("/mapGenerationHub");
                r.MapControllers();
                r.MapSwagger();
            });
            using (var serviceScope = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                try
                {
                    serviceScope.ServiceProvider.GetService<OVDBDatabaseContext>().Database.Migrate();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
            app.UseSpa(spa =>
            {
                // To learn more about options for serving an Angular SPA from ASP.NET Core,
                // see https://go.microsoft.com/fwlink/?linkid=864501

                spa.Options.SourcePath = "OVDBFrontend";

                // Don't add spa.UseAngularCliServer here: it waits for the dev server to
                // print "open your browser on <url>", which is the old webpack message.
                // Angular's esbuild dev server prints "Local: <url>" instead, so it always
                // times out. Development launches `ng serve` via Microsoft.AspNetCore.SpaProxy
                // (see SpaProxyServerUrl/SpaProxyLaunchCommand in OV_DB.csproj) instead.
            });
        }

        public static IEdmModel GetEdmModel()
        {
            var builder = new ODataConventionModelBuilder();
            builder.EntitySet<RouteInstance>("RouteInstances");
            builder.EntitySet<Route>("Routes");
            builder.EntitySet<Region>("Regions");
            builder.EntitySet<RouteType>("Types");
            return builder.GetEdmModel();
        }
    }
}
