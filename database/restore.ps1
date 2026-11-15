[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BackupFile,
    [string]$Server = "localhost",
    [int]$Port = 3306,
    [string]$User = "root",
    [string]$TargetDatabase = "bankarskisistemdb_restore",
    [switch]$OverwriteExisting,
    [string]$MySqlPath
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $BackupFile)) { throw "Backup fajl '$BackupFile' ne postoji." }

function Resolve-MySqlClient {
    param([string]$ExplicitPath)
    if ($ExplicitPath) { return $ExplicitPath }
    $onPath = Get-Command mysql -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }
    $default = Get-ChildItem "C:\Program Files\MySQL" -Recurse -Filter mysql.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.DirectoryName -like "*MySQL Server*" } |
        Select-Object -First 1
    if ($default) { return $default.FullName }
    throw "mysql.exe nije pronadjen. Prosledite putanju preko -MySqlPath."
}

$mysql = Resolve-MySqlClient -ExplicitPath $MySqlPath

if (-not $env:MYSQL_PWD) {
    $secure = Read-Host "Lozinka za MySQL korisnika '$User'" -AsSecureString
    $env:MYSQL_PWD = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
        [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
}

$connectionArgs = @("--host=$Server", "--port=$Port", "--user=$User", "--default-character-set=utf8mb4")

$exists = & $mysql @connectionArgs --batch --skip-column-names --execute="SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name = '$TargetDatabase';"
if ([int]$exists -gt 0 -and -not $OverwriteExisting) {
    throw "Baza '$TargetDatabase' vec postoji. Koristite -OverwriteExisting ili drugo ime baze."
}

& $mysql @connectionArgs --execute="DROP DATABASE IF EXISTS ``$TargetDatabase``; CREATE DATABASE ``$TargetDatabase`` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"
if ($LASTEXITCODE -ne 0) { throw "Kreiranje baze nije uspelo." }

$scriptPath = (Resolve-Path $BackupFile).Path.Replace("\", "/")
& $mysql @connectionArgs $TargetDatabase --execute="source $scriptPath"
if ($LASTEXITCODE -ne 0) { throw "Vracanje backup-a nije uspelo." }

$checks = & $mysql @connectionArgs $TargetDatabase --batch --skip-column-names --execute="SELECT (SELECT MAX(Version) FROM schema_migrations), (SELECT COUNT(*) FROM v_account_reconciliation WHERE Difference <> 0), (SELECT COALESCE(SUM(ABS(Total)), 0) FROM v_ledger_trial_balance);"
$parts = ($checks -split "\s+")
Write-Host "Vraceno u bazu '$TargetDatabase'." -ForegroundColor Green
Write-Host "Verzija seme: $($parts[0]); racuni sa razlikom: $($parts[1]); neuravnotezenost knjige: $($parts[2])"
if ([int]$parts[1] -ne 0 -or [decimal]$parts[2] -ne 0) {
    Write-Warning "Provera integriteta nije prosla. Ne koristite ovu kopiju dok se razlika ne objasni."
}
