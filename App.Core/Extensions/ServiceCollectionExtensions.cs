using System;
using System.Net;
using System.Net.Http;
using App.Core.Abstractions;
using App.Core.Infrastructure;
using App.Core.Models;
using App.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace App.Core.Extensions;

/// <summary>
/// Provides extension methods for setting up App.Core services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds and configures all necessary services for the App.Core class library,
    /// including configuration binding, abstractions, and the primary HTTP client.
    /// </summary>
    /// <param name="services">The service collection to add the services to.</param>
    /// <param name="configuration">The application configuration properties.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddMyAppCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<PostImageUploaderOptions>(
            configuration.GetSection(PostImageUploaderOptions.ConfigurationSectionName));

        services.AddSingleton<IFileSystem, PhysicalFileSystem>();
        services.AddSingleton(TimeProvider.System);

        services.AddTransient<UploadValidator>();

        services.AddHttpClient<IPostImageClient, PostImageClient>()
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                UseCookies               = true,
                CookieContainer          = new CookieContainer(),
                AllowAutoRedirect        = true,
                MaxAutomaticRedirections = 5,
            });

        return services;
    }
}
