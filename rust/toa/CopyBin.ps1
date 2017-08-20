Param (
    [switch]$Release
)

Copy-Item `
    (Join-Path $PSScriptRoot 'target\i686-pc-windows-msvc' |
        Join-Path -ChildPath $(if ($Release) { 'release' } else { 'debug' }) |
        Join-Path -ChildPath 'toa.exe') `
    (Join-Path $PSScriptRoot '..\..\docker\toa\')
