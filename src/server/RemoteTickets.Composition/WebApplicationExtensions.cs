namespace RemoteTickets.Composition;

/// <summary>Provides extension methods that compose RemoteTickets application services and HTTP middleware.</summary>
public static class WebApplicationExtensions
{
    extension (WebApplication app)
    {
        /// <summary>Configures middleware, authentication, tenant isolation, authorization, controllers, static assets, and Blazor endpoints.</summary>
        /// <typeparam name="TApp">The Blazor root component used by the application.</typeparam>
        /// <returns>The same web application for fluent startup composition.</returns>
        public async Task RunRemoteTickets<TApp>() where TApp : IComponent
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
            app.UseRouting();
            app.UseAuthentication();
            app.UseMiddleware<TenantAccessMiddleware>();
            app.UseAuthorization();
            app.UseAntiforgery();
            app.MapControllers();
            app.MapStaticAssets();
            app.MapRazorComponents<TApp>()
                .AddInteractiveServerRenderMode();
            await app.RunAsync();
        }
    }
}
