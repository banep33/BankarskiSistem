[CmdletBinding()]
param(
    [string]$Server = "localhost",
    [int]$Port = 3306,
    [string]$AdminUser = "root",
    [string]$Database = "bankarskisistemdb",
    [string]$AppUser = "bankarski_app",
    [string]$AppHost = "localhost",
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

function Read-Secret {
    param([string]$Prompt)
    $secure = Read-Host $Prompt -AsSecureString
    return [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
}

if ($AppUser -notmatch '^[A-Za-z0-9_]{1,32}$') { throw "Ime korisnika sme sadrzati samo slova, cifre i donju crtu." }
if ($Database -notmatch '^[A-Za-z0-9_]{1,64}$') { throw "Ime baze sme sadrzati samo slova, cifre i donju crtu." }
if ($AppHost -notmatch '^[A-Za-z0-9_.%-]{1,60}$') { throw "Host nije ispravan." }

$mysql = Resolve-MySqlClient -ExplicitPath $MySqlPath

if (-not $env:MYSQL_PWD) {
    $env:MYSQL_PWD = Read-Secret "Lozinka za administratorskog MySQL korisnika '$AdminUser'"
}

$appPassword = Read-Secret "Nova lozinka za korisnika aplikacije '$AppUser' (najmanje 16 znakova)"
if ($appPassword.Length -lt 16) { throw "Lozinka mora imati najmanje 16 znakova." }
$escapedPassword = $appPassword.Replace("\", "\\").Replace("'", "''")

$sql = @"
CREATE USER IF NOT EXISTS '$AppUser'@'$AppHost' IDENTIFIED BY '$escapedPassword';
ALTER USER '$AppUser'@'$AppHost' IDENTIFIED BY '$escapedPassword';
REVOKE ALL PRIVILEGES, GRANT OPTION FROM '$AppUser'@'$AppHost';
GRANT SELECT, INSERT, UPDATE, DELETE ON ``$Database``.* TO '$AppUser'@'$AppHost';
FLUSH PRIVILEGES;
"@

$arguments = @("--host=$Server", "--port=$Port", "--user=$AdminUser", "--default-character-set=utf8mb4", "--batch", "--execute=$sql")
$output = & $mysql @arguments 2>&1
if ($LASTEXITCODE -ne 0) { throw ($output | Out-String) }

Write-Host "Korisnik '$AppUser'@'$AppHost' ima samo SELECT, INSERT, UPDATE i DELETE nad bazom '$Database'." -ForegroundColor Green
Write-Host "Migracije i dalje pokrecite administratorskim korisnikom (migrate.ps1)." -ForegroundColor Green
Write-Host "Connection string aplikacije postavite kroz user-secrets, nikada u repozitorijum:" -ForegroundColor Green
Write-Host "  dotnet user-secrets set ""ConnectionStrings:DefaultConnection"" ""Server=$Server;Port=$Port;Database=$Database;User=$AppUser;Password=<lozinka>;"" --project backend/src/BankarskiSistem.API"
