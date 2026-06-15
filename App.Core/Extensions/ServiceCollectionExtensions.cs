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

public static class ServiceCollectionExtensions
{
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
