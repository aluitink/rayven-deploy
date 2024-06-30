using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Reflection;
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
        public static string AssemblyDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
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
            var fileName = IsLinux() ? "StaticSitesClient" : "StaticSitesClient-Win.exe";
            var staticSitesClientFilePath = Path.Join(tempPath, fileName);
            _logger.LogInformation($"The file path is: {staticSitesClientFilePath}");

            if (File.Exists(staticSitesClientFilePath))
                File.Delete(staticSitesClientFilePath);
            using (var fs = new FileStream(staticSitesClientFilePath, FileMode.CreateNew))
            {
                await downloadedFile.CopyToAsync(fs);
                await fs.FlushAsync();
            }

            _logger.LogInformation("The file was created");

            if (IsLinux())
            {
                File.SetUnixFileMode(staticSitesClientFilePath, UnixFileMode.OtherExecute | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.UserRead | UnixFileMode.GroupRead);
            }

            ProcessStartInfo info = new ProcessStartInfo
            {
                WorkingDirectory = tempPath,
                FileName = staticSitesClientFilePath,
                Arguments = "upload --help",
                WindowStyle = ProcessWindowStyle.Minimized,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process proc = new Process
            {
                StartInfo = info
            };
            var sb = new StringBuilder();
            proc.Refresh();
            proc.StartInfo.RedirectStandardOutput = true;
            proc.Start();

            while (!proc.StandardOutput.EndOfStream)
            {
                sb.AppendLine(proc.StandardOutput.ReadLine());
            }

            proc.WaitForExit();

            _logger.LogInformation(sb.ToString());

            return new OkObjectResult("Exeucted");
        }
    }
}
