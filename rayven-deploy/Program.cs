using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using rayven_deploy;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddHttpClient("github", (sp, client) =>
        {
            var apiOptions = sp.GetRequiredService <IOptions<ApiSettings>>();
            var apiSettings = apiOptions.Value;
            client.DefaultRequestHeaders.Add("User-Agent", "rayven-deploy/1.0");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            if (!string.IsNullOrWhiteSpace(apiSettings.GithubAccessToken))
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiSettings.GithubAccessToken}");
        });
        services.AddOptions<ApiSettings>()
            .Configure<IConfiguration>((settings, configuration) =>
            {
                configuration.GetSection("ApiSettings").Bind(settings);
            });
        services.AddOptions<AuthenticationSettings>()
            .Configure<IConfiguration>((settings, configuration) =>
            {
                configuration.GetSection("AuthenticationSettings").Bind(settings);
            });
    })
    .Build();

host.Run();
