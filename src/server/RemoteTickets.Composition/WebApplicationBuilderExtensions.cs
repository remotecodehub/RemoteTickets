namespace RemoteTickets.Composition;

/// <summary>Provides extension methods that compose RemoteTickets application services and HTTP middleware.</summary>
public static class WebApplicationBuilderExtensions
{
    extension (WebApplicationBuilder builder)
    {
        /// <summary>Registers all application, persistence, identity, validation, authentication, authorization, and tenant services.</summary>
        /// <returns>The same application builder for fluent composition.</returns>
        /// <exception cref="InvalidOperationException">Thrown when required JWT configuration is missing or its secret key is too short.</exception>
        public WebApplication BuildRemoteTickets()
        {
            var services = builder.Services;
            var configuration = builder.Configuration;

            services.AddRazorComponents()
                .AddInteractiveServerComponents();
            services.AddMudServices();
            services.AddControllers();
            services.AddHttpClient();
            services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
            services.AddSingleton<ISetupConfigurationStore, SetupConfigurationStore>();
            services.AddDbContext<RemoteTicketsDbContext>((sp, options) =>
            {
                var setup = sp.GetRequiredService<ISetupConfigurationStore>();
                var connectionString = setup.GetMasterConnectionString() ?? configuration.GetConnectionString("Identity") ?? configuration.GetConnectionString("Master");
                if (string.IsNullOrWhiteSpace(connectionString)) throw new InvalidOperationException("A master database connection string is required.");
                options.UseSqlServer(connectionString);
            });

            services.AddIdentityCore<User>(options =>
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
            })
            .AddRoles<Role>()
            .AddEntityFrameworkStores<RemoteTicketsDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

            services.AddScoped<IIdentityService, IdentityService>();
            services.AddScoped<ITenantManagementService, TenantManagementService>();
            services.AddScoped<TenantAccessMiddleware>();
            services.AddScoped<IMessageValidator, FluentMessageValidator>();
            services.AddSingleton<IRevokedTokenStore, RevokedTokenStore>();
            services.AddScoped<IJwtTokenService, JwtTokenService>();
            services.AddScoped<IIdentityEmailSender, LoggingIdentityEmailSender>();
            services.AddValidatorsFromAssemblyContaining<RegisterCommandValidator>();

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
            {
                var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? throw new InvalidOperationException("JWT configuration is missing.");
                if (Encoding.UTF8.GetByteCount(jwt.SecretKey) < 32) throw new InvalidOperationException("Authentication:Jwt:SecretKey must contain at least 256 bits.");
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
                        if (!string.Equals(tokenType, JwtTokenTypes.Access, StringComparison.Ordinal))
                        {
                            context.Fail("The supplied token is not an access token.");
                            return Task.CompletedTask;
                        }
                        if (token is not null && context.HttpContext.RequestServices.GetRequiredService<IRevokedTokenStore>().IsRevoked(token.Id)) context.Fail("The supplied token has been revoked.");
                        return Task.CompletedTask;
                    }
                };
            });

            services.AddAuthorizationBuilder()
                .AddPolicy(TenantPolicies.SysAdmin, policy => policy.RequireRole(TenantRoles.SysAdmin))
                .AddPolicy(TenantPolicies.TenantAdmin, policy => policy.RequireRole(TenantRoles.SysAdmin, TenantRoles.TenantAdmin))
                .AddPolicy(TenantPolicies.TenantOperator, policy => policy.RequireRole(TenantRoles.SysAdmin, TenantRoles.TenantAdmin, TenantRoles.TenantOperator))
                .AddPolicy(TenantPolicies.TenantAttendant, policy => policy.RequireRole(TenantRoles.SysAdmin, TenantRoles.TenantAdmin, TenantRoles.TenantOperator, TenantRoles.TenantAttendant))
                .AddPolicy(IdentityPolicies.Administrator, policy => policy.RequireRole(TenantRoles.SysAdmin));

            var mb = new Mediator.Net.MediatorBuilder();
            mb.RegisterHandlers(typeof(IdentityHandlers).Assembly)
                .ConfigureCommandReceivePipe(pipe => pipe.UseValidation())
                .ConfigureRequestPipe(pipe => pipe.UseValidation());
            services.RegisterMediator(mb);
            return builder.Build();
        }
    }
}
