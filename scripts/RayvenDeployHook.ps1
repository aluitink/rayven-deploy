param (
    [string]$deploymentToken,
    [string]$apiUrl = "http://localhost:7093",
    [int]$pollingInterval = 10  # Default polling interval set to 10 seconds
    [int]$maxAttempts = 3
)

function Send-DeploymentRequest {
    param (
        [string]$uri,
        [string]$token,
        [int]$interval,
        [int]$attempts
    )

    $deploymentRequest = @{
        DeploymentToken = $token
        MaxAttempts = $attempts
    }

    $jsonDeploymentRequest = $deploymentRequest | ConvertTo-Json

    do {
        try {
            $response = Invoke-RestMethod -Uri "$uri/api/Deploy" -Method Post -Body $jsonDeploymentRequest -ContentType "application/json" -ErrorAction Stop
            return $response
        } catch [System.Net.WebException] {
            $statusCode = $_.Exception.Response.StatusCode
            if ($statusCode -eq 409) {
                Write-Output "Received 409 Conflict. Retrying in $interval seconds..."
                Start-Sleep -Seconds $interval
            } else {
                throw $_
            }
        }
    } while ($true)
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
$response = Send-DeploymentRequest -uri $apiUrl -token $deploymentToken -interval $pollingInterval -attempts $maxAttempts

if ($null -ne $response) {
    $completedWorkflow = Poll-ForCompletion -uri $apiUrl -workflowRun $response -interval $pollingInterval
    Write-Output "Workflow has finished executing."

    $DeploymentScriptOutputs = Extract-NonNullProperties -obj $completedWorkflow
    Write-Output $DeploymentScriptOutputs
} else {
    Write-Output "Failed to receive a valid response from /api/Deploy."
}
