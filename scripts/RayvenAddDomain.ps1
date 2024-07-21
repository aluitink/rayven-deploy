param (
    [string]$apiKey,
    [string]$apiUrl = "http://localhost:7093",
    [string]$subDomainName,
    [string]$targetDomainName
)
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
        # Make the POST request
        $response = Invoke-RestMethod -Uri $Uri -Method Post -Headers $headers -Body $jsonBody

        # Output the response
        return $response
    }
    catch {
        Write-Error "Failed to send domain request: $_"
    }
}

$response = Send-DomainRequest -ApiKey $apiKey -SubDomainName $subDomainName -TargetDomainName $targetDomainName -Uri $apiUrl

$DeploymentScriptOutputs = @{}
if($response)
{
    $DeploymentScriptOutputs['conclusion'] = $true
}
else
{
    Write-Error "Unable to register subdomain"
}
