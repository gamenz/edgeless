<#
.SYNOPSIS
    Project gate for docs and code: file size, line length, encoding, folder size,
    plan-link and anti-cheat checks. Rules live in scripts/gate.rules.json.
.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts/gate.ps1
.NOTES
    Exit code 0 = pass (warnings allowed), 1 = at least one failure.
    Compatible with Windows PowerShell 5.1 and PowerShell 7.
#>
[CmdletBinding()]
param(
    [string]$Root,
    [string]$RulesPath
)

Set-StrictMode -Version 3
$ErrorActionPreference = 'Stop'

# $PSScriptRoot is not available in param defaults on Windows PowerShell 5.1.
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $Root) { $Root = Split-Path -Parent $scriptDir }
if (-not $RulesPath) { $RulesPath = Join-Path $scriptDir 'gate.rules.json' }
$Root = (Resolve-Path -LiteralPath $Root).Path

$script:failures = New-Object System.Collections.Generic.List[string]
$script:warnings = New-Object System.Collections.Generic.List[string]
$script:regexCache = @{}

function Add-Failure([string]$check, [string]$path, [string]$message) {
    $script:failures.Add("FAIL [$check] ${path}: $message")
}

function Add-Warning([string]$check, [string]$path, [string]$message) {
    $script:warnings.Add("WARN [$check] ${path}: $message")
}

# Converts a glob (*, **, ?) on '/'-separated relative paths into an anchored regex.
function Convert-GlobToRegex([string]$glob) {
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.Append('^')
    $i = 0
    while ($i -lt $glob.Length) {
        $c = $glob[$i]
        if ($c -eq '*') {
            $isDouble = ($i + 1 -lt $glob.Length) -and ($glob[$i + 1] -eq '*')
            if ($isDouble -and ($i + 2 -lt $glob.Length) -and ($glob[$i + 2] -eq '/')) {
                [void]$sb.Append('(?:.*/)?'); $i += 3; continue
            }
            if ($isDouble) { [void]$sb.Append('.*'); $i += 2; continue }
            [void]$sb.Append('[^/]*')
        }
        elseif ($c -eq '?') { [void]$sb.Append('[^/]') }
        else { [void]$sb.Append([regex]::Escape([string]$c)) }
        $i++
    }
    [void]$sb.Append('$')
    return $sb.ToString()
}

function Test-Glob([string]$path, [object[]]$globs) {
    foreach ($glob in $globs) {
        if (-not $script:regexCache.ContainsKey($glob)) {
            $script:regexCache[$glob] = New-Object System.Text.RegularExpressions.Regex(
                (Convert-GlobToRegex $glob), 'IgnoreCase')
        }
        if ($script:regexCache[$glob].IsMatch($path)) { return $true }
    }
    return $false
}

# Walks the tree, skipping excluded directories without descending into them.
function Get-GateFiles([string]$dir, [string[]]$excludeDirs) {
    foreach ($entry in Get-ChildItem -LiteralPath $dir -Force) {
        if ($entry.PSIsContainer) {
            if ($excludeDirs -notcontains $entry.Name) { Get-GateFiles $entry.FullName $excludeDirs }
        }
        else { $entry }
    }
}

function Get-RelativePath([string]$fullPath) {
    return $fullPath.Substring($Root.TrimEnd('\', '/').Length + 1).Replace('\', '/')
}

function Get-Extension([System.IO.FileInfo]$file) {
    if ($file.Extension) { return $file.Extension.ToLowerInvariant() }
    return $file.Name.ToLowerInvariant()   # dotfiles like .gitignore
}

function Test-Encoding([string]$rel, [byte[]]$bytes) {
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        Add-Failure 'encoding' $rel 'UTF-8 BOM found (save as UTF-8 without BOM)'
    }
    if ([Array]::IndexOf($bytes, [byte]13) -ge 0) {
        Add-Failure 'encoding' $rel 'CR found (use LF line endings only)'
    }
}

function Test-Size([string]$rel, [string[]]$lines, $rules, $exceptions) {
    $rule = $null
    foreach ($candidate in $rules) {
        if (Test-Glob $rel $candidate.include) { $rule = $candidate; break }
    }
    if ($null -eq $rule) { return }

    $maxLines = [int]$rule.maxLines
    $exception = $exceptions | Where-Object { $_.path -eq $rel } | Select-Object -First 1
    if ($exception) { $maxLines = [int]$exception.maxLines }

    $count = $lines.Length
    if ($count -gt $maxLines) {
        Add-Failure $rule.name $rel "$count lines > max $maxLines (split by responsibility)"
    }
    elseif ($count -gt [int]$rule.warnLines -and -not $exception) {
        Add-Warning $rule.name $rel "$count lines > soft limit $($rule.warnLines)"
    }

    if ($rule.PSObject.Properties['maxLineLength']) {
        $limit = [int]$rule.maxLineLength
        for ($n = 0; $n -lt $count; $n++) {
            if ($lines[$n].Length -gt $limit) {
                Add-Failure $rule.name "${rel}:$($n + 1)" "line is $($lines[$n].Length) chars > $limit"
            }
        }
    }
}

function Test-PlanLinks([string]$rel, [string]$text, $planLinks) {
    if ($rel -like 'docs/plans/*') { return }
    foreach ($pattern in $planLinks.patterns) {
        $match = [regex]::Match($text, $pattern)
        if ($match.Success) { Add-Failure 'plan-link' $rel "links to a plan: '$($match.Value)'" }
    }
}

function Test-AntiCheatSource([string]$rel, [string]$text, $antiCheat) {
    if (-not (Test-Glob $rel $antiCheat.sourceInclude)) { return }
    foreach ($token in $antiCheat.forbiddenTokens) {
        if ($text.Contains($token)) { Add-Failure 'anti-cheat' $rel "forbidden token '$token' (CLAUDE.md section 1.1)" }
    }
    foreach ($restricted in $antiCheat.restrictedTokens) {
        if ($text.Contains($restricted.token) -and -not (Test-Glob $rel $restricted.allowedIn)) {
            Add-Failure 'anti-cheat' $rel "'$($restricted.token)' is only allowed in: $($restricted.allowedIn -join ', ')"
        }
    }
    if ($rel -eq $antiCheat.openProcessFile) {
        foreach ($right in [regex]::Matches($text, 'PROCESS_[A-Z_]+')) {
            if ($right.Value -ne $antiCheat.openProcessRequiredRight) {
                Add-Failure 'anti-cheat' $rel "process right '$($right.Value)' not allowed"
            }
        }
    }
}

function Test-NativeMethods([string]$rel, [string[]]$lines, $antiCheat) {
    if ($rel -ne $antiCheat.nativeMethodsFile) { return }
    $allowed = @($antiCheat.nativeMethodsForeignWhitelist) + @($antiCheat.nativeMethodsOwnWindowOnly)
    foreach ($raw in $lines) {
        $name = $raw.Trim()
        if ($name -eq '' -or $name.StartsWith('//')) { continue }
        if ($allowed -ccontains $name) { continue }
        $prefixOk = $false
        foreach ($prefix in $antiCheat.nativeMethodsAllowedPrefixes) {
            if ($name.StartsWith($prefix, [StringComparison]::Ordinal)) { $prefixOk = $true; break }
        }
        if (-not $prefixOk) {
            Add-Failure 'anti-cheat' $rel "'$name' is not allowlisted in gate.rules.json (needs user approval)"
        }
    }
}

function Test-Folders($files, $folderRule) {
    $groups = $files | ForEach-Object { Split-Path -Parent (Get-RelativePath $_.FullName) } |
        ForEach-Object { $_.Replace('\', '/') } | Group-Object
    foreach ($group in $groups) {
        if (-not (Test-Glob "$($group.Name)/x" $folderRule.include)) { continue }
        if ($group.Count -gt [int]$folderRule.maxFilesPerFolder) {
            Add-Failure 'folder' "$($group.Name)/" "$($group.Count) files > max $($folderRule.maxFilesPerFolder) (add a sub-folder)"
        }
    }
}

# ---- main ----
$rules = Get-Content -LiteralPath $RulesPath -Raw -Encoding UTF8 | ConvertFrom-Json
$utf8 = New-Object System.Text.UTF8Encoding($false)
$files = @(Get-GateFiles $Root @($rules.excludeDirs))
$checked = 0

foreach ($file in $files) {
    if ($rules.textExtensions -notcontains (Get-Extension $file)) { continue }
    $rel = Get-RelativePath $file.FullName
    $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
    $text = $utf8.GetString($bytes)
    $lines = if ($text.Length -eq 0) { @() } else { $text.TrimEnd("`n").Split("`n") }
    $checked++

    Test-Encoding $rel $bytes
    Test-Size $rel $lines $rules.sizeRules @($rules.exceptions)
    Test-PlanLinks $rel $text $rules.planLinks
    Test-AntiCheatSource $rel $text $rules.antiCheat
    Test-NativeMethods $rel $lines $rules.antiCheat
}
Test-Folders $files $rules.folderRule

$script:warnings | ForEach-Object { Write-Host $_ -ForegroundColor Yellow }
$script:failures | ForEach-Object { Write-Host $_ -ForegroundColor Red }
Write-Host ("Gate: {0} files checked, {1} failure(s), {2} warning(s)" -f $checked, $script:failures.Count, $script:warnings.Count)
if ($script:failures.Count -gt 0) { exit 1 }
exit 0
