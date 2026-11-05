param(
    [Parameter(Mandatory)][ValidateSet('Full', 'Log', 'Check', 'Offsite', 'Retention')][string]$Job,
    [string]$Instance = '.\SQLEXPRESS',
    [string]$Database = 'FPMS',
    [string]$BackupRoot = 'E:\FPMS-backups',
    [string]$OffsiteDir = $env:FPMS_OFFSITE_DIR
)
$ErrorActionPreference = 'Stop'
$kinds = @{ Full = 1; Log = 2; Check = 3; Offsite = 5 }

function Invoke-Sql([string]$Query, [string]$Db = 'master') {
    $out = & sqlcmd -S $Instance -d $Db -E -b -h -1 -W -Q $Query 2>&1
    if ($LASTEXITCODE -ne 0) { throw ($out -join "`n") }
    return $out
}

function Save-Run([int]$Kind, [datetime]$Started, [bool]$Ok, [string]$File, $Size, [string]$Message) {
    try {
        $q = { param($s) $s.Replace("'", "''") }
        $msg = (& $q $Message); if ($msg.Length -gt 900) { $msg = $msg.Substring(0, 900) }
        $sizeSql = if ($null -eq $Size) { 'NULL' } else { [string][int64]$Size }
        Invoke-Sql "SET NOCOUNT ON; INSERT INTO FpmsBackupRuns (Kind, StartedAtUtc, FinishedAtUtc, Succeeded, FileName, SizeBytes, Message, Host) VALUES ($Kind, '$($Started.ToString('yyyy-MM-ddTHH:mm:ss'))', SYSUTCDATETIME(), $([int]$Ok), N'$(& $q $File)', $sizeSql, N'$msg', N'$($env:COMPUTERNAME)')" $Database | Out-Null
    } catch { Write-Warning "could not record the run: $_" }
}

function Get-Compression {
    $edition = ((Invoke-Sql "SET NOCOUNT ON; SELECT CAST(SERVERPROPERTY('EngineEdition') AS int)") -join '').Trim()
    if ($edition -eq '4') { return '' }
    return ', COMPRESSION'
}

function Invoke-Full {
    $dest = Join-Path $BackupRoot $stamp
    New-Item -ItemType Directory -Path $dest -Force | Out-Null
    $bak = Join-Path $dest "$Database.bak"
    Invoke-Sql "IF (SELECT recovery_model_desc FROM sys.databases WHERE name = N'$Database') <> 'FULL' ALTER DATABASE [$Database] SET RECOVERY FULL" | Out-Null
    Invoke-Sql "BACKUP DATABASE [$Database] TO DISK = N'$bak' WITH INIT, CHECKSUM$(Get-Compression), NAME = N'FPMS $stamp'" | Out-Null
    Invoke-Sql "RESTORE VERIFYONLY FROM DISK = N'$bak' WITH CHECKSUM" | Out-Null
    Get-FileHash $bak -Algorithm SHA256 | ForEach-Object { "$($_.Hash.ToLower())  $Database.bak" } | Set-Content (Join-Path $dest 'SHA256SUMS')
    Save-Run $kinds.Full $started $true "$stamp\$Database.bak" (Get-Item $bak).Length 'verified with CHECKSUM'
}

$started = (Get-Date).ToUniversalTime()
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
try {
    switch ($Job) {
        'Full' { Invoke-Full }
        'Log' {
            $chain = (Invoke-Sql "SET NOCOUNT ON; SELECT CASE WHEN d.recovery_model_desc = 'FULL' AND s.last_log_backup_lsn IS NOT NULL THEN 1 ELSE 0 END FROM sys.databases d JOIN sys.database_recovery_status s ON s.database_id = d.database_id WHERE d.name = N'$Database'") -join ''
            if ($chain.Trim() -ne '1') {
                Write-Output "no log chain yet for $Database; taking a full backup first"
                $Job = 'Full'
                Invoke-Full
                return
            }
            $dest = Join-Path $BackupRoot 'logs'
            New-Item -ItemType Directory -Path $dest -Force | Out-Null
            $trn = Join-Path $dest "$Database-$stamp.trn"
            Invoke-Sql "BACKUP LOG [$Database] TO DISK = N'$trn' WITH INIT, CHECKSUM$(Get-Compression)" | Out-Null
            Save-Run $kinds.Log $started $true "logs\$Database-$stamp.trn" (Get-Item $trn).Length ''
        }
        'Check' {
            $out = Invoke-Sql "DBCC CHECKDB ([$Database]) WITH NO_INFOMSGS, ALL_ERRORMSGS"
            if ($out) { throw ($out -join "`n") }
            Save-Run $kinds.Check $started $true '' $null 'CHECKDB found no errors'
        }
        'Offsite' {
            if (-not $OffsiteDir) { throw 'Set -OffsiteDir (or $env:FPMS_OFFSITE_DIR)' }
            if (-not $env:FPMS_OFFSITE_PASSPHRASE) { throw 'Set $env:FPMS_OFFSITE_PASSPHRASE' }
            $latest = Get-ChildItem $BackupRoot -Directory | Where-Object Name -Match '^\d{8}-\d{6}$' | Sort-Object Name | Select-Object -Last 1
            if (-not $latest) { throw 'no full backup found' }
            New-Item -ItemType Directory -Path $OffsiteDir -Force | Out-Null
            $out = Join-Path $OffsiteDir "FPMS-offsite-$($latest.Name).7z"
            $logs = Get-ChildItem (Join-Path $BackupRoot 'logs') -Filter *.trn -ErrorAction SilentlyContinue | Where-Object LastWriteTime -gt (Get-Item (Join-Path $latest.FullName "$Database.bak")).LastWriteTime
            & 7z a -t7z "-p$($env:FPMS_OFFSITE_PASSPHRASE)" -mhe=on $out $latest.FullName ($logs | ForEach-Object FullName) | Out-Null
            if ($LASTEXITCODE -ne 0) { throw '7-Zip failed' }
            & 7z t "-p$($env:FPMS_OFFSITE_PASSPHRASE)" $out | Out-Null
            if ($LASTEXITCODE -ne 0) { throw 'the off-site archive does not open' }
            Get-ChildItem $OffsiteDir -Filter 'FPMS-offsite-*.7z' | Sort-Object LastWriteTime -Descending | Select-Object -Skip 8 | Remove-Item -Force
            Save-Run $kinds.Offsite $started $true (Split-Path $out -Leaf) (Get-Item $out).Length 'encrypted, opened and tested after writing'
        }
        'Retention' {
            $folders = Get-ChildItem $BackupRoot -Directory | Where-Object Name -Match '^\d{8}-\d{6}$' | Sort-Object Name
            if ($folders.Count -lt 3) { Write-Output 'fewer than 3 full backups; nothing to delete'; return }
            $seen = @{}
            foreach ($f in $folders) {
                $d = [datetime]::ParseExact($f.Name.Substring(0, 8), 'yyyyMMdd', $null)
                $age = ((Get-Date) - $d).Days
                $month = $f.Name.Substring(0, 6)
                $first = -not $seen.ContainsKey($month); $seen[$month] = $true
                $keep = ($age -le 14) -or ($d.DayOfWeek -eq 'Sunday' -and $age -le 84) -or ($first -and $age -le 1826)
                if (-not $keep) { Remove-Item $f.FullName -Recurse -Force; Write-Output "removed $($f.Name)" }
            }
            $recent = ($folders | Where-Object { (Test-Path $_.FullName) -and ((Get-Date) - [datetime]::ParseExact($_.Name.Substring(0, 8), 'yyyyMMdd', $null)).Days -le 14 } | Select-Object -First 1).Name
            if (-not $recent) { return }
            Get-ChildItem (Join-Path $BackupRoot 'logs') -Filter *.trn -ErrorAction SilentlyContinue |
                Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-14) -and $_.BaseName -match '(\d{8}-\d{6})$' -and $Matches[1] -lt $recent } | Remove-Item -Force
        }
    }
} catch {
    if ($kinds.ContainsKey($Job)) { Save-Run $kinds[$Job] $started $false '' $null ([string]$_) }
    Write-Error $_
    exit 1
}
