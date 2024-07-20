using System.Text.Json.Serialization;

namespace Rayven.Deploy.App.Providers.GitHub.Models
{
    public class WorkflowsCollectionResponse
    {
        [JsonPropertyName("total_count")]
        public long TotalCount { get; set; }
        [JsonPropertyName("workflows")]
        public List<Workflow> Workflows { get; set; }
    }
}
