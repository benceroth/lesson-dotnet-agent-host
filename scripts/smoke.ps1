# Sends a few activities to the running host using the SDK's expectReplies delivery mode,
# so replies come back in the HTTP response. No Playground needed.
# Usage: dotnet run --project src/Northstar.Agent.Host   (in another terminal)
#        ./scripts/smoke.ps1
param([string]$BaseUrl = "http://localhost:3978")

$ErrorActionPreference = "Stop"
$conversation = "smoke-" + [guid]::NewGuid().ToString("N").Substring(0, 8)

function Send([string]$text) {
    $activity = @{
        type         = "message"
        id           = [guid]::NewGuid().ToString("N")
        text         = $text
        channelId    = "webchat"
        serviceUrl   = "http://localhost:9/"
        deliveryMode = "expectReplies"
        from         = @{ id = "smoke-user" }
        recipient    = @{ id = "agent" }
        conversation = @{ id = $conversation }
    } | ConvertTo-Json -Depth 5
    $response = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/messages" -ContentType "application/json" -Body $activity
    Write-Host "you   > $text"
    foreach ($reply in $response.activities) { Write-Host "agent > $($reply.text)" }
}

Send "Where is order NS-1042?"
Send "What about NS-1043?"
Send "Can I return it?"
Send "Where is NS-2001?"
Send "Show my recent orders"
