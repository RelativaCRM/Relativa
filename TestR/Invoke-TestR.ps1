param(
    [switch]$CI,
    [ValidateSet('Unit', 'Integration', 'E2E', 'Load', 'All')]
    [string]$Suite = 'Unit'
)

if ($PSVersionTable.PSVersion.Major -lt 5 -or
    ($PSVersionTable.PSVersion.Major -eq 5 -and $PSVersionTable.PSVersion.Minor -lt 1)) {
    throw "TestR requires PowerShell 5.1 or later."
}

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Continue'

$Script:ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

function Find-ProjectRoot {
    param([string]$From)
    $dir = $From
    while ($dir) {
        if ((Test-Path (Join-Path $dir 'docker-compose.yaml')) -or (Test-Path (Join-Path $dir '.git'))) { return $dir }
        $up = Split-Path -Parent $dir
        if ($up -eq $dir) { break }
        $dir = $up
    }
    return $From
}

$ScriptRoot = Find-ProjectRoot $Script:ScriptDir
Set-Location $ScriptRoot

$Script:AppName         = 'TestR'
$Script:Ver             = 'v1.04a'
$Script:Project         = ''
$Script:UiWidth         = 66
$Script:JsonDepth       = 4
$Script:LineThr         = 75
$Script:BranchThr       = 75
$Script:NoBuild         = $false
$Script:Filter          = $null
$Script:Verbosity       = 1
$Script:MaxParallel     = 2
$Script:InfraUpServices = @()

$Script:Ansi = @{
    Black=30; DarkBlue=34; DarkGreen=32; DarkCyan=36; DarkRed=31; DarkMagenta=35; DarkYellow=33; Gray=37
    DarkGray=90; Blue=94; Green=92; Cyan=96; Red=91; Magenta=95; Yellow=93; White=97
}

$Script:DataDir     = $Script:ScriptDir
$Script:LogDir      = $Script:ScriptDir
$Script:ConfigFile  = Join-Path $Script:ScriptDir "$Script:AppName.cfg"
$Script:StateFilePath = $null
$Script:PrevResults   = @{}
$Script:ResultsDir  = ''
$Script:ReportDir   = ''
$Script:E2EDir      = ''
$Script:LogFile     = Join-Path $Script:LogDir "$Script:AppName.log"
$Script:ComposeFile = Join-Path $ScriptRoot 'docker-compose.yaml'
$Script:ClsFilter       = ''
$Script:Assemblies      = @()
$Script:AllAsmFilter    = ''
$Script:Modules         = @()
$Script:ComposeServices = @()
$Script:MaxModuleId     = 9
$Script:DockerFilter    = ''
$Script:E2EReportDir       = ''
$Script:E2EBlobReporter    = $false
$Script:E2EBaseUrl         = 'http://localhost:3000'
$Script:DotnetCmd          = ''
$Script:CoverageCollect    = ''
$Script:ReportGeneratorCmd = ''
$Script:DiscoverGlob       = ''
$Script:DiscoverIntRegex   = ''
$Script:E2EReportFolder    = ''
$Script:LoadProject        = ''
$Script:CtrlCPending       = $false
$Script:IsTTY = ([Console]::IsOutputRedirected -eq $false) -and ($host.Name -eq 'ConsoleHost')


$Script:Sess = [PSCustomObject]@{
    Start     = Get-Date
    Results   = [System.Collections.Generic.List[PSObject]]::new()
    FailedIds = [System.Collections.Generic.HashSet[int]]::new()
    AnyFailed = $false
    CovLine   = $null
    CovBranch = $null
}
$Script:BgJobs   = [System.Collections.Generic.List[PSObject]]::new()
$Script:JobQueue = [System.Collections.Generic.Queue[PSObject]]::new()

$Script:Esc = [char]27

function Get-StateFilePath {
    if ($Script:StateFilePath) { return $Script:StateFilePath }
    $name = if ($Script:Project) { $Script:Project } else { Split-Path -Leaf $Script:ScriptDir }
    $safe = $name -replace '[\\/:*?"<>|]', '_'
    $dir  = Join-Path $env:APPDATA 'TestR'
    if (-not (Test-Path $dir)) { $null = New-Item $dir -ItemType Directory -Force }
    $Script:StateFilePath = Join-Path $dir ".$safe.ses"
    return $Script:StateFilePath
}

function Get-RelativePath {
    param([string]$Base, [string]$Target)
    $baseUri   = [Uri]::new($Base.TrimEnd('\') + '\')
    $targetUri = [Uri]::new($Target)
    return $baseUri.MakeRelativeUri($targetUri).ToString() -replace '/', '\'
}

function Read-AssembliesData {
    $path = Join-Path $Script:DataDir 'Assemblies.dat'
    if (-not (Test-Path $path)) { return $Script:Assemblies }
    $result = [System.Collections.Generic.List[PSCustomObject]]::new()
    foreach ($line in (Get-Content $path -ErrorAction SilentlyContinue)) {
        if ($line -match '^\s*#' -or $line.Trim() -eq '') { continue }
        $p = $line.Split('|')
        if ($p.Count -lt 3) { continue }
        $result.Add([PSCustomObject]@{ Id=$p[0].Trim(); Label=$p[1].Trim(); Filter=$p[2].Trim() })
    }
    if ($result.Count -eq 0) { return $Script:Assemblies }
    return $result.ToArray()
}

function Read-ModulesData {
    $path = Join-Path $Script:DataDir 'Modules.dat'
    if (-not (Test-Path $path)) { return $Script:Modules }
    $result = [System.Collections.Generic.List[PSCustomObject]]::new()
    foreach ($line in (Get-Content $path -ErrorAction SilentlyContinue)) {
        if ($line -match '^\s*#' -or $line.Trim() -eq '') { continue }
        try {
            $p = $line.Split('|')
            if ($p.Count -lt 5) { continue }
            $rawIds = if ($p.Count -gt 5) { $p[5].Trim() } else { '' }
            $asmIds = if ($rawIds) { @($rawIds.Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ }) } else { @() }
            $result.Add([PSCustomObject]@{
                Id     = [int]$p[0].Trim()
                Name   = $p[1].Trim()
                Short  = $p[2].Trim()
                Path   = $p[3].Trim()
                Type   = $p[4].Trim()
                AsmIds = $asmIds
            })
        } catch { continue }
    }
    if ($result.Count -eq 0) { return $Script:Modules }
    return $result.ToArray()
}

function Read-ServiceMetadata {
    $path = Join-Path $Script:DataDir 'Services.dat'
    $meta = @{}
    if (-not (Test-Path $path)) { return $meta }
    foreach ($line in (Get-Content $path -ErrorAction SilentlyContinue)) {
        if ($line -match '^\s*#' -or $line.Trim() -eq '') { continue }
        $p = $line.Split('|')
        if ($p.Count -lt 1) { continue }
        $name    = $p[0].Trim()
        $display = if ($p.Count -gt 1 -and $p[1].Trim()) { $p[1].Trim() } else { $name.Substring(0,1).ToUpper() + $name.Substring(1) }
        $group   = if ($p.Count -gt 2 -and $p[2].Trim()) { $p[2].Trim() } else { 'app' }
        $url     = if ($p.Count -gt 3) { $p[3].Trim() } else { '' }
        $meta[$name] = [PSCustomObject]@{ Display=$display; Group=$group; Url=$url }
    }
    return $meta
}

function Read-ComposeServices {
    if (-not (Test-Path $Script:ComposeFile)) { return $Script:ComposeServices }
    $dotEnv = @{}
    $envFile = Join-Path $ScriptRoot '.env'
    if (Test-Path $envFile) {
        foreach ($l in (Get-Content $envFile -ErrorAction SilentlyContinue)) {
            if ($l -match '^\s*([^#=\s][^=]*)=(.*)$') { $dotEnv[$Matches[1].Trim()] = $Matches[2].Trim() }
        }
    }
    $meta       = Read-ServiceMetadata
    $result     = [System.Collections.Generic.List[PSCustomObject]]::new()
    $inServices = $false
    $inPorts    = $false
    $current    = $null
    foreach ($raw in (Get-Content $Script:ComposeFile -ErrorAction SilentlyContinue)) {
        $line = $raw.TrimEnd()
        if ($line -match '^services:\s*$')              { $inServices = $true; continue }
        if ($inServices -and $line -match '^[a-zA-Z]') { break }
        if (-not $inServices)                           { continue }
        if ($line -match '^  ([a-z][a-z0-9_-]*):\s*$') {
            if ($current) { $result.Add([PSCustomObject]$current) }
            $n = $Matches[1]
            if ($meta.ContainsKey($n)) {
                $display = $meta[$n].Display
                $group   = $meta[$n].Group
                $url     = $meta[$n].Url
            } else {
                $display = $n.Substring(0,1).ToUpper() + $n.Substring(1)
                $group   = 'app'
                $url     = ''
            }
            $current = @{ Name=$n; Display=$display; Port=''; Url=$url; Group=$group }
            $inPorts = $false; continue
        }
        if ($null -eq $current) { continue }
        if ($line -match '^\s{4}ports:\s*$') { $inPorts = $true; continue }
        if ($inPorts -and $line -match '^\s{6}-\s+"?([^"]+)"?') {
            $mapping = $Matches[1]
            foreach ($k in $dotEnv.Keys) { $mapping = $mapping -replace [regex]::Escape("`${$k}"), $dotEnv[$k] }
            $hp = ($mapping -split ':')[0].Trim()
            $current['Port'] = if ($current['Port']) { "$($current['Port'])/$hp" } else { $hp }
        } elseif ($inPorts -and $line -notmatch '^\s{6}-') { $inPorts = $false }
    }
    if ($current) { $result.Add([PSCustomObject]$current) }
    if ($result.Count -eq 0) { return $Script:ComposeServices }
    return $result.ToArray()
}

function Import-Config {
    if (-not (Test-Path $Script:ConfigFile)) { return }
    try {
        $cfg   = Get-Content $Script:ConfigFile -Raw | ConvertFrom-Json
        $props = $cfg.PSObject.Properties
        if ($props['Project'])         { $Script:Project         = "$($cfg.Project)" }
        if ($props['UiWidth'])         { $Script:UiWidth         = [int]$cfg.UiWidth }
        if ($props['LineThr'])         { $Script:LineThr         = [int]$cfg.LineThr }
        if ($props['BranchThr'])       { $Script:BranchThr       = [int]$cfg.BranchThr }
        if ($props['Verbosity'])       { $Script:Verbosity       = [int]$cfg.Verbosity }
        if ($props['MaxParallel'])     { $Script:MaxParallel     = [int]$cfg.MaxParallel }
        if ($props['NoBuild'])         { $Script:NoBuild         = [bool]$cfg.NoBuild }
        if ($props['Filter'])          { $Script:Filter          = if ($cfg.Filter) { "$($cfg.Filter)" } else { $null } }
        if ($props['ClsFilter'])       { $Script:ClsFilter       = "$($cfg.ClsFilter)" }
        if ($props['ResultsDir'])      { $Script:ResultsDir      = Join-Path $ScriptRoot "$($cfg.ResultsDir)" }
        if ($props['ReportDir'])       { $Script:ReportDir       = Join-Path $ScriptRoot "$($cfg.ReportDir)" }
        if ($props['E2EDir'])           { $Script:E2EDir           = Join-Path $ScriptRoot "$($cfg.E2EDir)" }
        if ($props['E2EBlobReporter'])    { $Script:E2EBlobReporter    = [bool]$cfg.E2EBlobReporter }
        if ($props['InfraUpServices'])    { $Script:InfraUpServices    = @($cfg.InfraUpServices) }
        if ($props['DockerFilter'])       { $Script:DockerFilter       = "$($cfg.DockerFilter)" }
        if ($props['DotnetCmd'])          { $Script:DotnetCmd          = "$($cfg.DotnetCmd)" }          else { $Script:DotnetCmd          = 'dotnet' }
        if ($props['CoverageCollect'])    { $Script:CoverageCollect    = "$($cfg.CoverageCollect)" }    else { $Script:CoverageCollect    = 'XPlat Code Coverage' }
        if ($props['ReportGeneratorCmd']) { $Script:ReportGeneratorCmd = "$($cfg.ReportGeneratorCmd)" } else { $Script:ReportGeneratorCmd = 'reportgenerator' }
        if ($props['DiscoverGlob'])       { $Script:DiscoverGlob       = "$($cfg.DiscoverGlob)" }       else { $Script:DiscoverGlob       = '*.csproj' }
        if ($props['DiscoverIntRegex'])   { $Script:DiscoverIntRegex   = "$($cfg.DiscoverIntRegex)" }   else { $Script:DiscoverIntRegex   = '\.Integration\.' }
        if ($props['E2EReportFolder'])   { $Script:E2EReportFolder   = "$($cfg.E2EReportFolder)" }     else { $Script:E2EReportFolder   = 'playwright-report' }
        if ($props['LoadProject'])        { $Script:LoadProject        = "$($cfg.LoadProject)" }        else { $Script:LoadProject        = 'LoadTests/Relativa.LoadTests/Relativa.LoadTests.csproj' }
        if ($props['E2EBaseUrl'])        { $Script:E2EBaseUrl        = "$($cfg.E2EBaseUrl)" }          else { $Script:E2EBaseUrl        = 'http://localhost:3000' }
    } catch {}
}

function Export-Config {
    try {
        [ordered]@{
            Project         = $Script:Project
            UiWidth         = $Script:UiWidth
            LineThr         = $Script:LineThr
            BranchThr       = $Script:BranchThr
            Verbosity       = $Script:Verbosity
            MaxParallel     = $Script:MaxParallel
            NoBuild         = $Script:NoBuild
            Filter          = $Script:Filter
            ClsFilter       = $Script:ClsFilter
            ResultsDir      = if ($Script:ResultsDir) { Get-RelativePath $ScriptRoot $Script:ResultsDir } else { '' }
            ReportDir       = if ($Script:ReportDir)  { Get-RelativePath $ScriptRoot $Script:ReportDir  } else { '' }
            E2EDir          = if ($Script:E2EDir)      { Get-RelativePath $ScriptRoot $Script:E2EDir     } else { '' }
            E2EBlobReporter  = $Script:E2EBlobReporter
            DotnetCmd        = $Script:DotnetCmd
            CoverageCollect  = $Script:CoverageCollect
            ReportGeneratorCmd = $Script:ReportGeneratorCmd
            DiscoverGlob     = $Script:DiscoverGlob
            DiscoverIntRegex = $Script:DiscoverIntRegex
            E2EReportFolder  = $Script:E2EReportFolder
            E2EBaseUrl       = $Script:E2EBaseUrl
            LoadProject      = $Script:LoadProject
            InfraUpServices  = $Script:InfraUpServices
            DockerFilter     = $Script:DockerFilter
        } | ConvertTo-Json | Set-Content $Script:ConfigFile -Encoding UTF8
    } catch {}
}

function Add-Log {
    param([string]$Line)
    "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  $Line" | Out-File $Script:LogFile -Append -Encoding UTF8
}

function Format-Elapsed {
    param([TimeSpan]$Span)
    if ($Span.TotalSeconds -lt 60) { return '{0:N1}s' -f $Span.TotalSeconds }
    return '{0}m{1:N1}s' -f [int]$Span.Minutes, $Span.Seconds
}

function Get-VerbosityLabel {
    switch ($Script:Verbosity) {
        0 { return 'quiet' }
        1 { return 'minimal' }
        2 { return 'normal' }
        3 { return 'detailed' }
    }
    return 'minimal'
}

function Get-VerbosityArgs {
    $lv = Get-VerbosityLabel
    return @('--verbosity', $lv, '--logger', "console;verbosity=$lv")
}

function Write-C {
    param([string]$Text, [ConsoleColor]$Color = 'White', [switch]$NoNewLine)
    if (-not $Script:IsTTY) {
        if ($NoNewLine) { [Console]::Write($Text) } else { [Console]::WriteLine($Text) }
        return
    }
    $c = $Script:Ansi[$Color.ToString()]
    if ($NoNewLine) {
        [Console]::Write("$Script:Esc[${c}m${Text}$Script:Esc[0m")
    } else {
        [Console]::Write("$Script:Esc[${c}m${Text}$Script:Esc[0m$Script:Esc[K`n")
    }
}

function Write-Ln {
    [Console]::Write("$Script:Esc[K`n")
}

function Write-Header {
    param([string]$Subtitle = '')
    $bar  = '=' * $Script:UiWidth
    $left = "$Script:AppName  $Script:Ver"
    if ($Subtitle) {
        $inner = "$left  |  $Subtitle"
    } else {
        $pad   = [math]::Max(1, $Script:UiWidth - $left.Length - $Script:Project.Length)
        $inner = "$left$(' ' * $pad)$Script:Project"
    }
    Write-C "  $bar" Cyan
    Write-C "  $inner" Cyan
    Write-C "  $bar" Cyan
}

function Write-Sep {
    param([string]$Label = '', [ConsoleColor]$Color = 'DarkGray')
    if ($Label) {
        $fill = [math]::Max(2, $Script:UiWidth - $Label.Length - 4)
        Write-C "  -- $Label $('-' * $fill)" $Color
    } else {
        Write-C "  $('-' * $Script:UiWidth)" $Color
    }
}

function Enter-Render {
    if (-not $Script:IsTTY) { return }
    [Console]::CursorVisible = $false
    [Console]::Write("$Script:Esc[H")
}

function Exit-Render {
    if (-not $Script:IsTTY) { return }
    [Console]::Write("$Script:Esc[J")
    [Console]::CursorVisible = $true
}

function Read-Input {
    param([string]$Prompt = "$Script:AppName>")
    Write-Host "  $Prompt " -ForegroundColor DarkGray -NoNewline
    return [Console]::ReadLine()
}

function Wait-AnyKey {
    Write-C "  press any key to continue" DarkGray
    [Console]::TreatControlCAsInput = $true
    try {
        while ($true) {
            if ([Console]::KeyAvailable) {
                $key = [Console]::ReadKey($true)
                $isCtrlC = ($key.Modifiers -band [ConsoleModifiers]::Control) -and ($key.Key -eq [ConsoleKey]::C)
                if ($isCtrlC) { $Script:CtrlCPending = $true }
                return
            }
            Start-Sleep -Milliseconds 50
        }
    } finally {
        [Console]::TreatControlCAsInput = $false
    }
}

function Read-MenuInput {
    param([string]$Prompt = $Script:AppName)
    $hasBg    = @($Script:BgJobs | Where-Object { -not $_.Collected }).Count -gt 0
    $interval = if ($hasBg) { 2 } else { 30 }
    $deadline = (Get-Date).AddSeconds($interval)
    $chars = ''
    if ($Script:CtrlCPending) {
        Write-Ln
        foreach ($entry in @($Script:BgJobs | Where-Object { -not $_.Collected })) {
            try { Stop-Job -Job $entry.Job -ErrorAction SilentlyContinue } catch {}
            try { Remove-Job -Job $entry.Job -Force -ErrorAction SilentlyContinue } catch {}
        }
        exit 0
    }
    Write-Host "  $Prompt> " -ForegroundColor DarkGray -NoNewline
    [Console]::TreatControlCAsInput = $true
    try {
        while ($true) {
            if ([Console]::KeyAvailable) {
                $key = [Console]::ReadKey($true)
                $isCtrlC = ($key.Modifiers -band [ConsoleModifiers]::Control) -and ($key.Key -eq [ConsoleKey]::C)
                if ($isCtrlC) {
                    if ($Script:CtrlCPending) {
                        Write-Ln
                        foreach ($entry in @($Script:BgJobs | Where-Object { -not $_.Collected })) {
                            try { Stop-Job -Job $entry.Job -ErrorAction SilentlyContinue } catch {}
                            try { Remove-Job -Job $entry.Job -Force -ErrorAction SilentlyContinue } catch {}
                        }
                        exit 0
                    }
                    $Script:CtrlCPending = $true
                    Write-Ln
                    Write-Host '  CTRL+C again to exit, any key to cancel' -ForegroundColor Yellow
                    Write-Host "  $Prompt> " -ForegroundColor DarkGray -NoNewline
                    continue
                }
                $Script:CtrlCPending = $false
                if ($key.Key -eq [ConsoleKey]::Enter) {
                    Write-Ln
                    return $chars
                }
                if ($key.Key -eq [ConsoleKey]::Backspace) {
                    if ($chars.Length -gt 0) {
                        $chars = $chars.Substring(0, $chars.Length - 1)
                        [Console]::Write("`b `b")
                    }
                    continue
                }
                if ($key.Key -eq [ConsoleKey]::Escape) {
                    Write-Ln
                    return ''
                }
                $c = $key.KeyChar
                if ($c -ne [char]0 -and ([char]::IsLetterOrDigit($c) -or $c -eq '&' -or $c -eq '?' -or $c -eq ' ' -or $c -eq '+' -or $c -eq '*')) {
                    $chars += $c
                    Write-Host $c -NoNewline -ForegroundColor White
                }
            }
            if ((Get-Date) -gt $deadline -and $hasBg -and $chars -eq '') {
                $Script:CtrlCPending = $false
                Write-Ln
                return $null
            }
            Start-Sleep -Milliseconds 50
        }
    } finally {
        [Console]::TreatControlCAsInput = $false
    }
}

function Get-LastResults {
    $seen = [System.Collections.Generic.HashSet[string]]::new()
    $last = [System.Collections.Generic.List[PSObject]]::new()
    for ($i = $Script:Sess.Results.Count - 1; $i -ge 0; $i--) {
        $r = $Script:Sess.Results[$i]
        if ($seen.Add($r.Short)) { $last.Insert(0, $r) }
    }
    return $last
}

function Update-AnyFailed {
    $Script:Sess.AnyFailed = @(Get-LastResults | Where-Object { -not $_.Ok }).Count -gt 0
}

function Get-SessionBanner {
    if ($Script:Sess.Results.Count -eq 0) { return $null }
    $last = Get-LastResults
    $p   = @($last | Where-Object { $_.Ok }).Count
    $f   = @($last | Where-Object { -not $_.Ok }).Count
    $dur = Format-Elapsed ((Get-Date) - $Script:Sess.Start)
    $cov = if ($null -ne $Script:Sess.CovLine) { "  |  cov $($Script:Sess.CovLine)% / $($Script:Sess.CovBranch)%" } else { '' }
    if ($f -gt 0) { return "$p passed  |  $f FAILED  |  $dur$cov" }
    return "all $p passed  |  $dur$cov"
}

function Save-State {
    $path = Get-StateFilePath
    if (-not $path) { return }
    $merged = foreach ($m in $Script:Modules) {
        $cur = @($Script:Sess.Results | Where-Object { $_.Short -eq $m.Short }) | Select-Object -Last 1
        if ($null -ne $cur) {
            @{ Name=$cur.Name; Short=$cur.Short; Id=$cur.Id; Ok=$cur.Ok; Dur=$cur.Dur; Type=$cur.Type }
        } elseif ($Script:PrevResults.ContainsKey($m.Short)) {
            $p = $Script:PrevResults[$m.Short]
            @{ Name=$m.Name; Short=$m.Short; Id=$m.Id; Ok=$p.Ok; Dur=$p.Dur; Type=$m.Type }
        }
    }
    $json = @{
        Timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm'
        Results   = @($merged | Where-Object { $null -ne $_ })
        Failed    = @($Script:Sess.FailedIds)
        AnyFailed = $Script:Sess.AnyFailed
        CovLine   = $Script:Sess.CovLine
        CovBranch = $Script:Sess.CovBranch
    } | ConvertTo-Json -Depth $Script:JsonDepth
    try {
        if ([System.IO.File]::Exists($path)) {
            $fi = [System.IO.FileInfo]::new($path)
            $fi.Attributes = $fi.Attributes -band (-bnot [System.IO.FileAttributes]::Hidden)
        }
        [System.IO.File]::WriteAllText($path, $json, [System.Text.Encoding]::UTF8)
    } catch { return }
    try {
        $fi = [System.IO.FileInfo]::new($path)
        $fi.Attributes = $fi.Attributes -bor [System.IO.FileAttributes]::Hidden
    } catch {}
}

function Get-SavedState {
    $path = Get-StateFilePath
    if (-not $path -or -not [System.IO.File]::Exists($path)) { return $null }
    try { return [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8) | ConvertFrom-Json } catch { return $null }
}

function Restore-State {
    $saved = Get-SavedState
    if ($null -eq $saved) { return }
    $props = $saved.PSObject.Properties
    if ($props['AnyFailed'] -and $null -ne $saved.AnyFailed) { $Script:Sess.AnyFailed = [bool]$saved.AnyFailed }
    if ($props['CovLine']   -and $null -ne $saved.CovLine)   { $Script:Sess.CovLine   = [double]$saved.CovLine }
    if ($props['CovBranch'] -and $null -ne $saved.CovBranch) { $Script:Sess.CovBranch = [double]$saved.CovBranch }
    if ($props['Failed']    -and $null -ne $saved.Failed) {
        foreach ($id in $saved.Failed) { $null = $Script:Sess.FailedIds.Add([int]$id) }
    }
    if ($props['Results'] -and $null -ne $saved.Results) {
        $items = if ($saved.Results -is [array]) { $saved.Results } else { @($saved.Results) }
        foreach ($r in $items) {
            try {
                $dur = if ($r.PSObject.Properties['Dur'] -and $r.Dur) { "$($r.Dur)" } else { '' }
                $Script:PrevResults["$($r.Short)"] = [PSCustomObject]@{ Ok=[bool]$r.Ok; Dur=$dur }
            } catch { continue }
        }
    }
}

function Get-ModuleStats {
    param([PSCustomObject]$Module)
    $trx = Join-Path $Script:ResultsDir "$($Module.Short)\results.trx"
    if (-not (Test-Path $trx)) { return $null }
    try {
        [xml]$xml = Get-Content $trx -Raw -ErrorAction Stop
        $ns = New-Object System.Xml.XmlNamespaceManager $xml.NameTable
        $ns.AddNamespace('t', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
        $ctr = $xml.SelectSingleNode('//t:Counters', $ns)
        if ($null -eq $ctr) { return $null }
        return [PSCustomObject]@{ Total=[int]$ctr.total; Passed=[int]$ctr.passed; Failed=[int]$ctr.failed }
    } catch { return $null }
}

function Get-CoverageStats {
    param([string]$Dir)
    $xmlPath = Join-Path $Dir 'Cobertura.xml'
    if (-not (Test-Path $xmlPath)) { return $null }
    try {
        [xml]$doc = Get-Content $xmlPath -Raw -ErrorAction Stop
        $root = $doc.DocumentElement
        $line   = [double]$root.'line-rate'   * 100
        $branch = [double]$root.'branch-rate' * 100
        return @{ Line = [int][Math]::Round($line); Branch = [int][Math]::Round($branch) }
    } catch { return $null }
}

function Get-E2EReportMeta {
    param([string]$Dir)
    $path = Join-Path $Dir 'testr-meta.json'
    if (-not (Test-Path $path)) { return $null }
    try {
        $obj = Get-Content $path -Raw | ConvertFrom-Json
        return @{ Ok = [bool]$obj.Ok }
    } catch { return $null }
}

function Save-E2EReport {
    param([bool]$Ok)
    $src = Join-Path $Script:E2EDir $Script:E2EReportFolder
    if (-not (Test-Path $src)) { return }
    if (-not (Test-Path $Script:E2EReportDir)) { $null = New-Item $Script:E2EReportDir -ItemType Directory -Force }
    $dest = Join-Path $Script:E2EReportDir (Get-Date).ToString('yyyyMMdd_HHmmss')
    Copy-Item $src $dest -Recurse -Force
    @{ Ok = $Ok } | ConvertTo-Json -Depth 1 | Set-Content (Join-Path $dest 'testr-meta.json') -Encoding UTF8
}

function Test-Docker {
    try { $null = & docker info 2>&1; return ($LASTEXITCODE -eq 0) } catch { return $false }
}

function Test-Playwright {
    return (Test-Path (Join-Path $Script:E2EDir 'node_modules')) -and
           (Test-Path (Join-Path $Script:E2EDir 'node_modules\.bin\playwright'))
}

function Measure-E2EBaseUrl {
    $url = $Script:E2EBaseUrl
    if (-not $url) { return }
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $resp = Invoke-WebRequest -Uri $url -TimeoutSec 10 -UseBasicParsing -ErrorAction Stop
        $sw.Stop()
        $ms = [int]$sw.ElapsedMilliseconds
        Write-C ("  [PROBE] {0}  HTTP {1}  in {2} ms" -f $url, [int]$resp.StatusCode, $ms) DarkGray
        Add-Log "E2E-PROBE $url status=$([int]$resp.StatusCode) ms=$ms"
        if ($ms -gt 3000) {
            Write-C "  [WARN]  baseURL slow to respond ($ms ms) - first navigation may exceed test navigationTimeout." Yellow
        }
    } catch {
        $sw.Stop()
        Write-C ("  [PROBE] {0}  UNREACHABLE after {1} ms - is the app/container up on this port?" -f $url, [int]$sw.ElapsedMilliseconds) Yellow
        Add-Log "E2E-PROBE $url UNREACHABLE ms=$([int]$sw.ElapsedMilliseconds) err=$($_.Exception.Message)"
    }
}

function Get-ContainerStatus {
    $map = @{}
    if (-not (Test-Docker)) { return $map }
    try {
        $filterArgs = if ($Script:DockerFilter) { @('--filter', "name=$Script:DockerFilter") } else { @() }
        $lines = & docker ps -a @filterArgs --format '{{.Names}}|{{.State}}' 2>&1
        foreach ($line in $lines) {
            if ($line -match '^([^|]+)\|(.+)$') {
                $raw = $Matches[1].Trim()
                $key = if ($Script:DockerFilter -and $raw.StartsWith($Script:DockerFilter)) { $raw.Substring($Script:DockerFilter.Length) } else { $raw }
                $map[$key] = $Matches[2].Trim()
            }
        }
    } catch {}
    return $map
}

function Invoke-Process {
    param(
        [string]$Command,
        [string[]]$Arguments = @(),
        [string]$WorkDir = ''
    )
    $interrupted = $false
    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.UseShellExecute = $false
    if ($WorkDir -and (Test-Path $WorkDir)) { $psi.WorkingDirectory = $WorkDir }
    $info = try { Get-Command $Command -ErrorAction Stop } catch { $null }
    if ($null -ne $info -and $info.Source -match '\.exe$') {
        $psi.FileName = $info.Source
        try {
            [void]$psi.ArgumentList.Add('_')
            $psi.ArgumentList.Clear()
            foreach ($a in $Arguments) { [void]$psi.ArgumentList.Add($a) }
        } catch {
            $psi.Arguments = ($Arguments | ForEach-Object {
                if ($_ -match '[\s"]') { '"' + ($_ -replace '"', '""') + '"' } else { $_ }
            }) -join ' '
        }
    } else {
        $psi.FileName = $env:ComSpec
        $innerArgs = (@($Command) + $Arguments) | ForEach-Object {
            if ($_ -match '[\s"]') { '"' + ($_ -replace '"', '""') + '"' } else { $_ }
        }
        $psi.Arguments = '/c "' + ($innerArgs -join ' ') + '"'
    }
    $proc = [System.Diagnostics.Process]::new()
    $proc.StartInfo = $psi
    $canPollKeys = $false
    try { $null = [Console]::KeyAvailable; $canPollKeys = $true } catch { $canPollKeys = $false }
    if ($canPollKeys) { try { [Console]::TreatControlCAsInput = $true } catch { $canPollKeys = $false } }
    try {
        [void]$proc.Start()
        while (-not $proc.HasExited) {
            if ($canPollKeys -and [Console]::KeyAvailable) {
                $key = [Console]::ReadKey($true)
                $isCtrlC = ($key.Modifiers -band [ConsoleModifiers]::Control) -and ($key.Key -eq [ConsoleKey]::C)
                if ($key.Key -eq [ConsoleKey]::Escape -or $isCtrlC) {
                    $interrupted = $true
                    try { & taskkill /F /T /PID $proc.Id 2>&1 | Out-Null } catch {}
                    [void]$proc.WaitForExit(3000)
                    break
                }
            }
            if ($canPollKeys) { Start-Sleep -Milliseconds 100 } else { [void]$proc.WaitForExit(250) }
        }
        $exitCode = if ($interrupted) { 1 } else { $proc.ExitCode }
    } finally {
        if ($canPollKeys) { try { [Console]::TreatControlCAsInput = $false } catch {} }
        try { $proc.Dispose() } catch {}
    }
    return [PSCustomObject]@{ ExitCode = $exitCode; Interrupted = $interrupted }
}

function Start-BackgroundJob {
    param([PSCustomObject]$Module)
    $abs     = Join-Path $ScriptRoot $Module.Path
    $logPath = Join-Path $Script:ResultsDir "$($Module.Short)-bg.log"
    $argList = [System.Collections.Generic.List[string]]::new()
    $argList.Add('test')
    $argList.Add($abs)
    $argList.Add('--collect'); $argList.Add($Script:CoverageCollect)
    $argList.Add("--results-directory=$Script:ResultsDir\$($Module.Short)")
    foreach ($va in (Get-VerbosityArgs)) { $argList.Add($va) }
    $argList.Add('--logger'); $argList.Add('trx;logfilename=results.trx')
    if ($Script:NoBuild) { $argList.Add('--no-build') }
    if ($Script:Filter)  { $argList.Add("--filter=$Script:Filter") }
    $argArray = $argList.ToArray()
    $cmd = $Script:DotnetCmd
    $job = Start-Job -Name $Module.Short -ScriptBlock {
        param([string[]]$ta, [string]$lp, [string]$cmd)
        $out = & $cmd @ta 2>&1
        $out | Out-File $lp -Encoding UTF8
        $LASTEXITCODE | Out-File "$lp.exit" -Encoding UTF8
    } -ArgumentList $argArray, $logPath, $cmd
    $Script:BgJobs.Add([PSCustomObject]@{
        Module    = $Module
        Job       = $job
        Started   = Get-Date
        LogPath   = $logPath
        Collected = $false
        Ok        = $null
        Dur       = $null
        Stopped   = $false
    })
    Add-Log "BG-START $($Module.Short) job=$($job.Id)"
    return $job.Id
}

function Sync-BackgroundJobs {
    $pending = @($Script:BgJobs | Where-Object { -not $_.Collected })
    foreach ($entry in $pending) {
        if ($entry.Job.State -notin @('Running', 'NotStarted')) {
            $exitCode = 1
            if (Test-Path "$($entry.LogPath).exit") {
                try { $exitCode = [int](Get-Content "$($entry.LogPath).exit" -Raw).Trim() } catch {}
            }
            $ok  = ($entry.Job.State -eq 'Completed') -and ($exitCode -eq 0)
            $dur = Format-Elapsed ((Get-Date) - $entry.Started)
            $entry.Ok        = $ok
            $entry.Dur       = $dur
            $entry.Collected = $true
            $Script:Sess.Results.Add([PSCustomObject]@{
                Name  = $entry.Module.Name
                Short = $entry.Module.Short
                Id    = $entry.Module.Id
                Ok    = $ok
                Dur   = $dur
                Type  = $entry.Module.Type
            })
            if ($ok) {
                $null = $Script:Sess.FailedIds.Remove($entry.Module.Id)
                Update-AnyFailed
                Add-Log "BG-PASS $($entry.Module.Short) $dur"
            } else {
                $Script:Sess.AnyFailed = $true
                $null = $Script:Sess.FailedIds.Add($entry.Module.Id)
                Add-Log "BG-FAIL $($entry.Module.Short) $dur"
                try { [Console]::Beep() } catch {}
            }
            if ($entry.Module.Short -eq 'e2e') { Save-E2EReport -Ok $ok }
            try { Remove-Job -Job $entry.Job -Force } catch {}
        }
    }
    while ($Script:JobQueue.Count -gt 0) {
        $running = @($Script:BgJobs | Where-Object { -not $_.Collected }).Count
        if ($running -ge $Script:MaxParallel) { break }
        $next = $Script:JobQueue.Dequeue()
        Start-BackgroundJob $next.Module | Out-Null
        Add-Log "BG-DEQUEUE $($next.Module.Short)"
    }
}

function Stop-BackgroundJob {
    param([PSCustomObject]$Entry)
    if ($Entry.Collected) { return }
    try { Stop-Job  -Job $Entry.Job -ErrorAction SilentlyContinue } catch {}
    try { Remove-Job -Job $Entry.Job -Force -ErrorAction SilentlyContinue } catch {}
    $Entry.Collected = $true
    $Entry.Stopped   = $true
    $Entry.Ok        = $false
    $Entry.Dur       = Format-Elapsed ((Get-Date) - $Entry.Started)
    Add-Log "BG-STOP $($Entry.Module.Short) $($Entry.Dur)"
}

function Get-JobBadge {
    param([PSCustomObject]$Entry)
    if ($Entry.Stopped) { return @{ Tag = '[STOP]'; Col = 'Yellow' } }
    if ($Entry.Ok)      { return @{ Tag = '[ OK ]'; Col = 'Green' } }
    return @{ Tag = '[FAIL]'; Col = 'Red' }
}

function Invoke-Suite {
    param([PSCustomObject]$Module, [switch]$Background)
    $abs = Join-Path $ScriptRoot $Module.Path
    if (-not (Test-Path $abs)) {
        Write-C "  [WARN]  Not found: $($Module.Path)" Yellow
        return
    }
    if ($Script:NoBuild -and $Module.Type -eq 'Integration') {
        Write-C "  [WARN]  --no-build ON for Integration - schema changes will not compile." Yellow
    }
    if ($Background) {
        $running = @($Script:BgJobs | Where-Object { -not $_.Collected }).Count
        if ($running -ge $Script:MaxParallel) {
            $Script:JobQueue.Enqueue([PSCustomObject]@{ Module=$Module })
            Write-C "  [WAIT]  $($Module.Name)  queued (parallel cap: $Script:MaxParallel)" Yellow
            Add-Log "BG-QUEUE $($Module.Short)"
            return
        }
        $jobId = Start-BackgroundJob $Module
        Write-C "  [ BG ]  $($Module.Name)  started as job $jobId" Cyan
        return
    }
    $testArgs = [System.Collections.Generic.List[string]]::new()
    $testArgs.Add('test')
    $testArgs.Add($abs)
    $testArgs.Add('--collect'); $testArgs.Add($Script:CoverageCollect)
    $testArgs.Add("--results-directory=$Script:ResultsDir\$($Module.Short)")
    foreach ($va in (Get-VerbosityArgs)) { $testArgs.Add($va) }
    $testArgs.Add('--logger'); $testArgs.Add('trx;logfilename=results.trx')
    if ($Script:NoBuild) { $testArgs.Add('--no-build') }
    if ($Script:Filter)  { $testArgs.Add("--filter=$Script:Filter") }
    Write-Ln
    Write-C "  [ >> ]  $($Module.Name)  " Cyan -NoNewLine
    Write-C "[$($Module.Type)]" DarkGray
    Write-C "          ESC or CTRL+C to interrupt" DarkGray
    $start = Get-Date
    Add-Log "START $($Module.Short)"
    $r   = Invoke-Process -Command $Script:DotnetCmd -Arguments $testArgs.ToArray()
    $ok  = $r.ExitCode -eq 0
    if ($r.Interrupted) {
        Write-C "  [STOP]  $($Module.Name)  interrupted" Yellow
        Add-Log "INTERRUPTED $($Module.Short)"
        $null = Read-Input 'record as fail? [Y/n]>'
        $ok = $false
    }
    $dur = Format-Elapsed ((Get-Date) - $start)
    $Script:Sess.Results.Add([PSCustomObject]@{ Name=$Module.Name; Short=$Module.Short; Id=$Module.Id; Ok=$ok; Dur=$dur; Type=$Module.Type })
    if ($ok) {
        $null = $Script:Sess.FailedIds.Remove($Module.Id)
        Update-AnyFailed
        Write-C "  [ OK ]  $($Module.Name)  $dur" Green
        Add-Log "PASS $($Module.Short) $dur"
    } else {
        $Script:Sess.AnyFailed = $true
        $null = $Script:Sess.FailedIds.Add($Module.Id)
        Write-C "  [FAIL]  $($Module.Name)  $dur" Red
        Add-Log "FAIL $($Module.Short) $dur"
    }
}

function Invoke-E2ESuite {
    param([switch]$Background)
    Write-Sep 'E2E'
    if (-not (Test-Path $Script:E2EDir)) { Write-C "  [FAIL]  $Script:E2EDir not found." Red; return }
    if (-not (Test-Playwright)) {
        Write-C "  [WARN]  Playwright not ready." Yellow
        Write-C "          cd tests\e2e && npm install && npx playwright install" DarkGray
        return
    }
    Measure-E2EBaseUrl
    if ($Background) {
        $logPath = Join-Path $Script:ResultsDir 'e2e-bg.log'
        $e2eDir  = $Script:E2EDir
        $useBlob = $Script:E2EBlobReporter
        $job = Start-Job -Name 'e2e' -ScriptBlock {
            param([string]$dir, [string]$lp, [bool]$useBlob)
            $env:PLAYWRIGHT_HTML_OPEN = 'never'
            Set-Location $dir
            if ($useBlob) {
                $out = & npx playwright test --reporter=blob --reporter=list 2>&1
                $testCode = $LASTEXITCODE
                $out | Out-File $lp -Encoding UTF8
                $mergeOut = & npx playwright merge-reports blob-report --reporter=html 2>&1
                $mergeOut | Out-File $lp -Encoding UTF8 -Append
                $testCode
            } else {
                $out = & npx playwright test 2>&1
                $out | Out-File $lp -Encoding UTF8
                $LASTEXITCODE
            }
        } -ArgumentList $e2eDir, $logPath, $useBlob
        $fakeModule = [PSCustomObject]@{ Name='E2E (Playwright)'; Short='e2e'; Id=0; Type='E2E' }
        $Script:BgJobs.Add([PSCustomObject]@{
            Module    = $fakeModule
            Job       = $job
            Started   = Get-Date
            LogPath   = $logPath
            Collected = $false
            Ok        = $null
            Dur       = $null
            Stopped   = $false
        })
        Write-C "  [ BG ]  E2E  queued as job $($job.Id)" Cyan
        Add-Log "BG-START e2e job=$($job.Id)"
        return
    }
    $ok    = $false
    $start = Get-Date
    $env:PLAYWRIGHT_HTML_OPEN = 'never'
    try {
        $interrupted = $false
        if ($Script:E2EBlobReporter) {
            $r1 = Invoke-Process -Command 'npx' -Arguments @('playwright', 'test', '--reporter=blob', '--reporter=list') -WorkDir $Script:E2EDir
            $interrupted = $r1.Interrupted
            if (-not $interrupted) {
                $r2 = Invoke-Process -Command 'npx' -Arguments @('playwright', 'merge-reports', 'blob-report', '--reporter=html') -WorkDir $Script:E2EDir
                $ok = $r1.ExitCode -eq 0
            }
        } else {
            $r1 = Invoke-Process -Command 'npx' -Arguments @('playwright', 'test') -WorkDir $Script:E2EDir
            $interrupted = $r1.Interrupted
            $ok = $r1.ExitCode -eq 0
        }
        if ($interrupted) {
            Write-C "  [STOP]  E2E interrupted" Yellow
            Add-Log "INTERRUPTED e2e"
            $null = Read-Input 'record as fail? [Y/n]>'
            $ok = $false
        } else {
            Save-E2EReport -Ok $ok
        }
    } finally { Remove-Item env:PLAYWRIGHT_HTML_OPEN -ErrorAction SilentlyContinue }
    $dur = Format-Elapsed ((Get-Date) - $start)
    $Script:Sess.Results.Add([PSCustomObject]@{ Name='E2E (Playwright)'; Short='e2e'; Id=0; Ok=$ok; Dur=$dur; Type='E2E' })
    if ($ok) {
        Update-AnyFailed
        Write-C "  [ OK ]  E2E  $dur" Green
        Add-Log "PASS e2e $dur"
    } else {
        $Script:Sess.AnyFailed = $true
        Write-C "  [FAIL]  E2E  $dur" Red
        $rp = Join-Path $Script:E2EDir "$Script:E2EReportFolder\index.html"
        if (Test-Path $rp) { Write-C "          report: $rp" Yellow }
        Add-Log "FAIL e2e $dur"
    }
}

function Invoke-LoadSuite {
    param([switch]$Background)
    Write-Sep 'Load'
    $proj = Join-Path $ScriptRoot $Script:LoadProject
    if (-not (Test-Path $proj)) { Write-C "  [FAIL]  Load project not found: $proj" Red; return }
    if ($Background) {
        $logPath = Join-Path $Script:ResultsDir 'load-bg.log'
        $job = Start-Job -Name 'load' -ScriptBlock {
            param([string]$p, [string]$lp)
            $out = & dotnet run --project $p -c Release 2>&1
            $out | Out-File $lp -Encoding UTF8
            $LASTEXITCODE
        } -ArgumentList $proj, $logPath
        $fakeModule = [PSCustomObject]@{ Name='Load (NBomber)'; Short='load'; Id=0; Type='Load' }
        $Script:BgJobs.Add([PSCustomObject]@{
            Module=$fakeModule; Job=$job; Started=Get-Date; LogPath=$logPath
            Collected=$false; Ok=$null; Dur=$null; Stopped=$false
        })
        Write-C "  [ BG ]  Load  queued as job $($job.Id)" Cyan
        Add-Log "BG-START load job=$($job.Id)"
        return
    }
    $start = Get-Date
    $r = Invoke-Process -Command $Script:DotnetCmd -Arguments @('run', '--project', $proj, '-c', 'Release') -WorkDir $ScriptRoot
    $ok  = (-not $r.Interrupted) -and ($r.ExitCode -eq 0)
    $dur = Format-Elapsed ((Get-Date) - $start)
    $Script:Sess.Results.Add([PSCustomObject]@{ Name='Load (NBomber)'; Short='load'; Id=0; Ok=$ok; Dur=$dur; Type='Load' })
    if ($ok) {
        Update-AnyFailed
        Write-C "  [ OK ]  Load  $dur" Green
        Add-Log "PASS load $dur"
    } else {
        $Script:Sess.AnyFailed = $true
        Write-C "  [FAIL]  Load  $dur  (thresholds breached or services down)" Red
        Add-Log "FAIL load $dur"
    }
}

function Invoke-UnitSuite {
    param([switch]$Background)
    Write-Sep 'Unit'
    $Script:Modules | Where-Object { $_.Type -eq 'Unit' } | ForEach-Object { Invoke-Suite $_ -Background:$Background }
}

function Invoke-IntegrationSuite {
    param([switch]$WarnIfUnitFailed, [switch]$Background)
    if ($WarnIfUnitFailed) {
        if (@($Script:Sess.Results | Where-Object { -not $_.Ok -and $_.Type -eq 'Unit' }).Count -gt 0) {
            Write-C "  [WARN]  Unit failures present - integration results may be unreliable." Yellow
        }
    }
    if (-not (Test-Docker)) {
        Write-C "  [FAIL]  Docker not running.  Use [D] to start it, then retry." Red
        return
    }
    Write-Sep 'Integration'
    $Script:Modules | Where-Object { $_.Type -eq 'Integration' } | ForEach-Object { Invoke-Suite $_ -Background:$Background }
}

function Invoke-WatchMode {
    Write-Sep 'Watch'
    foreach ($m in $Script:Modules) {
        $tag = if ($m.Type -eq 'Integration') { '  [Docker]' } else { '' }
        Write-C ("   [{0}]  {1,-28}  {2}{3}" -f $m.Id, $m.Name, $m.Type, $tag) White
    }
    Write-Ln
    $sel = (Read-Input 'module [#]>').Trim()
    $m   = $Script:Modules | Where-Object { $_.Id -eq [int]$sel }
    if (-not $m) { Write-C "  [WARN]  Invalid selection." Yellow; return }
    if ($m.Type -eq 'Integration' -and -not (Test-Docker)) {
        Write-C "  [FAIL]  Docker not running.  Use [D] to start it." Red; return
    }
    $abs       = Join-Path $ScriptRoot $m.Path
    $watchArgs = [System.Collections.Generic.List[string]]::new()
    $watchArgs.Add('watch')
    $watchArgs.Add('--project'); $watchArgs.Add($abs)
    $watchArgs.Add('test')
    if ($Script:Filter) { $watchArgs.Add('--filter'); $watchArgs.Add($Script:Filter) }
    Write-C "  Watching $($m.Name)$(if ($Script:Filter) { "  filter: $Script:Filter" })  -  ESC or CTRL+C to stop." Cyan
    Write-Ln
    Invoke-Process -Command $Script:DotnetCmd -Arguments $watchArgs.ToArray() | Out-Null
}

function Invoke-RerunFailed {
    param([switch]$Background)
    $Script:Sess.CovLine   = $null
    $Script:Sess.CovBranch = $null
    [int[]]$ids = @()
    if ($Script:Sess.FailedIds.Count -gt 0) {
        $ids = @($Script:Sess.FailedIds)
    } else {
        $saved     = Get-SavedState
        $savedProp = if ($null -ne $saved) { $saved.PSObject.Properties['Failed'] } else { $null }
        if ($null -ne $savedProp -and $null -ne $savedProp.Value) {
            $ids = @($savedProp.Value | ForEach-Object { [int]$_ })
            if ($ids.Count -gt 0) { Write-C "  [WARN]  No failures in session - using previous session state." Yellow }
        }
    }
    if ($ids.Count -eq 0) { Write-C "  No failures on record." Yellow; return }
    if (@($Script:Modules | Where-Object { $ids -contains $_.Id -and $_.Type -eq 'Integration' }).Count -gt 0 -and -not (Test-Docker)) {
        Write-C "  [FAIL]  Docker required for some failed suites.  Use [D] to start it." Red
        return
    }
    Write-Sep 'Re-running failed'
    foreach ($id in $ids) {
        $m = $Script:Modules | Where-Object { $_.Id -eq [int]"$id" }
        if ($m) { Invoke-Suite $m -Background:$Background }
    }
}

function Select-CoverageScope {
    $ranIds      = @($Script:Sess.Results | Where-Object { $_.Ok } | ForEach-Object { $_.Id })
    $ranMods     = @($Script:Modules | Where-Object { $ranIds -contains $_.Id })
    $testedAIds  = @($ranMods | ForEach-Object { $_.AsmIds } | Where-Object { $_ } | Sort-Object -Unique)
    $testedAsms  = @($Script:Assemblies | Where-Object { $testedAIds -contains $_.Id })
    $testedLabel = if ($testedAsms.Count -gt 0) { ($testedAsms | ForEach-Object { $_.Label }) -join ', ' } else { 'none' }
    $testedFilter = ($testedAsms | ForEach-Object { $_.Filter }) -join ';'
    Write-Ln
    Write-Sep 'Coverage Scope'
    Write-C "  Session tested:  $testedLabel" DarkGray
    Write-Ln
    Write-C "   T   Tested assemblies only  (default)" White
    Write-C "   L   All assemblies" White
    Write-C "   P   Pick assemblies" White
    Write-Ln
    $sel = (Read-Input 'scope>').Trim().ToUpper()
    if ($sel -eq 'L') { return $Script:AllAsmFilter }
    if ($sel -eq 'P') {
        Write-Ln
        Write-Sep 'Pick Assemblies - combine letters e.g. AC'
        foreach ($a in $Script:Assemblies) { Write-C ("   [$($a.Id)]  $($a.Label)") White }
        Write-Ln
        $pick     = (Read-Input 'letters>').Trim().ToUpper()
        $selected = @($Script:Assemblies | Where-Object { $pick.Contains($_.Id) })
        if ($selected.Count -eq 0) {
            Write-C "  [WARN]  No valid selection - using all." Yellow
            return $Script:AllAsmFilter
        }
        return ($selected | ForEach-Object { $_.Filter }) -join ';'
    }
    if ($testedFilter -eq '') {
        Write-C "  [WARN]  No tested assemblies in session - using all." Yellow
        return $Script:AllAsmFilter
    }
    return $testedFilter
}

function Invoke-ReportGenerator {
    param([string]$AsmFilter)
    $xmlFiles = @(Get-ChildItem -Path $Script:ResultsDir -Filter 'coverage.cobertura.xml' -Recurse -ErrorAction SilentlyContinue)
    if ($xmlFiles.Count -eq 0) {
        Write-C "  [WARN]  No coverage XML found - run tests first." Yellow
        return
    }
    $ts     = Get-Date -Format 'yyyyMMdd_HHmmss'
    $outDir = Join-Path $Script:ReportDir $ts
    $rgArgs = @(
        "-reports:$Script:ResultsDir\**\coverage.cobertura.xml",
        "-targetdir:$outDir",
        '-reporttypes:Html;Cobertura',
        "-assemblyfilters:$AsmFilter",
        "-classfilters:$Script:ClsFilter"
    )
    $r = Invoke-Process -Command $Script:ReportGeneratorCmd -Arguments $rgArgs
    if ($r.ExitCode -ne 0) {
        Write-C "  [FAIL]  reportgenerator failed." Red
        Write-C "          dotnet tool install -g dotnet-reportgenerator-globaltool" DarkGray
        return
    }
    $cobFile = Join-Path $outDir 'Cobertura.xml'
    if (-not (Test-Path $cobFile)) { return }
    try {
        [xml]$cob = Get-Content $cobFile -ErrorAction Stop
        $node     = $cob.SelectSingleNode('//coverage')
        if ($null -eq $node) { throw 'Cobertura root node missing' }
        $lr = [math]::Round([double]$node.'line-rate'   * 100, 1)
        $br = [math]::Round([double]$node.'branch-rate' * 100, 1)
        $Script:Sess.CovLine   = $lr
        $Script:Sess.CovBranch = $br
        $lOk = $lr -ge $Script:LineThr
        $bOk = $br -ge $Script:BranchThr
        Write-Ln
        Write-C ("  {0,-10}  {1,5}%   threshold {2}%" -f 'Line',   $lr, $Script:LineThr)   $(if ($lOk) { 'Green' } else { 'Red' })
        Write-C ("  {0,-10}  {1,5}%   threshold {2}%" -f 'Branch', $br, $Script:BranchThr) $(if ($bOk) { 'Green' } else { 'Red' })
        Write-Ln
        if (-not $lOk -or -not $bOk) {
            $Script:Sess.AnyFailed = $true
            Write-C "  [FAIL]  Quality gate FAILED" Red
            Add-Log "COVERAGE FAIL line=$lr% branch=$br%"
        } else {
            Write-C "  [ OK ]  Quality gate PASSED" Green
            Add-Log "COVERAGE PASS line=$lr% branch=$br%"
        }
    } catch {
        Write-C "  [FAIL]  Coverage parse error: $_" Red
        Add-Log "COVERAGE PARSE ERROR: $_"
    }
    Write-C "  $outDir\index.html" DarkGray
}

function Invoke-Coverage {
    Write-Sep 'Coverage'
    $asmFilter = Select-CoverageScope
    Invoke-ReportGenerator -AsmFilter $asmFilter
}

function Invoke-Cleanup {
    Write-Ln
    Write-Sep 'Cleaning'
    @($Script:ResultsDir, $Script:ReportDir, $Script:E2EReportDir) | ForEach-Object {
        if (Test-Path $_) {
            $count = @(Get-ChildItem $_ -Recurse -File -ErrorAction SilentlyContinue).Count
            Remove-Item $_ -Recurse -Force
            Write-C "  removed: $_  ($count file(s))" DarkGray
        } else {
            Write-C "  skipped: $_ (not found)" DarkGray
        }
    }
    $sesPath = Get-StateFilePath
    if (Test-Path $sesPath) {
        Remove-Item $sesPath -Force
        $Script:PrevResults   = @{}
        $Script:Sess.AnyFailed = $false
        $Script:Sess.CovLine   = $null
        $Script:Sess.CovBranch = $null
        $Script:Sess.FailedIds.Clear()
        Write-C "  removed: $sesPath" DarkGray
    }
    Write-Ln
    Write-C "  [ OK ]  Clean." Green
    Add-Log 'CLEANUP'
    $null = Read-Input 'press Enter'
}

function Invoke-Discover {
    Write-Sep 'Discover'
    $testFiles = @(Get-ChildItem -Path $ScriptRoot -Recurse -Filter $Script:DiscoverGlob -ErrorAction SilentlyContinue |
        Where-Object { $_.BaseName -match '\.Tests?$' -and $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
        Sort-Object BaseName)
    if ($testFiles.Count -eq 0) {
        Write-C "  [WARN]  No *.Tests.csproj found under $ScriptRoot" Yellow
        return
    }
    Write-C "  Found $($testFiles.Count) test project(s)." DarkGray
    Write-Ln

    $namespaces = @($testFiles | ForEach-Object { $_.BaseName })
    if ($namespaces.Count -eq 1) {
        $firstDot = $namespaces[0].IndexOf('.')
        $prefix   = if ($firstDot -ge 0) { $namespaces[0].Substring(0, $firstDot + 1) } else { '' }
    } else {
        $prefix = $namespaces[0]
        foreach ($ns in ($namespaces | Select-Object -Skip 1)) {
            while ($prefix.Length -gt 0 -and -not $ns.StartsWith($prefix)) {
                $lastDot = $prefix.LastIndexOf('.')
                $prefix  = if ($lastDot -ge 0) { $prefix.Substring(0, $lastDot) } else { '' }
            }
        }
        if ($prefix.Length -gt 0 -and -not $prefix.EndsWith('.')) { $prefix += '.' }
    }

    $asmList = [System.Collections.Generic.List[PSCustomObject]]::new()
    $modules = [System.Collections.Generic.List[PSCustomObject]]::new()
    $asmIdx  = 65
    $modId   = 1

    $sorted = @($testFiles | Sort-Object {
        $isInt = [int]($_.BaseName -match $Script:DiscoverIntRegex)
        "$isInt`_$($_.BaseName)"
    })

    foreach ($f in $sorted) {
        $ns            = $f.BaseName
        $isIntegration = $ns -match $Script:DiscoverIntRegex
        $cleaned       = $ns -replace '\.Integration\.Tests$', '' -replace '\.Tests$', ''
        $stripped      = if ($prefix.Length -gt 0 -and $cleaned.StartsWith($prefix) -and $cleaned.Length -gt $prefix.Length) { $cleaned.Substring($prefix.Length) } else { $cleaned }
        $short         = ($stripped -replace '\.', '-').ToLower() + $(if ($isIntegration) { '-int' } else { '-unit' })
        $name          = ($stripped -replace '\.', ' ') + $(if ($isIntegration) { ' Integration' } else { '' })
        $relDir        = Get-RelativePath $ScriptRoot $f.DirectoryName
        $type          = if ($isIntegration) { 'Integration' } else { 'Unit' }

        $existing = $asmList | Where-Object { $_.Key -eq $cleaned } | Select-Object -First 1
        if (-not $existing) {
            $asmStripped = if ($prefix.Length -gt 0 -and $cleaned.StartsWith($prefix) -and $cleaned.Length -gt $prefix.Length) { $cleaned.Substring($prefix.Length) } else { $cleaned }
            $existing = [PSCustomObject]@{
                Id     = [char]$asmIdx
                Key    = $cleaned
                Label  = $asmStripped -replace '\.', ' '
                Filter = "+$cleaned"
            }
            $asmList.Add($existing)
            $asmIdx++
        }

        $modules.Add([PSCustomObject]@{
            Id    = $modId
            Name  = $name
            Short = $short
            Path  = $relDir
            Type  = $type
            AsmId = $existing.Id
        })
        $modId++
    }

    Write-Sep 'Modules'
    foreach ($m in $modules) {
        $tag = if ($m.Type -eq 'Integration') { '[Docker]' } else { '' }
        Write-C ("  {0}  {1,-30}  {2,-14}  {3,-26}  {4}" -f $m.Id, $m.Name, $m.Type, $m.Short, $tag) White
    }
    Write-Ln
    Write-Sep 'Assemblies'
    foreach ($a in $asmList) {
        Write-C ("  [$($a.Id)]  {0,-30}  {1}" -f $a.Label, $a.Filter) White
    }
    Write-Ln
    Write-C "  Common prefix stripped: '$prefix'" DarkGray
    Write-C "  Tip: edit AsmIds in modules.dat to merge shared assembly groups." DarkGray
    Write-Ln

    $modFile = Join-Path $Script:DataDir 'Modules.dat'
    $asmFile = Join-Path $Script:DataDir 'Assemblies.dat'
    $svcFile = Join-Path $Script:DataDir 'Services.dat'

    $existingAsmIds = @{}
    if (Test-Path $modFile) {
        foreach ($el in (Get-Content $modFile -ErrorAction SilentlyContinue)) {
            if ($el -match '^\s*#' -or $el.Trim() -eq '') { continue }
            $ep = $el.Split('|')
            if ($ep.Count -ge 6 -and $ep[5].Trim()) { $existingAsmIds[$ep[3].Trim()] = $ep[5].Trim() }
        }
        Write-C "  [INFO]  Existing AsmIds will be preserved for matching paths." DarkGray
    }
    if ((Test-Path $modFile) -or (Test-Path $asmFile)) {
        Write-C "  [WARN]  Data files exist and will be rewritten." Yellow
    }
    $c = (Read-Input 'write [Y/N]>').Trim().ToUpper()
    if ($c -ne 'Y') { Write-C "  Cancelled." DarkGray; return }

    $mLines = [System.Collections.Generic.List[string]]::new()
    $mLines.Add('# id|name|short|path|type|asmids (comma-separated)')
    foreach ($m in $modules) {
        $asmId = if ($existingAsmIds.ContainsKey($m.Path)) { $existingAsmIds[$m.Path] } else { "$($m.AsmId)" }
        $mLines.Add("$($m.Id)|$($m.Name)|$($m.Short)|$($m.Path)|$($m.Type)|$asmId")
    }
    $mLines | Set-Content $modFile -Encoding UTF8

    $aLines = [System.Collections.Generic.List[string]]::new()
    $aLines.Add('# id|label|filter')
    foreach ($a in $asmList) {
        $aLines.Add("$($a.Id)|$($a.Label)|$($a.Filter)")
    }
    $aLines | Set-Content $asmFile -Encoding UTF8

    Write-C "  [ OK ]  Modules.dat      $($modules.Count) module(s)" Green
    Write-C "  [ OK ]  Assemblies.dat   $($asmList.Count) assembly group(s)" Green
    Add-Log "DISCOVER modules=$($modules.Count) assemblies=$($asmList.Count)"

    Write-Ln
    Write-Sep 'Load suite'
    $loadProj = @(Get-ChildItem -Path $ScriptRoot -Recurse -Filter '*.csproj' -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.BaseName -notmatch '\.Tests?$' } |
        Where-Object { $_.BaseName -match '(?i)(load|perf|benchmark)' -or $_.DirectoryName -match '(?i)[\\/](load|perf|benchmark)[a-z]*[\\/]' } |
        Sort-Object FullName) | Select-Object -First 1
    if ($loadProj) {
        $rel = (Get-RelativePath $ScriptRoot $loadProj.FullName) -replace '\\', '/'
        Write-C "  [ OK ]  Load project      $rel" Green
        Write-C "          Run with [L] or -Suite Load  (a suite, not a numbered module)" DarkGray
        if ($Script:LoadProject -ne $rel) {
            $Script:LoadProject = $rel
            Export-Config
            Write-C "  [ OK ]  LoadProject saved to $($Script:AppName).cfg" Green
            Add-Log "DISCOVER load-project=$rel"
        }
    } else {
        Write-C "  No load / performance project found (optional - configure LoadProject in $($Script:AppName).cfg)." DarkGray
    }

    if (-not (Test-Path $svcFile) -and $Script:ComposeServices.Count -gt 0) {
        Write-Ln
        Write-C "  Services.dat not found.  Write template from compose services? [Y/N]" Yellow
        $sc = (Read-Input 'services template [Y/N]>').Trim().ToUpper()
        if ($sc -eq 'Y') {
            $sLines = [System.Collections.Generic.List[string]]::new()
            $sLines.Add('# name|display|group|url')
            foreach ($svc in $Script:ComposeServices) { $sLines.Add("$($svc.Name)|||") }
            $sLines | Set-Content $svcFile -Encoding UTF8
            Write-C "  [ OK ]  Services.dat     $($Script:ComposeServices.Count) service(s) - fill in display/group/url" Green
            Add-Log "DISCOVER services-template=$($Script:ComposeServices.Count)"
        }
    }

    $Script:Assemblies   = Read-AssembliesData
    $Script:AllAsmFilter = ($Script:Assemblies | ForEach-Object { $_.Filter }) -join ';'
    $Script:Modules      = Read-ModulesData
    $Script:MaxModuleId  = if ($Script:Modules.Count -gt 0) { ($Script:Modules | Measure-Object -Property Id -Maximum).Maximum } else { 9 }
}

function Select-ComposeService {
    param([string]$Prompt)
    Write-Ln
    $svcs = $Script:ComposeServices
    for ($i = 0; $i -lt $svcs.Count; $i++) {
        $pl = if ($svcs[$i].Port) { "  :$($svcs[$i].Port)" } else { '' }
        Write-C ("    [{0,2}]  {1,-14}{2}" -f ($i + 1), $svcs[$i].Display, $pl) White
    }
    Write-Ln
    $pick = (Read-Input $Prompt).Trim()
    if ($pick -match '^\d+$') {
        $idx = [int]$pick - 1
        if ($idx -ge 0 -and $idx -lt $svcs.Count) { return $svcs[$idx].Name }
    }
    Write-C "  [WARN]  Invalid selection." Yellow
    return $null
}

function Show-ReportsPanel {
    while ($true) {
        Sync-BackgroundJobs
        $cov = @(Get-ChildItem -Path $Script:ReportDir -Directory -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending)
        $e2e = @()
        if (Test-Path $Script:E2EReportDir) {
            $e2e = @(Get-ChildItem -Path $Script:E2EReportDir -Directory -ErrorAction SilentlyContinue |
                Sort-Object Name -Descending)
        }
        $pending = @($Script:BgJobs | Where-Object { -not $_.Collected })
        Enter-Render
        Write-Header 'Reports'
        Write-Ln
        if ($pending.Count -gt 0) {
            Write-Sep 'Running'
            foreach ($j in $pending) {
                $elapsed = Format-Elapsed ((Get-Date) - $j.Started)
                Write-C ("  [ BG ]  {0,-28}  {1}" -f $j.Module.Name, $elapsed) Cyan
            }
            Write-Ln
        }
        Write-Sep 'Coverage'
        if ($cov.Count -gt 0) {
            for ($i = 0; $i -lt $cov.Count; $i++) {
                $label  = $cov[$i].Name -replace '^(\d{4})(\d{2})(\d{2})_(\d{2})(\d{2})(\d{2})$', '$1-$2-$3  $4:$5'
                $stats  = Get-CoverageStats $cov[$i].FullName
                $sStr   = if ($stats) { "  $($stats.Line)% ln  $($stats.Branch)% br" } else { '' }
                $latest = if ($i -eq 0) { '  (latest)' } else { '' }
                Write-C ("  C{0}  {1}{2}{3}" -f ($i + 1), $label, $sStr, $latest) White
            }
        } else {
            Write-C '  No coverage reports yet.' DarkGray
        }
        Write-Ln
        Write-Sep 'E2E'
        if ($e2e.Count -gt 0) {
            for ($i = 0; $i -lt $e2e.Count; $i++) {
                $label  = $e2e[$i].Name -replace '^(\d{4})(\d{2})(\d{2})_(\d{2})(\d{2})(\d{2})$', '$1-$2-$3  $4:$5'
                $meta   = Get-E2EReportMeta $e2e[$i].FullName
                $status = if ($null -eq $meta) { '      ' } elseif ($meta.Ok) { '[ OK ]' } else { '[FAIL]' }
                $scol   = if ($null -eq $meta) { 'DarkGray' } elseif ($meta.Ok) { 'Green' } else { 'Red' }
                $latest = if ($i -eq 0) { '  (latest)' } else { '' }
                Write-C "  E$($i + 1)  $label  " White -NoNewLine
                Write-C $status $scol -NoNewLine
                Write-C $latest DarkGray
            }
        } else {
            Write-C '  No E2E reports yet.' DarkGray
        }
        Write-Ln
        Write-Sep 'Actions'
        Write-C '  C#   E#         open report        append & to run in background' White
        Write-C '  C#D  E#D        delete with confirm' White
        Write-C '  E#S             serve report live (playwright show-report)' White
        Write-C '  +C   +C&        generate coverage report' White
        Write-C '  +E   +E&        re-run / regen E2E' White
        Write-C '  *D   C*D  E*D   delete all (by type)' White
        Write-C '  *P<n>           prune — keep n newest per type' White
        Write-C '  Q               back' White
        Write-Ln
        Exit-Render
        $selRaw = Read-MenuInput 'reports'
        if ($null -eq $selRaw) { continue }
        $sel = $selRaw.Trim()
        if ($sel -eq '' -or $sel -ieq 'Q') { return }
        if ($sel -imatch '^C(\d+)D$') {
            $idx = [int]$Matches[1] - 1
            if ($idx -ge 0 -and $idx -lt $cov.Count) {
                $conf = (Read-Input "delete $($cov[$idx].Name) [Y/N]").Trim().ToUpper()
                if ($conf -eq 'Y') { Remove-Item $cov[$idx].FullName -Recurse -Force }
            } else { Write-C "  Unknown: '$sel'" Yellow; Start-Sleep 1 }
            continue
        }
        if ($sel -imatch '^E(\d+)D$') {
            $idx = [int]$Matches[1] - 1
            if ($idx -ge 0 -and $idx -lt $e2e.Count) {
                $conf = (Read-Input "delete $($e2e[$idx].Name) [Y/N]").Trim().ToUpper()
                if ($conf -eq 'Y') { Remove-Item $e2e[$idx].FullName -Recurse -Force }
            } else { Write-C "  Unknown: '$sel'" Yellow; Start-Sleep 1 }
            continue
        }
        if ($sel -imatch '^E(\d+)S$') {
            $idx = [int]$Matches[1] - 1
            if ($idx -ge 0 -and $idx -lt $e2e.Count) {
                Start-Process powershell -ArgumentList "-NoExit","-NoProfile","-Command","npx playwright show-report '$($e2e[$idx].FullName)'"
            } else { Write-C "  Unknown: '$sel'" Yellow; Start-Sleep 1 }
            continue
        }
        if ($sel -imatch '^C(\d+)$') {
            $idx = [int]$Matches[1] - 1
            if ($idx -ge 0 -and $idx -lt $cov.Count) {
                Start-Process (Join-Path $cov[$idx].FullName 'index.html')
            } else { Write-C "  Unknown: '$sel'" Yellow; Start-Sleep 1 }
            continue
        }
        if ($sel -imatch '^E(\d+)$') {
            $idx = [int]$Matches[1] - 1
            if ($idx -ge 0 -and $idx -lt $e2e.Count) {
                Start-Process (Join-Path $e2e[$idx].FullName 'index.html')
            } else { Write-C "  Unknown: '$sel'" Yellow; Start-Sleep 1 }
            continue
        }
        if ($sel -ieq '+C') {
            Invoke-Coverage
            $null = Read-Input 'press Enter'
            continue
        }
        if ($sel -ieq '+C&') {
            Invoke-Coverage
            continue
        }
        if ($sel -ieq '+E' -or $sel -ieq '+E&') {
            $bg      = $sel -ieq '+E&'
            $blobDir = Join-Path $Script:E2EDir 'blob-report'
            if (-not $bg -and $Script:E2EBlobReporter -and (Test-Path $blobDir)) {
                Push-Location $Script:E2EDir
                try { & npx playwright merge-reports blob-report --reporter=html } finally { Pop-Location }
                Save-E2EReport -Ok $true
                $null = Read-Input 'press Enter'
            } else {
                Invoke-E2ESuite -Background:$bg
                if (-not $bg) {
                    Show-Summary
                    $null = Read-Input 'press Enter'
                }
            }
            continue
        }
        if ($sel -imatch '^\*D$' -or $sel -imatch '^C\*D$' -or $sel -imatch '^E\*D$') {
            $delC = $sel -imatch '^\*D$' -or $sel -imatch '^C\*D$'
            $delE = $sel -imatch '^\*D$' -or $sel -imatch '^E\*D$'
            $desc = if ($sel -imatch '^\*D$') { 'all reports' } elseif ($sel -imatch '^C\*D$') { 'all coverage reports' } else { 'all E2E reports' }
            $conf = (Read-Input "delete $desc [Y/N]").Trim().ToUpper()
            if ($conf -eq 'Y') {
                if ($delC) { $cov | ForEach-Object { Remove-Item $_.FullName -Recurse -Force } }
                if ($delE) { $e2e | ForEach-Object { Remove-Item $_.FullName -Recurse -Force } }
            }
            continue
        }
        if ($sel -imatch '^\*P(\d+)$') {
            $keep = [int]$Matches[1]
            if ($cov.Count -gt $keep) { $cov | Select-Object -Skip $keep | ForEach-Object { Remove-Item $_.FullName -Recurse -Force } }
            if ($e2e.Count -gt $keep) { $e2e | Select-Object -Skip $keep | ForEach-Object { Remove-Item $_.FullName -Recurse -Force } }
            continue
        }
        Write-C "  Unknown: '$sel'" Yellow
        Start-Sleep 1
    }
}

function Show-DockerPanel {
    while ($true) {
        $dockerOk = Test-Docker
        $status   = Get-ContainerStatus
        Enter-Render
        Write-Header 'Docker'
        Write-Ln
        Write-C '  Docker Desktop:  ' White -NoNewLine
        if ($dockerOk) { Write-C 'running' Green } else { Write-C 'not running' Red }
        Write-Ln
        Write-Sep 'Infrastructure'
        foreach ($svc in ($Script:ComposeServices | Where-Object { $_.Group -eq 'infra' })) {
            $st  = if ($status.ContainsKey($svc.Name)) { $status[$svc.Name] } else { '-' }
            $col = switch ($st) { 'running' { 'Green' } '-' { 'DarkGray' } default { 'Yellow' } }
            $pl  = if ($svc.Port) { ":$($svc.Port)" } else { '' }
            Write-C ("    {0,-14}  {1,-12}  {2}" -f $svc.Display, $st, $pl) $col
        }
        Write-Ln
        Write-Sep 'Application'
        foreach ($svc in ($Script:ComposeServices | Where-Object { $_.Group -eq 'app' })) {
            $st  = if ($status.ContainsKey($svc.Name)) { $status[$svc.Name] } else { '-' }
            $col = switch ($st) { 'running' { 'Green' } '-' { 'DarkGray' } default { 'Yellow' } }
            $pl  = if ($svc.Port) { ":$($svc.Port)" } else { '' }
            Write-C ("    {0,-14}  {1,-12}  {2}" -f $svc.Display, $st, $pl) $col
        }
        Write-Ln
        Write-Sep 'Actions'
        $infraLabel = if ($Script:InfraUpServices.Count -gt 0) { $Script:InfraUpServices -join ' + ' } else { 'not configured' }
        Write-C "   U   Stack up         (compose up -d)" White
        Write-C "   D   Stack down       (compose down)" White
        Write-C "   I   Infra up         ($infraLabel)" White
        Write-C "   S   Stop service     T   Start service    X   Restart service" White
        Write-C "   L   Logs  (tail)     B   Build images" White
        Write-C "   O   Open in browser  R   Refresh          Q   Back" White
        Write-Ln
        Exit-Render
        $raw = Read-MenuInput 'docker'
        if ($null -eq $raw) { continue }
        $inp = $raw.Trim().ToUpper()
        if ($inp -eq '' -or $inp -eq 'Q') { return }
        switch ($inp) {
            'R' { continue }
            'U' { Write-Ln; & docker compose -f $Script:ComposeFile up -d;    Write-Ln; $null = Read-Input }
            'D' { Write-Ln; & docker compose -f $Script:ComposeFile down;      Write-Ln; $null = Read-Input }
            'I' {
                if ($Script:InfraUpServices.Count -eq 0) {
                    Write-C "  [WARN]  InfraUpServices not configured in $Script:AppName.cfg" Yellow; Start-Sleep 1
                } else {
                    Write-Ln; & docker compose -f $Script:ComposeFile up -d @Script:InfraUpServices; Write-Ln; $null = Read-Input
                }
            }
            'B' { Write-Ln; & docker compose -f $Script:ComposeFile build;    Write-Ln; $null = Read-Input }
            'S' { $n = Select-ComposeService 'stop service [#]';    if ($n) { Write-Ln; & docker compose -f $Script:ComposeFile stop $n;    Start-Sleep 1 } }
            'T' { $n = Select-ComposeService 'start service [#]';   if ($n) { Write-Ln; & docker compose -f $Script:ComposeFile start $n;   Start-Sleep 1 } }
            'X' { $n = Select-ComposeService 'restart service [#]'; if ($n) { Write-Ln; & docker compose -f $Script:ComposeFile restart $n; Start-Sleep 1 } }
            'L' {
                $n = Select-ComposeService 'logs for service [#]'
                if ($n) {
                    Write-Ln; Write-C "  Streaming $n - Ctrl+C to stop..." Yellow; Write-Ln
                    & docker compose -f $Script:ComposeFile logs -f --tail=100 $n
                    Write-Ln; $null = Read-Input
                }
            }
            'O' {
                $us = @($Script:ComposeServices | Where-Object { $_.Url -ne '' })
                Write-Ln
                for ($i = 0; $i -lt $us.Count; $i++) {
                    Write-C ("    [{0}]  {1,-14}  {2}" -f ($i+1), $us[$i].Display, $us[$i].Url) White
                }
                Write-Ln
                $p = (Read-Input 'open [#]').Trim()
                if ($p -match '^\d+$') {
                    $idx = [int]$p - 1
                    if ($idx -ge 0 -and $idx -lt $us.Count) { Start-Process $us[$idx].Url }
                }
            }
            default { Write-C "  Unknown: '$inp'" Yellow; Start-Sleep 1 }
        }
    }
}

function Show-Stats {
    while ($true) {
        Sync-BackgroundJobs
        $hasBg = @($Script:BgJobs | Where-Object { -not $_.Collected }).Count -gt 0
        Enter-Render
        Write-Header 'Statistics'
        Write-Ln
        Write-Sep 'Modules'
        $total = 0
        foreach ($m in $Script:Modules) {
            $stats   = Get-ModuleStats $m
            $lastRun = @($Script:Sess.Results | Where-Object { $_.Short -eq $m.Short }) | Select-Object -Last 1
            $stLbl   = if ($null -ne $lastRun) { if ($lastRun.Ok) { '[ OK ]' } else { '[FAIL]' } } else { '[  -- ]' }
            $durLbl  = if ($null -ne $lastRun) { $lastRun.Dur } else { '' }
            $cntLbl  = if ($null -ne $stats)   { "$($stats.Total) tests ($($stats.Passed) pass)" } else { 'no data' }
            $col     = if ($null -ne $lastRun -and $lastRun.Ok) { 'Green' } elseif ($null -ne $lastRun) { 'Red' } else { 'DarkGray' }
            if ($null -ne $stats) { $total += $stats.Total }
            Write-C ("  {0}  {1}  {2,-28}  {3,-8}  {4}" -f $m.Id, $stLbl, $m.Name, $durLbl, $cntLbl) $col
        }
        Write-Ln
        if ($total -gt 0) { Write-C "  Total tracked:  $total tests" White }
        Write-Ln
        Write-Sep 'Coverage'
        if ($null -ne $Script:Sess.CovLine) {
            $lOk = $Script:Sess.CovLine   -ge $Script:LineThr
            $bOk = $Script:Sess.CovBranch -ge $Script:BranchThr
            Write-C ("  {0,-10}  {1,5}%   threshold {2}%" -f 'Line',   $Script:Sess.CovLine,   $Script:LineThr)   $(if ($lOk) { 'Green' } else { 'Red' })
            Write-C ("  {0,-10}  {1,5}%   threshold {2}%" -f 'Branch', $Script:Sess.CovBranch, $Script:BranchThr) $(if ($bOk) { 'Green' } else { 'Red' })
            $latestRpt = @(Get-ChildItem -Path $Script:ReportDir -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending | Select-Object -First 1)
            if ($latestRpt.Count -gt 0) { Write-C "  $($latestRpt[0].FullName)\index.html  ([O] to browse all)" DarkGray }
        } else {
            Write-C "  No coverage data yet.  Run [C] to generate." DarkGray
        }
        Write-Ln
        Write-Sep 'Session'
        Write-C "  Started:  $(Get-Date $Script:Sess.Start -Format 'yyyy-MM-dd HH:mm:ss')" DarkGray
        Write-C "  Running:  $(Format-Elapsed ((Get-Date) - $Script:Sess.Start))" DarkGray
        Write-C "  Log:      $Script:LogFile" DarkGray
        if ($Script:BgJobs.Count -gt 0 -or $Script:JobQueue.Count -gt 0) {
            Write-Ln
            Write-Sep 'Background Jobs'
            foreach ($entry in $Script:BgJobs) {
                if (-not $entry.Collected) {
                    Write-C ("  [ RUN ]  {0,-28}  running {1}" -f $entry.Module.Name, (Format-Elapsed ((Get-Date) - $entry.Started))) Cyan
                } else {
                    $b = Get-JobBadge $entry
                    Write-C ("  $($b.Tag)  {0,-28}  {1}  log: TestResults\$($entry.Module.Short)-bg.log" -f $entry.Module.Name, $entry.Dur) $b.Col
                }
            }
            foreach ($pending in $Script:JobQueue) {
                Write-C ("  [WAIT]  {0,-28}  waiting for free slot" -f $pending.Module.Name) Yellow
            }
        }
        Write-Ln
        Write-C "  Q / Esc / Enter to go back$(if ($hasBg) { '   auto-refresh 1s' })" DarkGray
        Exit-Render
        if (-not $hasBg) {
            while ($true) {
                if ([Console]::KeyAvailable) {
                    $key = [Console]::ReadKey($true)
                    if ($key.Key -eq [ConsoleKey]::Escape -or
                        $key.KeyChar -eq 'q' -or $key.KeyChar -eq 'Q' -or
                        $key.Key -eq [ConsoleKey]::Enter) { return }
                }
                Start-Sleep -Milliseconds 50
            }
        }
        $deadline = (Get-Date).AddSeconds(1)
        while ((Get-Date) -lt $deadline) {
            if ([Console]::KeyAvailable) {
                $key = [Console]::ReadKey($true)
                if ($key.Key -eq [ConsoleKey]::Escape -or
                    $key.KeyChar -eq 'q' -or $key.KeyChar -eq 'Q' -or
                    $key.Key -eq [ConsoleKey]::Enter) { return }
            }
            Start-Sleep -Milliseconds 50
        }
    }
}

function Show-JobLog {
    param([PSCustomObject]$Entry)
    while ($true) {
        Sync-BackgroundJobs
        Enter-Render
        Write-Header "Job Log  |  $($Entry.Module.Name)"
        Write-Ln
        if (Test-Path $Entry.LogPath) {
            $lines = @(Get-Content $Entry.LogPath -ErrorAction SilentlyContinue)
            if ($lines.Count -gt 0) {
                foreach ($line in ($lines | Select-Object -Last 40)) { Write-C "  $line" DarkGray }
            } else {
                Write-C "  (no output yet)" DarkGray
            }
        } else {
            Write-C "  Log file not created yet..." DarkGray
        }
        Write-Ln
        if (-not $Entry.Collected) {
            $elapsed = Format-Elapsed ((Get-Date) - $Entry.Started)
            Write-C "  running  $elapsed" Cyan -NoNewLine
        } elseif ($Entry.Stopped) {
            Write-C "  stopped  $($Entry.Dur)" Yellow -NoNewLine
        } elseif ($Entry.Ok) {
            Write-C "  completed OK  $($Entry.Dur)" Green -NoNewLine
        } else {
            Write-C "  completed FAIL  $($Entry.Dur)" Red -NoNewLine
        }
        Write-C "     Q / Esc to go back   auto-refresh 1s" DarkGray
        Exit-Render
        $deadline = (Get-Date).AddSeconds(1)
        while ((Get-Date) -lt $deadline) {
            if ([Console]::KeyAvailable) {
                $key = [Console]::ReadKey($true)
                if ($key.Key -eq [ConsoleKey]::Escape -or
                    $key.KeyChar -eq 'q' -or $key.KeyChar -eq 'Q') { return }
            }
            Start-Sleep -Milliseconds 50
        }
    }
}

function Show-Jobs {
    while ($true) {
        Sync-BackgroundJobs
        Enter-Render
        Write-Header 'Background Jobs'
        Write-Ln
        if ($Script:BgJobs.Count -eq 0) {
            Write-C "  No background jobs this session." DarkGray
            Write-Ln
            Write-C "  Q / Esc to go back" DarkGray
        } else {
            Write-Sep 'Jobs'
            for ($i = 0; $i -lt $Script:BgJobs.Count; $i++) {
                $entry = $Script:BgJobs[$i]
                if (-not $entry.Collected) {
                    $elapsed = Format-Elapsed ((Get-Date) - $entry.Started)
                    Write-C ("  [{0}]  [ RUN ]  {1,-28}  running {2}" -f ($i+1), $entry.Module.Name, $elapsed) Cyan
                } else {
                    $b = Get-JobBadge $entry
                    Write-C ("  [{0}]  $($b.Tag)  {1,-28}  {2}" -f ($i+1), $entry.Module.Name, $entry.Dur) $b.Col
                }
            }
            Write-Ln
            if ($Script:JobQueue.Count -gt 0) {
                Write-Sep 'Queued'
                foreach ($pending in $Script:JobQueue) {
                    Write-C ("  [WAIT]  {0,-28}  waiting for free slot" -f $pending.Module.Name) Yellow
                }
                Write-Ln
            }
            Write-C "  [#]+Enter view log   K[#]+Enter stop job   Q / Esc back   auto-refresh 1s" DarkGray
        }
        Exit-Render
        $chars    = ''
        $deadline = (Get-Date).AddSeconds(1)
        while ((Get-Date) -lt $deadline) {
            if ([Console]::KeyAvailable) {
                $key = [Console]::ReadKey($true)
                if ($key.Key -eq [ConsoleKey]::Escape -or
                    (($key.KeyChar -eq 'q' -or $key.KeyChar -eq 'Q') -and $chars -eq '')) { return }
                if ($key.Key -eq [ConsoleKey]::Enter) {
                    if ($chars -match '^\d+$') {
                        $idx = [int]$chars - 1
                        if ($idx -ge 0 -and $idx -lt $Script:BgJobs.Count) { Show-JobLog $Script:BgJobs[$idx] }
                    } elseif ($chars -imatch '^K(\d+)$') {
                        $idx = [int]$Matches[1] - 1
                        if ($idx -ge 0 -and $idx -lt $Script:BgJobs.Count) {
                            $target = $Script:BgJobs[$idx]
                            if ($target.Collected) {
                                Write-C "  Job already finished." Yellow; Start-Sleep 1
                            } else {
                                $conf = (Read-Input "stop $($target.Module.Name) [Y/N]").Trim().ToUpper()
                                if ($conf -eq 'Y') { Stop-BackgroundJob $target; Sync-BackgroundJobs }
                            }
                        }
                    }
                    $chars = ''
                    break
                }
                if ($key.KeyChar -match '[0-9Kk]') {
                    $chars += $key.KeyChar
                    Write-Host $key.KeyChar -NoNewline -ForegroundColor White
                }
            }
            Start-Sleep -Milliseconds 50
        }
    }
}

function Show-Summary {
    if ($Script:Sess.Results.Count -eq 0) { return }
    $last   = @(Get-LastResults)
    $dur    = Format-Elapsed ((Get-Date) - $Script:Sess.Start)
    $passed = @($last | Where-Object { $_.Ok }).Count
    $failed = @($last | Where-Object { -not $_.Ok }).Count
    $total  = $last.Count
    Write-Ln
    Write-Sep 'Session'
    foreach ($r in $last) {
        $tag = if ($r.Ok) { '[ OK ]' } else { '[FAIL]' }
        $col = if ($r.Ok) { 'Green' } else { 'Red' }
        Write-C ("  {0}  {1,-28}  {2,-14}  {3,6}" -f $tag, $r.Name, $r.Type, $r.Dur) $col
    }
    Write-Sep
    if ($failed -gt 0) {
        Write-C "  $passed / $total passed  |  $failed FAILED  |  $dur" Red
    } else {
        Write-C "  all $total passed  |  $dur" Green
    }
    Save-State
    Add-Log "SUMMARY passed=$passed failed=$failed dur=$dur"
}

function Show-Settings {
    Write-Ln
    Write-Sep 'Verbosity'
    Write-C "   0   quiet    suppress all dotnet output" White
    Write-C "   1   minimal  failures + final summary  (default)" White
    Write-C "   2   normal   all test names" White
    Write-C "   3   detailed full diagnostic output" White
    Write-Ln
    $v = (Read-Input "verbosity [0-3, Enter = keep $Script:Verbosity]>").Trim()
    if ($v -match '^[0-3]$') {
        $Script:Verbosity = [int]$v
        Write-C "  Verbosity: $(Get-VerbosityLabel)" Cyan
    }
    Write-Ln
    Write-Sep 'Max parallel background jobs'
    Write-C "   1   single - sequential queue" White
    Write-C "   2   dual   (default)" White
    Write-C "   3-8 more   (watch CPU)" White
    Write-Ln
    $p = (Read-Input "max parallel [1-8, Enter = keep $Script:MaxParallel]>").Trim()
    if ($p -match '^[1-8]$') {
        $Script:MaxParallel = [int]$p
        Write-C "  Max parallel: $Script:MaxParallel" Cyan
    }
    Export-Config
    Start-Sleep 1
}

function Show-Help {
    Enter-Render
    Write-Header 'Help'
    Write-Ln
    Write-Sep 'Suites and modules'
    Write-C "   A / U / I / E / L  All / Unit / Integration / E2E / Load" White
    Write-C "   L  Load runs the performance suite against the running stack" DarkGray
    Write-C "   123            combine single digits e.g. 123 = modules 1,2,3" White
    Write-C "   1 10 2         space-separated for IDs >= 10" White
    Write-C "   &<cmd>           run in background e.g. &2  &U  &21" White
    Write-Ln
    Write-Sep 'Tools'
    Write-C "   C   coverage report            O   open reports panel (cov / E2E)" White
    Write-C "   V   stats and metrics           D   Docker / Stack panel" White
    Write-C "   K   clean TestResults + report  W   watch mode (unit modules)" White
    Write-C "   R   re-run failed               J   background job status" White
    Write-C "   N   discover test projects" White
    Write-Ln
    Write-Sep 'Interrupts'
    Write-C "  ESC or CTRL+C during a test run stops the process cleanly." White
    Write-C "  You are prompted whether to record the run as a failure." White
    Write-C "  CTRL+C at the menu: first press warns; second press exits TestR." White
    Write-Ln
    Write-Sep 'Flags'
    Write-C "   B   --no-build  (skip MSBuild before running)" White
    Write-C "   F   xUnit name filter  (class, method, or namespace.class)" White
    Write-C "   X   output verbosity   (quiet / minimal / normal / detailed)" White
    Write-Ln
    Write-Sep 'Background runs  (&)'
    Write-C "  Prefix any suite or module with & to run non-blocking." White
    Write-C "  Returns immediately to menu. Output saved to TestResults\<name>-bg.log." White
    Write-C "  Check status: [J].  Coverage [C] must be run after jobs finish." White
    Write-Ln
    Write-Sep 'CI flags'
    Write-C "  -CI                    Unit + coverage, exit 0/1" White
    Write-C "  -CI -Suite All         Unit + Integration + E2E + coverage" White
    Write-C "  -CI -Suite Integration" White
    Write-C "  -CI -Suite E2E" White
    Write-C "  -CI -Suite Load        Load tests vs running stack, exit 0/1" White
    Write-Ln
    Write-Sep "Log  |  $Script:LogFile" DarkGray
    Write-Ln
    Exit-Render
    Wait-AnyKey
}

function Show-Menu {
    Sync-BackgroundJobs
    $banner  = Get-SessionBanner
    $bgCount = @($Script:BgJobs | Where-Object { -not $_.Collected }).Count
    Enter-Render
    Write-Header
    if ($null -ne $banner) {
        $col = if ($Script:Sess.AnyFailed) { 'Red' } else { 'Green' }
        Write-C "  $banner" $col
    }
    if ($bgCount -gt 0) {
        Write-C "  $bgCount job(s) running in background  [J] to check" Yellow
    }
    Write-Ln
    Write-Sep 'Suites'
    Write-C "   A   All" White
    Write-C "   U   Unit" White
    Write-C "   I   Integration                                      [Docker]" White
    Write-C "   E   E2E                                          [Playwright]" White
    Write-C "   L   Load                                              [Stack]" White
    Write-Ln
    Write-Sep 'Modules'
    foreach ($m in $Script:Modules) {
        $lastRun  = @($Script:Sess.Results | Where-Object { $_.Short -eq $m.Short }) | Select-Object -Last 1
        $fromPrev = $false
        if ($null -eq $lastRun -and $Script:PrevResults.ContainsKey($m.Short)) {
            $lastRun  = $Script:PrevResults[$m.Short]
            $fromPrev = $true
        }
        $badge = if ($null -eq $lastRun) { '[ -- ]' } elseif ($lastRun.Ok) { '[ OK ]' } else { '[FAIL]' }
        $col   = if ($null -eq $lastRun) { 'DarkGray' } `
                 elseif ($lastRun.Ok  -and -not $fromPrev) { 'Green' } `
                 elseif ($lastRun.Ok)                      { 'DarkGreen' } `
                 elseif (-not $fromPrev)                   { 'Red' } `
                 else                                      { 'DarkRed' }
        $tag   = if ($m.Type -eq 'Integration') { '[Docker]' } else { '' }
        Write-C ("   {0,-2}  {1}  {2,-28}  {3,-14}  {4}" -f $m.Id, $badge, $m.Name, $m.Type, $tag) $col
    }
    Write-Ln
    Write-Sep 'Tools'
    Write-C "   C   Coverage          V   Stats            D   Docker / Stack" White
    Write-C "   W   Watch             K   Clean            O   Reports" White
    Write-C "   B   --no-build        F   Filter           X   Verbosity" White
    Write-C "   R   Re-run            J   Jobs             N   Discover" White
    Write-C "   ?   Help                                   Q   Quit" White
    Write-Ln
    $nb  = if ($Script:NoBuild) { 'ON' } else { 'OFF' }
    $flt = if ($Script:Filter) { if ($Script:Filter.Length -gt 24) { $Script:Filter.Substring(0, 21) + '...' } else { $Script:Filter } } else { 'none' }
    Write-C "  --no-build $nb  |  verbosity $(Get-VerbosityLabel)  |  parallel $Script:MaxParallel  |  filter $flt" DarkGray
    Write-C "  $('-' * $Script:UiWidth)" DarkGray
    Exit-Render
}

$Script:Assemblies      = Read-AssembliesData
$Script:AllAsmFilter    = ($Script:Assemblies | ForEach-Object { $_.Filter }) -join ';'
$Script:Modules         = Read-ModulesData
$Script:ComposeServices = Read-ComposeServices
Import-Config
$Script:MaxModuleId  = if ($Script:Modules.Count -gt 0) { ($Script:Modules | Measure-Object -Property Id -Maximum).Maximum } else { 9 }
$Script:E2EReportDir = Join-Path (Split-Path -Parent $Script:ReportDir) 'E2EReports'
Restore-State

try {
    Add-Type -Name 'Kernel32' -Namespace '' -MemberDefinition @'
        [DllImport("kernel32.dll")] public static extern IntPtr GetStdHandle(int n);
        [DllImport("kernel32.dll")] public static extern bool GetConsoleMode(IntPtr h, out uint m);
        [DllImport("kernel32.dll")] public static extern bool SetConsoleMode(IntPtr h, uint m);
'@
    $h = [Kernel32]::GetStdHandle(-11); $m = 0
    [Kernel32]::GetConsoleMode($h, [ref]$m) | Out-Null
    [Kernel32]::SetConsoleMode($h, $m -bor 4) | Out-Null
} catch {}

if ($CI) {
    Add-Log 'CI run start'
    switch ($Suite) {
        'Unit'        { Invoke-UnitSuite }
        'Integration' { Invoke-IntegrationSuite }
        'E2E'         { Invoke-E2ESuite }
        'Load'        { Invoke-LoadSuite }
        'All'         { Invoke-UnitSuite; Invoke-IntegrationSuite -WarnIfUnitFailed; Invoke-E2ESuite }
    }
    if ($Suite -ne 'E2E' -and $Suite -ne 'Load') { Invoke-ReportGenerator -AsmFilter $Script:AllAsmFilter }
    Show-Summary
    Add-Log 'CI run end'
    exit $(if ($Script:Sess.AnyFailed) { 1 } else { 0 })
}

if ((Test-Path $Script:LogFile) -and (Get-Item $Script:LogFile).Length -gt 2MB) {
    Move-Item $Script:LogFile "$Script:LogFile.1" -Force
}
Add-Log 'Session start'
cls

while ($true) {
    Show-Menu
    $raw = Read-MenuInput
    if ($null -eq $raw) { continue }
    $raw = $raw.Trim()
    $bg  = $raw.StartsWith('&')
    $inp = if ($bg) { $raw.Substring(1).Trim().ToUpper() } else { $raw.ToUpper() }

    if ($inp -eq 'Q') {
        Write-Ln
        Write-Sep 'Quit'
        $banner = Get-SessionBanner
        if ($banner) {
            $col = if ($Script:Sess.AnyFailed) { 'Red' } else { 'Green' }
            Write-C "  $banner" $col
        }
        $liveJobs = @($Script:BgJobs | Where-Object { -not $_.Collected }).Count
        if ($liveJobs -gt 0) {
            Write-C "  [WARN]  $liveJobs background job(s) will be killed." Yellow
        }
        Write-Ln
        $c = (Read-Input 'quit [Y/N]').Trim().ToUpper()
        if ($c -eq 'Y') { break }
        continue
    }
    if     ($inp -eq '?') { Show-Help; continue }
    elseif ($inp -eq 'B') {
        $Script:NoBuild = -not $Script:NoBuild
        Write-C "  --no-build: $(if ($Script:NoBuild) { 'ON' } else { 'OFF' })" Cyan
        Export-Config; Start-Sleep 1; continue
    }
    elseif ($inp -eq 'X') { Show-Settings; continue }
    elseif ($inp -eq 'F') {
        Write-Ln
        Write-Sep 'xUnit filter'
        Write-C "  Filters test names passed to dotnet test --filter." DarkGray
        Write-C "  Examples:" DarkGray
        Write-C "    MyClass                    match class name" DarkGray
        Write-C "    MyClass.MyMethod           match specific test" DarkGray
        Write-C "    MyClass|OtherClass         OR - run both" DarkGray
        Write-C "    Category=fast&MyClass      AND - fast tests in MyClass" DarkGray
        Write-C "  Enter empty to clear." DarkGray
        Write-Ln
        $f = (Read-Input 'filter>').Trim()
        $Script:Filter = if ($f -ne '') { $f } else { $null }
        Export-Config; continue
    }
    elseif ($inp -eq 'O') { Show-ReportsPanel; continue }
    elseif ($inp -eq 'D') { Show-DockerPanel; continue }
    elseif ($inp -eq 'V') { Show-Stats; continue }
    elseif ($inp -eq 'J') { Show-Jobs; continue }
    elseif ($inp -eq 'W') { Invoke-WatchMode }
    elseif ($inp -eq 'C') { Invoke-Coverage }
    elseif ($inp -eq 'K') { Invoke-Cleanup; continue }
    elseif ($inp -eq 'N') { Invoke-Discover }
    elseif ($inp -eq 'R') { Invoke-RerunFailed -Background:$bg }
    elseif ($inp -eq 'A') {
        Invoke-UnitSuite -Background:$bg
        Invoke-IntegrationSuite -WarnIfUnitFailed -Background:$bg
        Invoke-E2ESuite -Background:$bg
        if (-not $bg) { Invoke-Coverage }
    }
    elseif ($inp -eq 'U') { Invoke-UnitSuite -Background:$bg }
    elseif ($inp -eq 'I') { Invoke-IntegrationSuite -Background:$bg }
    elseif ($inp -eq 'E') { Invoke-E2ESuite -Background:$bg }
    elseif ($inp -eq 'L') { Invoke-LoadSuite -Background:$bg }
    elseif ($inp -match '^[\d ]+$') {
        $tokens  = if ($Script:MaxModuleId -gt 9 -or $inp -match ' ') {
            @($inp.Trim() -split '\s+' | Where-Object { $_ -ne '' } | ForEach-Object { [int]$_ })
        } else {
            @($inp.ToCharArray() | ForEach-Object { [int]$_.ToString() })
        }
        $seen    = [System.Collections.Generic.HashSet[int]]::new()
        $ordered = [System.Collections.Generic.List[int]]::new()
        foreach ($id in $tokens) { if ($seen.Add($id)) { $ordered.Add($id) } }
        $valid = @($ordered | Where-Object { $oid = $_; $Script:Modules | Where-Object { $_.Id -eq $oid } })
        if ($valid.Count -eq 0) {
            Write-C "  Unknown: '$inp' - ? for help." Yellow; Start-Sleep 1; continue
        }
        $needsDocker = $false
        foreach ($id in $ordered) {
            $m = $Script:Modules | Where-Object { $_.Id -eq $id }
            if ($m -and $m.Type -eq 'Integration') { $needsDocker = $true }
        }
        if ($needsDocker -and -not (Test-Docker)) {
            Write-C "  [FAIL]  Docker not running.  Use [D] to manage Docker." Red
            Start-Sleep 2; continue
        }
        foreach ($id in $ordered) {
            $m = $Script:Modules | Where-Object { $_.Id -eq $id }
            if ($m) { Invoke-Suite $m -Background:$bg }
        }
    }
    elseif ($inp -eq '') { continue }
    else { Write-C "  Unknown: '$inp' - ? for help." Yellow; Start-Sleep 1; continue }

    if (-not $bg) {
        Show-Summary
        Write-Ln
        Wait-AnyKey
    }
}

foreach ($entry in $Script:BgJobs) {
    if (-not $entry.Collected) {
        try { Stop-Job  -Job $entry.Job -ErrorAction SilentlyContinue } catch {}
        try { Remove-Job -Job $entry.Job -Force -ErrorAction SilentlyContinue } catch {}
        Add-Log "KILLED $($entry.Module.Short) on exit"
    }
}
Export-Config
Add-Log 'Session end'
exit $(if ($Script:Sess.AnyFailed) { 1 } else { 0 })
