param(
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [ValidateRange(1, 600)][int]$Timeout = 120
)
$ErrorActionPreference = "Stop"
$library = Join-Path ([System.IO.Path]::GetFullPath($ProjectPath)) "Library"
if (!(Test-Path -LiteralPath $library -PathType Container)) { throw "Unity project Library folder not found: $library" }
$token = [guid]::NewGuid().ToString("N")
$signalPath = Join-Path (Join-Path $library "UnityAgentCoordination") ("notify-" + $token + ".json")
$watcher = [System.IO.FileSystemWatcher]::new($library, ("notify-" + $token + ".json"))
$watcher.IncludeSubdirectories = $true
$watcher.NotifyFilter = [System.IO.NotifyFilters]::FileName
$eventId = "uac-" + $token
try {
    $null = Register-ObjectEvent -InputObject $watcher -EventName Renamed -SourceIdentifier $eventId
    $watcher.EnableRaisingEvents = $true
    Write-Output (@{event="ready"; notification_token=$token} | ConvertTo-Json -Compress)
    $deadline = [DateTime]::UtcNow.AddSeconds($Timeout)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $signalPath -PathType Leaf) {
            $event = Get-Content -Raw -LiteralPath $signalPath | ConvertFrom-Json
            if ($event.token -eq $token -and $event.ticket_id) {
                Write-Output (@{event="turn"; ticket_id=$event.ticket_id} | ConvertTo-Json -Compress)
                exit 0
            }
        }
        $remaining = [Math]::Max(1, [int][Math]::Ceiling(($deadline - [DateTime]::UtcNow).TotalSeconds))
        $notification = Wait-Event -SourceIdentifier $eventId -Timeout $remaining
        if ($notification) { Remove-Event -EventIdentifier $notification.EventIdentifier }
    }
    Write-Output (@{event="timeout"} | ConvertTo-Json -Compress)
    exit 2
} finally {
    $watcher.Dispose()
    Unregister-Event -SourceIdentifier $eventId -ErrorAction SilentlyContinue
}
