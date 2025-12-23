#!/bin/bash
#
# .Synopsis Add a migration for multiple database providers
# .Notes
#     From the project root, pass in this context and migration name.
#     If you only have a single context, assign it to the parameter.
# .Example
#     ./Data/migrations-add.ps1 TestMigr ClientDb
#

if [ "$#" -ne 2 ]; then
    echo "usage: $0 migration-name db-context"
    exit 1
fi

context=${2%Context}
name=$1
declare -a providers=("SqlServer" "PostgreSQL")

for provider in "${providers[@]}"; do
    export Database__Provider=$provider
    echo $provider $name $context
    dotnet ef migrations add $name --context ${context}Context${provider} -o ./Data/Migrations/$provider/${context} --project /mnt/data/crucible/topomojo/topomojo/src/TopoMojo.Api
    wait
done

export Database__Provider=
