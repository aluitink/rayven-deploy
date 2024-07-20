using System.Text.Json.Serialization;

namespace Rayven.Deploy.App.Providers.GitHub.Models
{
    public class WorkflowRunsCollectionResponse
    {
        [JsonPropertyName("total_count")]
        public long TotalCount { get; set; }
        [JsonPropertyName("workflow_runs")]
        public List<WorkflowRun> WorkflowRuns { get; set; }
    }
}
