param (
    [string]$deploymentToken,
    [string]$apiUrl = "http://localhost:7093",
    [int]$pollingInterval = 10  # Default polling interval set to 10 seconds
)

function Send-DeploymentRequest {
    param (
        [string]$uri,
        [string]$token
    )

    $deploymentRequest = @{
        DeploymentToken = $token
    }

    $jsonDeploymentRequest = $deploymentRequest | ConvertTo-Json
    $response = Invoke-RestMethod -Uri "$uri/api/Deploy" -Method Post -Body $jsonDeploymentRequest -ContentType "application/json"
    return $response
}

function Get-WorkflowStatus {
    param (
        [string]$uri,
        $workflowRun
    )

    $jsonWorkflowRun = $workflowRun | ConvertTo-Json
    $statusResponse = Invoke-RestMethod -Uri "$uri/api/Status" -Method Post -Body $jsonWorkflowRun -ContentType "application/json"
    return $statusResponse
}

function Poll-ForCompletion {
    param (
        [string]$uri,
        $workflowRun,
        [int]$interval
    )

    do {
        $statusResponse = Get-WorkflowStatus -uri $uri -workflowRun $workflowRun
        $status = $statusResponse.Status

        if ($status -ne "completed") {
            Write-Output "Current status: $status. Polling again in $interval seconds..."
            Start-Sleep -Seconds $interval
        }
    } while ($status -ne "completed")

    return $statusResponse
}

function Extract-NonNullProperties {
    param (
        $obj
    )

    $nonNullProperties = @{}
    foreach ($property in $obj.PSObject.Properties) {
        if ($null -ne $property.Value) {
            $nonNullProperties[$property.Name] = $property.Value
        }
    }
    return $nonNullProperties
}

# Main script logic
$response = Send-DeploymentRequest -uri $apiUrl -token $deploymentToken

if ($null -ne $response) {
    $completedWorkflow = Poll-ForCompletion -uri $apiUrl -workflowRun $response -interval $pollingInterval
    Write-Output "Workflow has finished executing."

    $DeploymentScriptOutputs = Extract-NonNullProperties -obj $completedWorkflow
    Write-Output $DeploymentScriptOutputs
} else {
    Write-Output "Failed to receive a valid response from /api/Deploy."
}
