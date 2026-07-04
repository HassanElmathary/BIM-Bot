<#
.SYNOPSIS
    Sends a JSON-RPC 2.0 command to the BIM-Bot plugin via TCP.

.DESCRIPTION
    Connects to localhost:8080, sends a JSON-RPC request using Content-Length
    framing protocol, and returns the JSON response. Used by the Antigravity
    /revit workflow.

    Includes automatic retry with backoff if Revit is not ready yet.

.PARAMETER Method
    The BIM-Bot tool name, e.g. "get_levels", "create_wall".

.PARAMETER Params
    JSON string of parameters. Default: "{}"

.PARAMETER Port
    TCP port. Default: 8080

.PARAMETER TimeoutSec
    Seconds to wait for a response. Default: 30

.PARAMETER RetryCount
    Number of connection retries. Default: 3

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

    [int]$TimeoutSec = 30,

    [int]$RetryCount = 3
)

$ErrorActionPreference = "Stop"

# Build JSON-RPC 2.0 request
$request = @{
    jsonrpc = "2.0"
    method  = $Method
    params  = ($Params | ConvertFrom-Json)
    id      = 1
} | ConvertTo-Json -Depth 10 -Compress

function Send-RevitCommand {
    param([string]$RequestJson, [int]$PortNum, [int]$Timeout)

    $client = $null
    $stream = $null

    try {
        # Connect
        $client = [System.Net.Sockets.TcpClient]::new()
        $client.SendTimeout    = $Timeout * 1000
        $client.ReceiveTimeout = $Timeout * 1000

        $connectTask = $client.ConnectAsync("127.0.0.1", $PortNum)
        if (-not $connectTask.Wait(5000)) {
            throw "Connection timed out. Is Revit running with BIM-Bot service on port ${PortNum}?"
        }

        $stream = $client.GetStream()

        # ── Send using Content-Length framing ──
        $payloadBytes = [System.Text.Encoding]::UTF8.GetBytes($RequestJson)
        $header = "Content-Length: $($payloadBytes.Length)`n"
        $headerBytes = [System.Text.Encoding]::UTF8.GetBytes($header)

        $stream.Write($headerBytes, 0, $headerBytes.Length)
        $stream.Write($payloadBytes, 0, $payloadBytes.Length)
        $stream.Flush()

        # ── Read response using Content-Length framing ──
        $buffer   = New-Object byte[] 65536
        $rawData  = [System.Text.StringBuilder]::new()
        $stream.ReadTimeout = $Timeout * 1000

        # Read until we have a complete Content-Length framed message
        $contentLength = -1
        $headerEnd = -1

        while ($true) {
            $count = $stream.Read($buffer, 0, $buffer.Length)
            if ($count -le 0) { throw "Connection closed before response received." }
            [void]$rawData.Append([System.Text.Encoding]::UTF8.GetString($buffer, 0, $count))

            $data = $rawData.ToString()

            # Try to parse Content-Length header
            if ($contentLength -lt 0) {
                if ($data.StartsWith("Content-Length: ")) {
                    $nlIdx = $data.IndexOf("`n")
                    if ($nlIdx -ge 0) {
                        $lenStr = $data.Substring("Content-Length: ".Length, $nlIdx - "Content-Length: ".Length).Trim()
                        $contentLength = [int]$lenStr
                        $headerEnd = $nlIdx + 1
                    }
                } else {
                    # Fallback: no Content-Length header — use legacy brace-counting
                    # Wait for complete JSON object
                    $trimmed = $data.TrimStart()
                    if ($trimmed.StartsWith("{")) {
                        $depth = 0; $inStr = $false; $escaped = $false
                        for ($i = 0; $i -lt $trimmed.Length; $i++) {
                            $c = $trimmed[$i]
                            if ($escaped) { $escaped = $false; continue }
                            if ($c -eq '\' -and $inStr) { $escaped = $true; continue }
                            if ($c -eq '"') { $inStr = -not $inStr; continue }
                            if ($inStr) { continue }
                            if ($c -eq '{') { $depth++ }
                            elseif ($c -eq '}') {
                                $depth--
                                if ($depth -eq 0) {
                                    return $trimmed.Substring(0, $i + 1)
                                }
                            }
                        }
                    }
                    # Not complete yet — keep reading
                    Start-Sleep -Milliseconds 50
                    continue
                }
            }

            # If we have Content-Length, check if we have the full payload
            if ($contentLength -ge 0 -and $headerEnd -ge 0) {
                $payloadSoFar = [System.Text.Encoding]::UTF8.GetBytes($data.Substring($headerEnd))
                if ($payloadSoFar.Length -ge $contentLength) {
                    return [System.Text.Encoding]::UTF8.GetString($payloadSoFar, 0, $contentLength)
                }
            }

            # Brief pause before reading more data
            Start-Sleep -Milliseconds 50
        }
    }
    finally {
        if ($null -ne $stream) { $stream.Dispose() }
        if ($null -ne $client) { $client.Dispose() }
    }
}

# ── Retry loop with backoff ──
$lastError = $null
for ($attempt = 1; $attempt -le $RetryCount; $attempt++) {
    try {
        $responseText = Send-RevitCommand -RequestJson $request -PortNum $Port -Timeout $TimeoutSec

        if ([string]::IsNullOrWhiteSpace($responseText)) {
            throw "Empty response from Revit."
        }

        # Output the JSON response
        Write-Output $responseText
        exit 0
    }
    catch {
        $lastError = $_
        if ($attempt -lt $RetryCount) {
            $delay = $attempt * 2
            Write-Error "Attempt $attempt/$RetryCount failed: $_  — retrying in ${delay}s..."
            Start-Sleep -Seconds $delay
        }
    }
}

Write-Error "revit-cmd error after $RetryCount attempts: $lastError"
exit 1
