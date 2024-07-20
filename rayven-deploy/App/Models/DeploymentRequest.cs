namespace Rayven.Deploy.App.Models
{
    public class DeploymentRequest
    {
        public string DeploymentToken { get; set; }
        public int MaxAttempts { get; set; }
    }
}
