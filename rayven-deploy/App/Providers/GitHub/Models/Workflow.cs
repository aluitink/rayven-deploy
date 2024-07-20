using System.Text.Json.Serialization;

namespace Rayven.Deploy.App.Providers.GitHub.Models
{
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
}
