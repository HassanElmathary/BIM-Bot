<#
.SYNOPSIS
    Sends a JSON-RPC 2.0 command to the Revit MCP plugin via TCP.

.DESCRIPTION
    Connects to localhost:8080, sends a JSON-RPC request, and returns the
    JSON response. Used by the Antigravity /revit workflow.

.PARAMETER Method
    The Revit MCP tool name, e.g. "get_levels", "create_wall".

.PARAMETER Params
    JSON string of parameters. Default: "{}"

.PARAMETER Port
    TCP port. Default: 8080

.PARAMETER TimeoutSec
    Seconds to wait for a response. Default: 30

.EXAMPLE
    pwsh scripts/revit-cmd.ps1 -Method "get_levels" -Params "{}"

.EXAMPLE
    pwsh scripts/revit-cmd.ps1 -Method "create_wall" -Params '{"startX":0,"startY":0,"endX":20,"endY":0,"levelName":"Level 1","height":10}'
#>
param(
    [Parameter(Mandatory)]
    [string]$Method,

    [string]$Params = "{}",

    [int]$Port = 8080,

    [int]$TimeoutSec = 30
)

$ErrorActionPreference = "Stop"

# Build JSON-RPC 2.0 request
$request = @{
    jsonrpc = "2.0"
    method  = $Method
    params  = ($Params | ConvertFrom-Json)
    id      = 1
} | ConvertTo-Json -Depth 10 -Compress

try {
    # Connect
    $client = [System.Net.Sockets.TcpClient]::new()
    $client.SendTimeout    = $TimeoutSec * 1000
    $client.ReceiveTimeout = $TimeoutSec * 1000

    $connectTask = $client.ConnectAsync("127.0.0.1", $Port)
    if (-not $connectTask.Wait(5000)) {
        throw "Connection timed out. Is Revit running with MCP service started on port ${Port}?"
    }

    $stream = $client.GetStream()

    # Send
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($request)
    $stream.Write($bytes, 0, $bytes.Length)
    $stream.Flush()

    # Read response (buffered)
    $buffer   = New-Object byte[] 65536
    $response = [System.Text.StringBuilder]::new()
    $stream.ReadTimeout = $TimeoutSec * 1000

    do {
        $count = $stream.Read($buffer, 0, $buffer.Length)
        if ($count -gt 0) {
            [void]$response.Append([System.Text.Encoding]::UTF8.GetString($buffer, 0, $count))
        }
        # Give Revit a moment to flush remaining data
        Start-Sleep -Milliseconds 50
    } while ($stream.DataAvailable)

    $responseText = $response.ToString()

    if ([string]::IsNullOrWhiteSpace($responseText)) {
        throw "Empty response from Revit."
    }

    # Output the JSON response
    Write-Output $responseText
    exit 0
}
catch {
    Write-Error "revit-cmd error: $_"
    exit 1
}
finally {
    if ($null -ne $stream) { $stream.Dispose() }
    if ($null -ne $client) { $client.Dispose() }
}
