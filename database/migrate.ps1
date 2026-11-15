[CmdletBinding()]
param(
    [string]$Server = "localhost",
    [int]$Port = 3306,
    [string]$User = "root",
    [string]$Database = "bankarskisistemdb",
    [string]$MySqlPath
)

$ErrorActionPreference = "Stop"

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

function Invoke-MySql {
    param([string]$Sql, [string]$TargetDatabase)
    $arguments = $connectionArgs + @("--batch", "--skip-column-names")
    if ($TargetDatabase) { $arguments += $TargetDatabase }
    $arguments += @("--execute=$Sql")
    $output = & $mysql @arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw ($output | Out-String) }
    return $output
}

Invoke-MySql -Sql "CREATE DATABASE IF NOT EXISTS ``$Database`` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;" | Out-Null

$migrationsDirectory = Join-Path $PSScriptRoot "migrations"
$migrations = Get-ChildItem $migrationsDirectory -Filter "V*.sql" | Sort-Object Name

foreach ($migration in $migrations) {
    $version = $migration.Name.Split("__")[0]
    $tableExists = Invoke-MySql -TargetDatabase $Database -Sql "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'schema_migrations';"
    if ([int]$tableExists -gt 0) {
        $applied = Invoke-MySql -TargetDatabase $Database -Sql "SELECT COUNT(*) FROM schema_migrations WHERE Version = '$version';"
        if ([int]$applied -gt 0) {
            Write-Host "[preskoceno] $($migration.Name)" -ForegroundColor DarkGray
            continue
        }
    }

    Write-Host "[primena]    $($migration.Name)" -ForegroundColor Cyan
    $scriptPath = $migration.FullName.Replace("\", "/")
    Invoke-MySql -TargetDatabase $Database -Sql "source $scriptPath" | Out-Null
    Write-Host "[uspesno]    $($migration.Name)" -ForegroundColor Green
}

Write-Host "Baza '$Database' je azurna." -ForegroundColor Green
