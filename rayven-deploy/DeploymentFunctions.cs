using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Dns;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;
using Rayven.Deploy.App.Providers.GitHub;
using Rayven.Deploy.App.Providers.GitHub.Models;
using System.Net.Http.Json;

namespace Rayven.Deploy.App.Models
{
    public class DeploymentFunctions
    {
        private readonly GitHubWorkflowProvider _gitHubWorkflowProvider;
        private readonly ApiSettings _apiSettings;
        private readonly AuthenticationSettings _authSettings;

        public DeploymentFunctions(GitHubWorkflowProvider gitHubWorkflowController, IOptions<ApiSettings> apiOptions, IOptions<AuthenticationSettings> authOptions)
        {
            _gitHubWorkflowProvider = gitHubWorkflowController;
            _apiSettings = apiOptions.Value;
            _authSettings = authOptions.Value;
        }

        [Function("Deploy")]
        public async Task<IActionResult> DeployAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
        {
            if (!IsAuthorized(req))
                return new UnauthorizedObjectResult("Invalid authorization");

            var deploymentRequest = await req.ReadFromJsonAsync<DeploymentRequest>();
            if (deploymentRequest == null)
                return new BadRequestObjectResult("Please post a DeploymentRequest");
            if (string.IsNullOrWhiteSpace(deploymentRequest.DeploymentToken))
                return new BadRequestObjectResult("Please post a DeploymentRequest.DeploymentToken");
            var workflow = await _gitHubWorkflowProvider.GetWorkflowAsync();
            if (workflow == null)
                return new NotFoundObjectResult("Unable to locate workflow");

            var activeWorkflowRun = await _gitHubWorkflowProvider.GetActiveWorkflowRunAsync(workflow.Id);
            if (activeWorkflowRun != null)
                return new ConflictObjectResult("An active deployment is in progress, please try again shortly...");

            await _gitHubWorkflowProvider.RunWorkflowAsync(workflow.Id, deploymentRequest.DeploymentToken, _apiSettings.GithubRepositoryBranch /* replace with branch from request */);
            int attempt = 0;
            do
            {
                attempt++;
                activeWorkflowRun = await _gitHubWorkflowProvider.GetActiveWorkflowRunAsync(workflow.Id);
                // backoff
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
            }
            while (activeWorkflowRun == null && attempt < deploymentRequest.MaxAttempts);
            if (activeWorkflowRun == null)
                return new BadRequestObjectResult("Unable to locate active workflow run");
            return new OkObjectResult(activeWorkflowRun);
        }
        [Function("Status")]
        public async Task<IActionResult> StatusAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
        {
            if (!IsAuthorized(req))
                return new UnauthorizedObjectResult("Invalid authorization");
            var workflowRun = await req.ReadFromJsonAsync<WorkflowRun>();
            if (workflowRun == null)
                return new BadRequestObjectResult("Please post a github WorkflowRun for it's status");
            var newWorkflowRun = await _gitHubWorkflowProvider.GetWorkflowRunAsync(workflowRun.Id);
            return new OkObjectResult(newWorkflowRun);
        }

        [Function("AddDomain")]
        public async Task<IActionResult> AddDomainAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
        {
            if (!IsAuthorized(req))
                return new UnauthorizedObjectResult("Invalid authorization");

            var domainRequest = await req.ReadFromJsonAsync<DomainRequest>();
            if (domainRequest == null)
                return new BadRequestObjectResult("Please post a DomainRequest");

            var credential = new ClientSecretCredential(_authSettings.TenantId, _authSettings.ClientId, _authSettings.ClientSecret);
            // Create a new resource management client with DefaultAzureCredential
            ArmClient armClient = new ArmClient(credential);

            // Get the default subscription
            var subscription = await armClient.GetDefaultSubscriptionAsync();

            var zoneResource = _apiSettings.DnsZoneResourceId;
            if (!ResourceIdentifier.TryParse(zoneResource, out var resourceIdentifier))
                return new BadRequestResult();

            var resourceGroup = await subscription.GetResourceGroupAsync(resourceIdentifier.ResourceGroupName);
            var dnsZones = resourceGroup.Value.GetDnsZones();
            var myZone = dnsZones.FirstOrDefault(z => z.Id.Equals(resourceIdentifier));

            if (myZone == null)
                return new BadRequestResult();

            var newCname = new DnsCnameRecordData()
            {
                Cname = domainRequest.TargetDomainName,
                TtlInSeconds = (long)TimeSpan.FromMinutes(10).TotalSeconds
            };

            var cnameRecordsCollection = myZone.GetDnsCnameRecords();
            var result = await cnameRecordsCollection.CreateOrUpdateAsync(Azure.WaitUntil.Completed, domainRequest.SubDomainName, newCname);

            return new OkObjectResult(result.Value.Data);
        }


        private bool IsAuthorized(HttpRequest req)
        {
            var authHeader = req.Headers.Authorization.FirstOrDefault();
            var authHeaderParts = authHeader.Split(" ", StringSplitOptions.RemoveEmptyEntries);
            if (authHeaderParts == null || authHeaderParts.Length < 2)
                return false;
            var token = authHeaderParts[1];
            if (_apiSettings.ArmTemplatePsk.Equals(token, StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }
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


    }
}
