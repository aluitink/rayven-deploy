param (
    [string]$owner,
    [string]$repo,
    [string]$token,
    [string]$deploymentToken,
    [string]$workflowPath,
    [string]$branch = "main",  # Default branch is set to "main"
    [int]$pollingInterval = 10  # Default polling interval set to 10 seconds
)

# Function to make GitHub API requests
function Invoke-GitHubAPI ($url, $method, $body = $null) {
    try {
        $headers = @{
            Authorization = "Bearer $token"
            "Accept" = "application/vnd.github+json"
            "X-GitHub-Api-Version" = "2022-11-28"
            "Content-Type" = "application/json"
        }
        return Invoke-RestMethod -Uri $url -Method $method -Headers $headers -Body ($body | ConvertTo-Json)
    } catch {
        throw "Error invoking GitHub API: $_"
    }
}

# Get the list of workflows and select the workflow by path
try {
    $workflows = Invoke-GitHubAPI "https://api.github.com/repos/$owner/$repo/actions/workflows" "Get"
    $workflow = $workflows.workflows | Where-Object { $_.path -eq $workflowPath }

    if (-not $workflow) { throw "Unable to find workflow with path '$workflowPath'" }

    $workflowId = $workflow.id
} catch {
    Write-Error $_
    exit 1
}

# Dispatch the selected workflow with deploymentToken
try {
    Invoke-GitHubAPI "https://api.github.com/repos/$owner/$repo/actions/workflows/$workflowId/dispatches" "Post" @{
        ref = $branch  # Use the specified branch
        inputs = @{
            deploymentToken = $deploymentToken
        }
    }
} catch {
    Write-Error "Failed to dispatch the workflow."
    exit 1
}

# Wait a short period to ensure the workflow run is registered
Start-Sleep -Seconds 5

# Poll the workflow runs to get the runId of the most recent run
try {
    $workflowRunsUrl = "https://api.github.com/repos/$owner/$repo/actions/workflows/$workflowId/runs"
    $workflowRuns = Invoke-GitHubAPI $workflowRunsUrl "Get"
    $runId = $workflowRuns.workflow_runs | Sort-Object -Property created_at -Descending | Select-Object -First 1 -ExpandProperty id
} catch {
    Write-Error "Failed to retrieve workflow runs."
    exit 1
}

# Poll the workflow run status until it is finished
$runCompleted = $false
while (-not $runCompleted) {
    Start-Sleep -Seconds $pollingInterval
    try {
        $runStatusUrl = "https://api.github.com/repos/$owner/$repo/actions/runs/$runId"
        $run = Invoke-GitHubAPI $runStatusUrl "Get"
        if ($run.status -eq "completed" -or $run.status -eq "failure") {
            $runCompleted = $true
            Write-Output "Workflow run completed with conclusion: $($run.conclusion)"
        }
    } catch {
        Write-Error "Error polling workflow run status: $_"
        exit 1
    }
}

Write-Output "Workflow has finished executing."
$DeploymentScriptOutputs = @{}
$DeploymentScriptOutputs['conclusion'] = $run.conclusion