using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace rayven_deploy
{
    public class Function1
    {

        public static bool IsLinux() => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        private readonly ILogger<Function1> _logger;

        public Function1(ILogger<Function1> logger)
        {
            _logger = logger;
        }

        [Function("Function1")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            _logger.LogInformation($"Environment Is Linux {IsLinux()}");


            var windowsDlPath = "https://swalocaldeploy.azureedge.net/downloads/1.0.027044/windows/StaticSitesClient.exe";
            var linuxDlPath = "https://swalocaldeploy.azureedge.net/downloads/1.0.027044/linux/StaticSitesClient";

            var dlPath = windowsDlPath;
            if (IsLinux())
                dlPath = linuxDlPath;

            var tempPath = Path.GetTempPath();

            HttpClient client = new HttpClient();
            var downloadedFile = await client.GetStreamAsync(dlPath);
            var staticSitesClient = Path.Join(tempPath, "StaticSitesClient");
            if (File.Exists(staticSitesClient))
                File.Delete(staticSitesClient);
            using (var fs = new FileStream(staticSitesClient, FileMode.CreateNew))
            {
                await downloadedFile.CopyToAsync(fs);
            }

            var process = Process.Start(staticSitesClient, "upload --help");
            var sb = new StringBuilder();
            process.OutputDataReceived += (sender, args) => {
                sb.AppendLine(args.Data);
            };
            process.WaitForExit();

            _logger.LogDebug(sb.ToString());

            return new OkObjectResult(sb.ToString());
        }
    }
}
