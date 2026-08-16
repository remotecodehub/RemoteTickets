namespace RemoteTickets.Composition;

/// <summary>Provides extension methods that compose RemoteTickets application services and HTTP middleware.</summary>
public static class RemoteTicketsCompositionExtensions
{
    extension (WebApplicationBuilder builder)
    {
        /// <summary>Registers all application, persistence, identity, validation, authentication, and authorization services.</summary>
        /// <returns>The same application builder for fluent composition.</returns>
        /// <exception cref="InvalidOperationException">Thrown when required JWT configuration is missing or its secret key is too short.</exception>
        public WebApplicationBuilder AddRemoteTickets()
        {
            var services = builder.Services;
            var configuration = builder.Configuration;

            services.AddRazorComponents()
                .AddInteractiveServerComponents();
            services.AddControllers();

            services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

            services.AddDbContext<RemoteTicketsDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("Identity")));

            services.AddIdentityCore<IdentityUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = false;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            }).AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<RemoteTicketsDbContext>()
                .AddSignInManager()
                .AddDefaultTokenProviders();

            services.AddScoped<IIdentityService, IdentityService>();
            services.AddScoped<IMessageValidator, FluentMessageValidator>();
            services.AddSingleton<IRevokedTokenStore, RevokedTokenStore>();
            services.AddScoped<IJwtTokenService, JwtTokenService>();
            services.AddScoped<IIdentityEmailSender, LoggingIdentityEmailSender>();
            services.AddValidatorsFromAssemblyContaining<RegisterCommandValidator>();

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                        ?? throw new InvalidOperationException("JWT configuration is missing.");

                    if (Encoding.UTF8.GetByteCount(jwt.SecretKey) < 32)
                    {
                        throw new InvalidOperationException("Authentication:Jwt:SecretKey must contain at least 256 bits.");
                    }

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
                        ValidateIssuer = true,
                        ValidIssuer = jwt.Issuer,
                        ValidateAudience = true,
                        ValidAudience = jwt.Audience,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromSeconds(30)
                    };

                    options.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = context =>
                        {
                            var token = context.SecurityToken as JwtSecurityToken;
                            var tokenType = token?.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Typ)?.Value;
                            if (!string.Equals(tokenType, "access", StringComparison.Ordinal))
                            {
                                context.Fail("The supplied token is not an access token.");
                                return Task.CompletedTask;
                            }

                            if (token is not null && context.HttpContext.RequestServices.GetRequiredService<IRevokedTokenStore>().IsRevoked(token.Id))
                            {
                                context.Fail("The supplied token has been revoked.");
                            }

                            return Task.CompletedTask;
                        }
                    };
                });

            services.AddAuthorizationBuilder()
                .AddPolicy(IdentityPolicies.Administrator, policy =>
                    policy.RequireClaim(IdentityClaimTypes.Permission, AdministratorPermission));
            var mb = new Mediator.Net.MediatorBuilder();
            mb.RegisterHandlers(typeof(IdentityHandlers).Assembly)
                .ConfigureCommandReceivePipe(pipe => pipe.UseValidation())
                .ConfigureRequestPipe(pipe => pipe.UseValidation());

            services.RegisterMediator(mb);

            return builder;
        }
    }

    extension (WebApplication app)
    {
        /// <summary>Configures middleware, authentication, authorization, controllers, static assets, and Blazor endpoints.</summary>
        /// <typeparam name="TApp">The Blazor root component used by the application.</typeparam>
        /// <returns>The same web application for fluent startup composition.</returns>
        public WebApplication UseRemoteTickets<TApp>() where TApp : IComponent
        {
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error", createScopeForErrors: true);
                app.UseHsts();
            }
            else
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseAntiforgery();

            app.MapControllers();
            app.MapStaticAssets();
            app.MapRazorComponents<TApp>()
                .AddInteractiveServerRenderMode();

            return app;
        }
    }

    private const string AdministratorPermission = "system.admin";
}
