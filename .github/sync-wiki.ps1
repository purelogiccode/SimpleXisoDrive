<#
.SYNOPSIS
    Regenerates the GitHub wiki pages from docs/.
.DESCRIPTION
    docs/ is the single source of truth for both the GitHub wiki and the GitHub
    Pages site. This script mirrors every docs page into the wiki checkout and
    removes pages that no longer exist in docs/, so renames and deletions are
    reflected. index.md is the Pages home only; Home.md is the wiki home. The
    documentation already uses wiki-style links, so no link rewriting is needed.
.PARAMETER DocsDir
    Path to the docs/ folder in the main checkout.
.PARAMETER WikiDir
    Path to the root of the .wiki.git checkout.
#>
param(
    [Parameter(Mandatory = $true)][string]$DocsDir,
    [Parameter(Mandatory = $true)][string]$WikiDir
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $DocsDir -PathType Container)) {
    throw "Docs directory not found: $DocsDir"
}

if (-not (Test-Path -LiteralPath $WikiDir -PathType Container)) {
    throw "Wiki directory not found: $WikiDir"
}

# Start clean so renamed/removed pages disappear from the wiki too.
Get-ChildItem -LiteralPath $WikiDir -Filter '*.md' -File |
    Remove-Item -Force

# index.md is the Pages home; the wiki has its own Home.md.
Get-ChildItem -LiteralPath $DocsDir -Filter '*.md' -File |
    Where-Object { $_.Name -ne 'index.md' } |
    ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $WikiDir $_.Name) }

Write-Output 'Wiki pages regenerated:'
Get-ChildItem -LiteralPath $WikiDir -Filter '*.md' -File |
    Sort-Object Name |
    ForEach-Object { Write-Output "  $($_.Name)" }
