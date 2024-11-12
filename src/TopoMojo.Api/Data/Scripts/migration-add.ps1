<# Copyright 2025 Carnegie Mellon University. #>
<# Released under a 3 Clause BSD-style license. See LICENSE.md in the project root. #>

<#
.Synopsis Add a migration for multiple database providers
.Notes
    From the project root, pass in this context and migration name.
    If you only have a single context, assign it to the parameter.
.Example
    ./Data/migrations-add.ps1 ClientDb TestMigr
#>
Param(
    [Parameter(Mandatory = $true)]
    $context,
    [Parameter(Mandatory = $true)]
    $name
)

$providers = @('SqlServer', 'PostgreSQL')
foreach ($provider in $providers) {
    $env:Database:Provider=$provider
    write-host $provider $name $context
    dotnet ef migrations add $name --context "$($context)Context$($provider)" -o ./Data/Migrations/$provider/$context
}

$env:Database:Provider=''
