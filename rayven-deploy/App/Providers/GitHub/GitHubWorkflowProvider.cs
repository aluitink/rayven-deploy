using Rayven.Deploy.App.Providers.GitHub.Models;
using System.Net.Http.Json;

namespace Rayven.Deploy.App.Providers.GitHub
{
    public class GitHubWorkflowProvider
    {
        private readonly string _repositoryOwner;
        private readonly string _repositoryName;
        private readonly string _repositoryWorkflowPath;
        private readonly HttpClient _httpClient;
        public GitHubWorkflowProvider(string repositoryOwner, string repositoryName, string workflowPath, HttpClient httpClient)
        {
            _repositoryOwner = repositoryOwner;
            _repositoryName = repositoryName;
            _repositoryWorkflowPath = workflowPath;
            _httpClient = httpClient;
        }
        public async Task<Workflow?> GetWorkflowAsync(CancellationToken cancellationToken = default)
        {
            var workflowsUri = GitWorkflowsUri(_repositoryOwner, _repositoryName);
            var workflows = await _httpClient.GetFromJsonAsync<WorkflowsCollectionResponse>(workflowsUri, cancellationToken);
            return workflows?.Workflows.FirstOrDefault(w => w.Path.Equals(_repositoryWorkflowPath, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<WorkflowRun?> GetLastWorkflowRunAsync(long workflowId, CancellationToken cancellationToken = default)
        {
            var workflowRunsUri = GitWorkflowRunsUri(_repositoryOwner, _repositoryName, workflowId);
            var workflowRuns = await _httpClient.GetFromJsonAsync<WorkflowRunsCollectionResponse>(workflowRunsUri, cancellationToken);
            return workflowRuns?.WorkflowRuns.OrderByDescending(w => w.CreatedUtc).FirstOrDefault();
        }

        public async Task<WorkflowRun?> GetActiveWorkflowRunAsync(long workflowId, CancellationToken cancellationToken = default)
        {
            var workflowRunsUri = GitWorkflowRunsUri(_repositoryOwner, _repositoryName, workflowId);
            var workflowRuns = await _httpClient.GetFromJsonAsync<WorkflowRunsCollectionResponse>(workflowRunsUri, cancellationToken);
            return workflowRuns?.WorkflowRuns.OrderByDescending(w => w.CreatedUtc).Where(w => WorkflowRun.ActiveStatuses.Contains(w.Status)).FirstOrDefault();
        }

        public async Task<WorkflowRun?> GetWorkflowRunAsync(long runId, CancellationToken cancellationToken = default)
        {
            var workflowRunUri = GitWorkflowRunUri(_repositoryOwner, _repositoryName, runId);
            return await _httpClient.GetFromJsonAsync<WorkflowRun>(workflowRunUri, cancellationToken);
        }

        public async Task RunWorkflowAsync(long workflowId, string deploymentToken, string branch, CancellationToken cancellationToken = default)
        {
            var workflowsUri = GitWorkflowDispatchUri(_repositoryOwner, _repositoryName, workflowId);
            var content = JsonContent.Create(new { @ref = branch, inputs = new { deploymentToken } });
            var result = await _httpClient.PostAsync(workflowsUri, content, cancellationToken);
            result.EnsureSuccessStatusCode();
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
    }
}
