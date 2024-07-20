using System.Text.Json.Serialization;

namespace Rayven.Deploy.App.Providers.GitHub.Models
{
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

        public static string[] ErrorStatuses =
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
}
