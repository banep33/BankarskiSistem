[CmdletBinding()]
param(
    [string]$Server = "localhost",
    [int]$Port = 3306,
    [string]$User = "root",
    [string]$Database = "bankarskisistemdb",
    [string]$OutputDirectory,
    [int]$KeepLast = 14,
    [string]$MySqlDumpPath
)

$ErrorActionPreference = "Stop"

if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) "backups"
}

function Resolve-MySqlDump {
    param([string]$ExplicitPath)
    if ($ExplicitPath) { return $ExplicitPath }
    $onPath = Get-Command mysqldump -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }
    $default = Get-ChildItem "C:\Program Files\MySQL" -Recurse -Filter mysqldump.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.DirectoryName -like "*MySQL Server*" } |
        Select-Object -First 1
    if ($default) { return $default.FullName }
    throw "mysqldump.exe nije pronadjen. Prosledite putanju preko -MySqlDumpPath."
}

$mysqldump = Resolve-MySqlDump -ExplicitPath $MySqlDumpPath

if (-not $env:MYSQL_PWD) {
    $secure = Read-Host "Lozinka za MySQL korisnika '$User'" -AsSecureString
    $env:MYSQL_PWD = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
        [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$target = Join-Path $OutputDirectory "$Database`_$timestamp.sql"

$arguments = @(
    "--host=$Server", "--port=$Port", "--user=$User",
    "--single-transaction", "--routines", "--triggers", "--events",
    "--default-character-set=utf8mb4", "--result-file=$target", $Database
)

& $mysqldump @arguments
if ($LASTEXITCODE -ne 0) { throw "Backup nije uspeo (mysqldump exit code $LASTEXITCODE)." }

$size = [math]::Round((Get-Item $target).Length / 1KB, 1)
Write-Host "Backup sacuvan: $target ($size KB)" -ForegroundColor Green

Get-ChildItem $OutputDirectory -Filter "$Database`_*.sql" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -Skip $KeepLast |
    ForEach-Object {
        Remove-Item $_.FullName -Force
        Write-Host "Obrisan stari backup: $($_.Name)" -ForegroundColor DarkGray
    }
