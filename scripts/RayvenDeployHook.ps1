param (
    [string]$deploymentToken,
    [string]$apiUrl = "http://localhost:7093",
    [string]$apiKey,
    [string]$subDomainName,
    [string]$targetDomainName,
    [int]$pollingInterval = 10,  # Default polling interval set to 10 seconds
    [int]$maxAttempts = 3
)

function Send-DeploymentRequest {
    param (
        [string]$uri,
        [string]$apiKey,
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
            $headers = @{
                "Authorization" = "Bearer $apiKey"
                "Content-Type"  = "application/json"
            }
            $response = Invoke-RestMethod -Uri "$uri/api/Deploy" -Method Post -Headers $headers -Body $jsonDeploymentRequest  -ErrorAction Stop
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
        [string]$apiKey,
        $workflowRun
    )
    $headers = @{
        "Authorization" = "Bearer $apiKey"
        "Content-Type"  = "application/json"
    }
    $jsonWorkflowRun = $workflowRun | ConvertTo-Json
    $statusResponse = Invoke-RestMethod -Uri "$uri/api/Status" -Method Post -Headers $headers -Body $jsonWorkflowRun
    return $statusResponse
}

function Poll-ForCompletion {
    param (
        [string]$uri,
        [string]$apiKey,
        $workflowRun,
        [int]$interval
    )

    do {
        $statusResponse = Get-WorkflowStatus -uri $uri -apiKey $apiKey -workflowRun $workflowRun
        $status = $statusResponse.Status

        if ($status -ne "completed") {
            Write-Output "Current status: $status. Polling again in $interval seconds..."
            Start-Sleep -Seconds $interval
        }
    } while ($status -ne "completed")

    return $statusResponse
}

function Send-DomainRequest {
    param (
        [string]$ApiKey,
        [string]$SubDomainName,
        [string]$TargetDomainName,
        [string]$Uri
    )

    # Create the request body as a hashtable
    $body = @{
        SubDomainName   = $SubDomainName
        TargetDomainName = $TargetDomainName
    }

    # Convert the body to JSON
    $jsonBody = $body | ConvertTo-Json

    # Define the headers including the authorization header
    $headers = @{
        "Authorization" = "Bearer $ApiKey"
        "Content-Type"  = "application/json"
    }

    try {
        $requestUri 

        # Make the POST request
        $response = Invoke-RestMethod -Uri "$Uri/api/AddDomain" -Method Post -Headers $headers -Body $jsonBody

        # Output the response
        return $response
    }
    catch {
        Write-Error "Failed to send domain request: $_"
    }
}

# Main script logic
$domainResponse = Send-DomainRequest -ApiKey $apiKey -SubDomainName $subDomainName -TargetDomainName $targetDomainName -Uri $apiUrl
if ($domainResponse) {
    $response = Send-DeploymentRequest -uri $apiUrl -apiKey $apiKey -token $deploymentToken -interval $pollingInterval -attempts $maxAttempts
    if ($null -ne $response) {
        $completedWorkflow = Poll-ForCompletion -uri $apiUrl -apiKey $apiKey -workflowRun $response -interval $pollingInterval
        Write-Output "Workflow has finished executing."
        $DeploymentScriptOutputs = @{}
        $DeploymentScriptOutputs['conclusion'] = $completedWorkflow.conclusion
    } else {
        Write-Output "Failed to receive a valid response from /api/Deploy."
    }
} else {
    Write-Output "Failed to register sub-domain."
}