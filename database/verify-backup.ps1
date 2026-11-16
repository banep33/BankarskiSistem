[CmdletBinding()]
param(
    [string]$BackupFile,
    [string]$Server = "localhost",
    [int]$Port = 3306,
    [string]$User = "root",
    [string]$SourceDatabase = "bankarskisistemdb",
    [string]$CheckDatabase = "bankarski_backup_provera",
    [string]$MySqlPath
)

$ErrorActionPreference = "Stop"
$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path

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

if ($CheckDatabase -notmatch '^[A-Za-z0-9_]{1,64}$' -or $CheckDatabase -eq $SourceDatabase) {
    throw "Baza za proveru mora imati drugo, ispravno ime."
}

if (-not $BackupFile) {
    $latest = Get-ChildItem (Join-Path $scriptDirectory "backups") -Filter "$SourceDatabase`_*.sql" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if (-not $latest) { throw "U folderu backups nema rezervne kopije baze '$SourceDatabase'." }
    $BackupFile = $latest.FullName
}

$mysql = Resolve-MySqlClient -ExplicitPath $MySqlPath

if (-not $env:MYSQL_PWD) {
    $secure = Read-Host "Lozinka za MySQL korisnika '$User'" -AsSecureString
    $env:MYSQL_PWD = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
        [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
}

$connectionArgs = @("--host=$Server", "--port=$Port", "--user=$User", "--default-character-set=utf8mb4", "--batch", "--skip-column-names")

function Invoke-Scalar {
    param([string]$Database, [string]$Sql)
    $output = & $mysql @connectionArgs $Database "--execute=$Sql" 2>&1
    if ($LASTEXITCODE -ne 0) { throw ($output | Out-String) }
    return "$output".Trim()
}

Write-Host "Provera kopije: $BackupFile" -ForegroundColor Cyan
$failures = @()

try {
    & (Join-Path $scriptDirectory "restore.ps1") -BackupFile $BackupFile -Server $Server -Port $Port -User $User -TargetDatabase $CheckDatabase -OverwriteExisting -MySqlPath $mysql | Out-Null

    $differences = [int](Invoke-Scalar $CheckDatabase "SELECT COUNT(*) FROM v_account_reconciliation WHERE Difference <> 0;")
    $imbalance = [decimal](Invoke-Scalar $CheckDatabase "SELECT COALESCE(SUM(ABS(Total)), 0) FROM v_ledger_trial_balance;")
    $restoredVersion = Invoke-Scalar $CheckDatabase "SELECT MAX(Version) FROM schema_migrations;"
    $expectedVersion = (Get-ChildItem (Join-Path $scriptDirectory "migrations") -Filter "V*.sql" | Sort-Object Name | Select-Object -Last 1).Name.Split("__")[0]

    if ($differences -ne 0) { $failures += "racuni sa razlikom izmedju stanja i knjige: $differences" }
    if ($imbalance -ne 0) { $failures += "glavna knjiga nije uravnotezena: $imbalance" }
    if ($restoredVersion -ne $expectedVersion) { $failures += "verzija seme u kopiji je $restoredVersion, a poslednja migracija je $expectedVersion" }

    Write-Host ""
    Write-Host ("{0,-22} {1,10} {2,10}" -f "Tabela", "Kopija", "Ziva baza")
    foreach ($table in @("users", "accounts", "transactions", "journal_entries", "postings", "auditlogs")) {
        $restored = [int](Invoke-Scalar $CheckDatabase "SELECT COUNT(*) FROM $table;")
        $live = [int](Invoke-Scalar $SourceDatabase "SELECT COUNT(*) FROM $table;")
        Write-Host ("{0,-22} {1,10} {2,10}" -f $table, $restored, $live)
        if ($restored -gt $live) { $failures += "tabela $table u kopiji ima vise redova od zive baze" }
    }
}
finally {
    & $mysql @connectionArgs "--execute=DROP DATABASE IF EXISTS ``$CheckDatabase``;" | Out-Null
}

Write-Host ""
if ($failures.Count -gt 0) {
    foreach ($failure in $failures) { Write-Host "NIJE PROSLO: $failure" -ForegroundColor Red }
    exit 1
}
Write-Host "Kopija je ispravna: vraca se bez greske, knjiga je uravnotezena, svi racuni su uskladjeni, sema je azurna." -ForegroundColor Green
Write-Host "Ziva baza ima iste ili novije podatke od kopije, sto je ocekivano." -ForegroundColor Green
