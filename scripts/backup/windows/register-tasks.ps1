param(
    [string]$ScriptPath = 'C:\FPMS\scripts\backup\windows\fpms-jobs.ps1',
    [string]$BackupRoot = 'E:\FPMS-backups',
    [string]$OffsiteDir = 'F:\FPMS-offsite'
)
$ErrorActionPreference = 'Stop'
function Add-Job([string]$Name, [string]$Job, $Trigger) {
    $action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$ScriptPath`" -Job $Job -BackupRoot `"$BackupRoot`" -OffsiteDir `"$OffsiteDir`""
    $settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Hours 3) -MultipleInstances IgnoreNew
    Register-ScheduledTask -TaskName "FPMS $Name" -Action $action -Trigger $Trigger -Settings $settings -User 'SYSTEM' -RunLevel Highest -Force | Out-Null
    Write-Output "registered FPMS $Name"
}
Add-Job 'Full backup'      'Full'      (New-ScheduledTaskTrigger -Daily -At 20:00)
Add-Job 'Retention'        'Retention' (New-ScheduledTaskTrigger -Daily -At 20:45)
Add-Job 'Integrity check'  'Check'     (New-ScheduledTaskTrigger -Weekly -DaysOfWeek Sunday -At 21:00)
Add-Job 'Off-site copy'    'Offsite'   (New-ScheduledTaskTrigger -Weekly -DaysOfWeek Sunday -At 22:00)
$log = New-ScheduledTaskTrigger -Once -At 00:05 -RepetitionInterval (New-TimeSpan -Minutes 30)
Add-Job 'Log backup'       'Log'       $log
Write-Output 'Set FPMS_OFFSITE_PASSPHRASE as a machine environment variable (setx /M) so the off-site task can read it.'
