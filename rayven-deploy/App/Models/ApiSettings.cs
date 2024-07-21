namespace Rayven.Deploy.App.Models
{

    public class ApiSettings
    {
        public string GithubRepositoryOwner { get; set; }
        public string GithubRepositoryName { get; set; }
        public string GithubRepositoryBranch { get; set; }
        public string GithubAccessToken { get; set; }
        public string GithubWorkflowPath { get; set; }
        public string ArmTemplatePsk { get; set; }
        public string DnsZoneResourceId { get; set; } 
    }
}