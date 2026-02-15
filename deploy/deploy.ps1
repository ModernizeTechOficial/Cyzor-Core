param(
    [string]$TargetHost = $env:DEPLOY_HOST,
    [string]$User = $env:DEPLOY_USER,
    [string]$Key  = $env:SSH_KEY,
    [string]$RemotePath = $env:DEPLOY_PATH,
    [string]$ApiKey = $null
)

if (-not $TargetHost -or -not $User -or -not $RemotePath) {
    Write-Error "Missing DEPLOY_HOST, DEPLOY_USER or DEPLOY_PATH environment variables."
    exit 2
}

# Determine publish directory: prefer explicit env var, then common locations
$publishDir = $env:PUBLISH_DIR
if (-not $publishDir) {
    if (Test-Path 'C:\tmp\cyzor_publish') { $publishDir = 'C:\tmp\cyzor_publish' }
    elseif (Test-Path (Join-Path $PSScriptRoot '..\..\tmp\cyzor_publish')) { $publishDir = (Resolve-Path (Join-Path $PSScriptRoot '..\..\tmp\cyzor_publish')).Path }
    else { $publishDir = (Resolve-Path (Join-Path $PSScriptRoot '..\Cyzor.Provisioning\bin\Release\net8.0')).Path }
}

Write-Host "Deploying $publishDir -> $User@${TargetHost}:${RemotePath}"

# Build scp command safely (avoid PowerShell parsing of $Host:)
$remoteTarget = "$User@${TargetHost}:${RemotePath}"
if ($Key) { $scpCmd = @('scp','-i',$Key,'-r', (Join-Path $publishDir '*'), $remoteTarget) }
else { $scpCmd = @('scp','-r', (Join-Path $publishDir '*'), $remoteTarget) }

Write-Host "Running: $($scpCmd -join ' ')"

$ps = Start-Process -FilePath $scpCmd[0] -ArgumentList $scpCmd[1..($scpCmd.Length-1)] -NoNewWindow -Wait -PassThru
if ($ps.ExitCode -ne 0) { Write-Error "SCP failed with exit code $($ps.ExitCode)"; exit $ps.ExitCode }

# Restart remote service
if ($ApiKey) {
    Write-Host "Installing API key drop-in on remote host"
    $dropIn = "/etc/systemd/system/cyzor-provisioning.service.d"

    # Build env file content and send it as base64 to avoid shell quoting issues
    $envContent = "[Service]`nEnvironment=PROVISIONING_API_KEY=`"$ApiKey`"`n"
    $envB64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($envContent))

    $remoteCmd = "mkdir -p $dropIn && echo $envB64 | base64 -d > $dropIn/env.conf && systemctl daemon-reload"
    $sshCmd1 = @('ssh', "$User@${TargetHost}", $remoteCmd)
    Write-Host "Running SSH to install drop-in..."
    $psDrop = Start-Process -FilePath $sshCmd1[0] -ArgumentList $sshCmd1[1..($sshCmd1.Length-1)] -NoNewWindow -Wait -PassThru
    if ($psDrop.ExitCode -ne 0) { Write-Error "Failed to install API key drop-in (exit $($psDrop.ExitCode))"; exit $psDrop.ExitCode }
}

# Restart remote service and show status
$sshCmd = @('ssh', "$User@${TargetHost}", 'systemctl restart cyzor-provisioning && systemctl status cyzor-provisioning --no-pager -n 20')
Write-Host "Running: $($sshCmd -join ' ')"
$ps2 = Start-Process -FilePath $sshCmd[0] -ArgumentList $sshCmd[1..($sshCmd.Length-1)] -NoNewWindow -Wait -PassThru
if ($ps2.ExitCode -ne 0) { Write-Error "SSH command failed with exit code $($ps2.ExitCode)"; exit $ps2.ExitCode }

Write-Host "Deploy complete." 