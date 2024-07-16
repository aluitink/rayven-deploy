using Azure;
using Azure.Identity;
using Azure.ResourceManager.ContainerInstance.Models;
using Azure.ResourceManager.ContainerInstance;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace rayven_deploy
{
    public class AuthenticationSettings
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string TenantId { get; set; }
        public string SubscriptionId { get; set; }
    }

    public class ApiSettings
    {
        public string GithubRepositoryOwner { get; set; }
        public string GithubRepositoryName { get; set; }
        public string GithubRepositoryBranch { get; set; }
        public string GithubAccessToken { get; set; }
        public string GithubWorkflowPath { get; set; }
    }

    public class DeploymentRequest
    {
        public string DeploymentToken { get; set; }
        public int MaxAttempts { get; set; }
    }

    public class Workflow
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("state")]
        public string State { get; set; }
        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; set; }
        [JsonPropertyName("updated_at")]
        public DateTime? UpdatedAt { get; set; }
        [JsonPropertyName("node_id")]
        public string NodeId { get; set; }
        [JsonPropertyName("path")]
        public string Path { get; set; }
        [JsonPropertyName("url")]
        public string Url { get; set; }
        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; }
        [JsonPropertyName("badge_url")]
        public string BadgeUrl { get; set; }
    }

    public class WorkflowsCollectionResponse
    {
        [JsonPropertyName("total_count")]
        public long TotalCount { get; set; }
        [JsonPropertyName("workflows")]
        public List<Workflow> Workflows { get; set; }
    }

    public class WorkflowRun
    {
        public const string RunningStatus = "running";
        public const string QueuedStatus = "queued";
        public const string FailureStatus = "failure";
        public const string CancelledStatus = "cancelled";
        public const string CompletedStatus = "completed";
        public const string ActionRequiredStatus = "action_required";
        public const string NeutralStatus = "neutral";
        public const string SkippedStatus = "skipped";
        public const string StaleStatus = "stale";
        public const string SuccessStatus = "success";
        public const string TimeoutStatus = "time_out";
        public const string InProgressStatus = "in_progress";
        public const string RequestedStatus = "requested";
        public const string WaitingStatus = "waiting";
        public const string PendingStatus = "pending";

        public static string[] ActiveStatuses =
        [
            RunningStatus,
            QueuedStatus,
            ActionRequiredStatus,
            InProgressStatus,
            RequestedStatus,
            WaitingStatus,
            PendingStatus
        ];

        public static string[] CompleteStatuses = 
        [
            FailureStatus,
            CancelledStatus,
            CompletedStatus,
            NeutralStatus,
            SkippedStatus,
            StaleStatus,
            SuccessStatus,
            TimeoutStatus,
        ];

        public static string[] ErrorStatuses  =
        [
            FailureStatus,
            CancelledStatus,
            SkippedStatus,
            StaleStatus,
            TimeoutStatus,
        ];

        [JsonPropertyName("id")]
        public long Id { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("node_id")]
        public string NodeId { get; set; }
        [JsonPropertyName("head_branch")]
        public string HeadBranch { get; set; }
        [JsonPropertyName("head_sha")]
        public string HeadSha { get; set; }
        [JsonPropertyName("path")]
        public string Path { get; set; }
        [JsonPropertyName("display_title")]
        public string DisplayTitle { get; set; }
        [JsonPropertyName("run_number")]
        public int RunNumber { get; set; }
        [JsonPropertyName("event")]
        public string Event { get; set; }
        [JsonPropertyName("status")]
        public string Status { get; set; }
        [JsonPropertyName("conclusion")]
        public string Conclusion { get; set; }
        [JsonPropertyName("workflow_id")]
        public long WorkflowId { get; set; }
        [JsonPropertyName("check_suite_id")]
        public long CheckSuiteId { get; set; }
        [JsonPropertyName("check_suite_node_id")]
        public string CheckSuiteNodeId { get; set; }
        [JsonPropertyName("url")]
        public string Url { get; set; }
        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; }
        [JsonPropertyName("run_started_at")]
        public DateTime? StartedAt { get; set; }
        [JsonPropertyName("created_at")]
        public DateTime? CreatedUtc { get; set; }
        [JsonPropertyName("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
    public class WorkflowRunsCollectionResponse
    {
        [JsonPropertyName("total_count")]
        public long TotalCount { get; set; }
        [JsonPropertyName("workflow_runs")]
        public List<WorkflowRun> WorkflowRuns { get; set; }
    }

    public class DeploymentFunctions
    {
        //public static bool IsLinux() => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DeploymentFunctions> _logger;
        private readonly AuthenticationSettings _authenticationSettings;
        private readonly ApiSettings _apiSettings;

        public DeploymentFunctions(IHttpClientFactory httpClientFactory, IOptions<ApiSettings> apiOptions, IOptions<AuthenticationSettings> authenticationOptions, ILogger<DeploymentFunctions> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _apiSettings = apiOptions.Value;
            _authenticationSettings = authenticationOptions.Value;
        }

        private Uri GitWorkflowsUri(string owner, string repo)
        {
            return new Uri($"https://api.github.com/repos/{owner}/{repo}/actions/workflows");
        }
        private Uri GitWorkflowDispatchUri(string owner, string repo, long workflowId)
        {
            return new Uri($"https://api.github.com/repos/{owner}/{repo}/actions/workflows/{workflowId}/dispatches");
        }
        private Uri GitWorkflowRunsUri(string owner, string repo, long workflowId)
        {
            return new Uri($"https://api.github.com/repos/{owner}/{repo}/actions/workflows/{workflowId}/runs");
        }
        private Uri GitWorkflowRunUri(string owner, string repo, long runId)
        {
            return new Uri($"https://api.github.com/repos/{owner}/{repo}/actions/runs/{runId}");
        }

        protected async Task<Workflow?> GetWorkflowAsync(HttpClient client, CancellationToken cancellationToken = default)
        {
            var workflowsUri = GitWorkflowsUri(_apiSettings.GithubRepositoryOwner, _apiSettings.GithubRepositoryName);
            var workflows = await client.GetFromJsonAsync<WorkflowsCollectionResponse>(workflowsUri, cancellationToken);
            return workflows?.Workflows.FirstOrDefault(w => w.Path.Equals(_apiSettings.GithubWorkflowPath, StringComparison.OrdinalIgnoreCase));
        }

        protected async Task<WorkflowRun?> GetLastWorkflowRunAsync(HttpClient client, long workflowId)
        {
            var workflowRunsUri = GitWorkflowRunsUri(_apiSettings.GithubRepositoryOwner, _apiSettings.GithubRepositoryName, workflowId);
            var result = await client.GetStringAsync(workflowRunsUri);
            var workflowRuns = await client.GetFromJsonAsync<WorkflowRunsCollectionResponse>(workflowRunsUri);
            return workflowRuns?.WorkflowRuns.OrderByDescending(w => w.CreatedUtc).FirstOrDefault();
        }

        protected async Task<WorkflowRun?> GetActiveWorkflowRunAsync(HttpClient client, long workflowId)
        {
            var workflowRunsUri = GitWorkflowRunsUri(_apiSettings.GithubRepositoryOwner, _apiSettings.GithubRepositoryName, workflowId);
            var result = await client.GetStringAsync(workflowRunsUri);
            var workflowRuns = await client.GetFromJsonAsync<WorkflowRunsCollectionResponse>(workflowRunsUri);
            return workflowRuns?.WorkflowRuns.OrderByDescending(w => w.CreatedUtc).Where(w => WorkflowRun.ActiveStatuses.Contains(w.Status)).FirstOrDefault();
        }

        public async Task<WorkflowRun?> GetWorkflowRunAsync(HttpClient client, long runId)
        {
            var workflowRunUri = GitWorkflowRunUri(_apiSettings.GithubRepositoryOwner, _apiSettings.GithubRepositoryName, runId);
            return await client.GetFromJsonAsync<WorkflowRun>(workflowRunUri);
        }

        protected async Task RunWorkflowAsync(HttpClient client, long workflowId, string deploymentToken, CancellationToken cancellationToken = default)
        {
            var workflowsUri = GitWorkflowDispatchUri(_apiSettings.GithubRepositoryOwner, _apiSettings.GithubRepositoryName, workflowId);
            var content = JsonContent.Create(new { @ref = _apiSettings.GithubRepositoryBranch, inputs = new { deploymentToken = deploymentToken } });
            var result = await client.PostAsync(workflowsUri, content, cancellationToken);
            result.EnsureSuccessStatusCode();
        }
        [Function("Deploy")]
        public async Task<IActionResult> DeployAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
        {
            var deploymentRequest = await req.ReadFromJsonAsync<DeploymentRequest>();
            if (deploymentRequest == null)
                return new BadRequestObjectResult("Please post a DeploymentRequest");
            if (string.IsNullOrWhiteSpace(deploymentRequest.DeploymentToken))
                return new BadRequestObjectResult("Please post a DeploymentRequest.DeploymentToken");
            using (var client = _httpClientFactory.CreateClient("github"))
            {
                var workflow = await GetWorkflowAsync(client);
                if (workflow == null)
                    return new NotFoundObjectResult("Unable to locate workflow");

                var activeWorkflowRun = await GetActiveWorkflowRunAsync(client, workflow.Id);
                if (activeWorkflowRun != null)
                    return new ConflictObjectResult("An active deployment is in progress, please try again shortly...");

                await RunWorkflowAsync(client, workflow.Id, deploymentRequest.DeploymentToken);

                int attempt = 0;
                do
                {
                    attempt++;
                    activeWorkflowRun = await GetActiveWorkflowRunAsync(client, workflow.Id);
                    // backoff
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
                }
                while (activeWorkflowRun == null && attempt < deploymentRequest.MaxAttempts);
                if (activeWorkflowRun == null)
                    return new BadRequestObjectResult("Unable to locate active workflow run");
                return new OkObjectResult(activeWorkflowRun);
            }
        }
        [Function("Status")]
        public async Task<IActionResult> StatusAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
        {
            var workflowRun = await req.ReadFromJsonAsync<WorkflowRun>();
            if (workflowRun == null)
                return new BadRequestObjectResult("Please post a github WorkflowRun for it's status");
            using (var client = _httpClientFactory.CreateClient("github"))
            {
                var newWorkflowRun = await GetWorkflowRunAsync(client, workflowRun.Id);
                return new OkObjectResult(newWorkflowRun);
            }
        }

        //public static string AssemblyDirectory
        //{
        //    get
        //    {
        //        string codeBase = Assembly.GetExecutingAssembly().CodeBase;
        //        UriBuilder uri = new UriBuilder(codeBase);
        //        string path = Uri.UnescapeDataString(uri.Path);
        //        return Path.GetDirectoryName(path);
        //    }
        //}

        //    [Function("Deploy")]
        //    public async Task<IActionResult> Deploy([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
        //    {
        //        await CreateContainerInstanceAsync("rayven-deploy-containers");
        //        return new OkObjectResult("ok");
        //    }
        //    public async Task CreateContainerInstanceAsync(string resourceGroupName)
        //    {
        //        var credential = new ClientSecretCredential(_authenticationSettings.TenantId, _authenticationSettings.ClientId, _authenticationSettings.ClientSecret);
        //        // Create a new resource management client with DefaultAzureCredential
        //        ArmClient armClient = new ArmClient(credential);

        //        // Get the default subscription
        //        SubscriptionResource subscription = await armClient.GetDefaultSubscriptionAsync();

        //        // Ensure the resource group exists
        //        ResourceGroupCollection resourceGroups = subscription.GetResourceGroups();
        //        var resourceGroup = await resourceGroups.GetIfExistsAsync(resourceGroupName) ?? throw new Exception($"Resource group {resourceGroupName} not found.");

        //        // Get the location of the resource group
        //        var location = resourceGroup?.Value?.Data?.Location;

        //        // Generate a unique container group name
        //        string containerGroupName = $"rayven-deploy-{Guid.NewGuid().ToString().Substring(0, 8)}";
        //        string containerName = containerGroupName; // Using the same name for simplicity

        //        // Set the container properties
        //        string containerImage = "swacli/static-web-apps-cli:latest";
        //        int cpu = 1;
        //        double memoryInGB = 1.5;

        //        // Define environment variables for the container
        //        var environmentVariables = new[]
        //        {
        //            new ContainerEnvironmentVariable("SECRET_KEY")
        //            {
        //                SecureValue = "your_secret_value" // Mark as secret
        //            },
        //            // Add more environment variables as needed
        //        };

        //        // Define the container group configuration
        //        var containers = new List<ContainerInstanceContainer>();
        //        containers.Add(new ContainerInstanceContainer(containerName, containerImage, new ContainerResourceRequirements(new ContainerResourceRequestsContent(memoryInGB, cpu))));

        //        var cGroupData = new ContainerGroupData(location.Value, containers, ContainerInstanceOperatingSystemType.Linux);
        //        cGroupData.RestartPolicy = ContainerGroupRestartPolicy.Never;

        //        // Create the container group
        //        ContainerGroupCollection containerGroups = resourceGroup.Value.GetContainerGroups();
        //        ArmOperation<ContainerGroupResource> containerGroupOperation = await containerGroups.CreateOrUpdateAsync(WaitUntil.Completed, containerGroupName, cGroupData);
        //        ContainerGroupResource containerGroup = containerGroupOperation.Value;

        //        Console.WriteLine($"Container group '{containerGroupName}' created successfully.");

        //        // Wait for a specified duration before deleting the container group (e.g., 5 minutes)
        //        await Task.Delay(TimeSpan.FromMinutes(5));

        //        // Delete the container group
        //        await containerGroup.DeleteAsync(WaitUntil.Completed);
        //        Console.WriteLine($"Container group '{containerGroupName}' deleted successfully.");
        //    }
        //}

        //[Function("Function1")]
        //public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
        //{
        //    _logger.LogInformation("C# HTTP trigger function processed a request.");

        //    _logger.LogInformation($"Environment Is Linux {IsLinux()}");


        //    var windowsDlPath = "https://swalocaldeploy.azureedge.net/downloads/1.0.027044/windows/StaticSitesClient.exe";
        //    var linuxDlPath = "https://swalocaldeploy.azureedge.net/downloads/1.0.027044/linux/StaticSitesClient";

        //    var dlPath = windowsDlPath;
        //    if (IsLinux())
        //        dlPath = linuxDlPath;

        //    var tempPath = Path.GetTempPath();

        //    HttpClient client = new HttpClient();
        //    var downloadedFile = await client.GetStreamAsync(dlPath);
        //    var fileName = IsLinux() ? "StaticSitesClient" : "StaticSitesClient-Win.exe";
        //    var staticSitesClientFilePath = Path.Join(tempPath, fileName);
        //    _logger.LogInformation($"The file path is: {staticSitesClientFilePath}");

        //    if (File.Exists(staticSitesClientFilePath))
        //        File.Delete(staticSitesClientFilePath);
        //    using (var fs = new FileStream(staticSitesClientFilePath, FileMode.CreateNew))
        //    {
        //        await downloadedFile.CopyToAsync(fs);
        //        await fs.FlushAsync();
        //    }

        //    _logger.LogInformation("The file was created");

        //    if (IsLinux())
        //    {
        //        File.SetUnixFileMode(staticSitesClientFilePath, UnixFileMode.OtherExecute | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.UserRead | UnixFileMode.GroupRead);
        //    }

        //    var procStart = new ProcessStartInfo()
        //    {
        //        FileName = staticSitesClientFilePath,
        //        Arguments = "upload --apiToken 8a2e8c3d7d756b1855080fcd847b6d2fd4ca9ea969526068982a69a0ed4f65de5-0b924b46-2180-45f5-9aed-e8cd985c1be0010441889 --",
        //        RedirectStandardOutput = true,
        //        UseShellExecute = false,
        //    };

        //    var proc = Process.Start(procStart);
        //    proc.WaitForExit();

        //    while (!proc.StandardOutput.EndOfStream)
        //    {
        //        _logger.LogInformation($"OUTPUT: {proc.StandardOutput.ReadLine()}");
        //    }

        //    _logger.LogInformation($"Exited: {proc.ExitCode}");

        //    return new OkObjectResult("Exeucted");
        //}
        //}
    }
}
