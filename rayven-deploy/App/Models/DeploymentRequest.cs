namespace Rayven.Deploy.App.Models
{
    public class DeploymentRequest
    {
        public string DeploymentToken { get; set; }
        public int MaxAttempts { get; set; }
    }
    public class DomainRequest
    {
        public string SubDomainName { get; set; }
        public string TargetDomainName { get; set; }
    }
}
