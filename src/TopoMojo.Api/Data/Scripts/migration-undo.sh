#!/bin/bash
#
# .Synopsis Remove last migration for multiple database providers
# .Notes
#     From the project root, pass in the context.
#     If you just have a single context, assign it to the parameter
# .Example
#     ./Data/migrations-undo.ps1 ClientDb
#

if [ "$#" -ne 1 ]; then
    echo "usage: $0 db-context"
    exit 1
fi

context=${1%Context}
declare -a providers=("SqlServer" "PostgreSQL")

for provider in "${providers[@]}"; do
    export Database__Provider=$provider
    echo $provider $context
    dotnet ef migrations remove --context ${context}Context${provider} -f
    wait
done

export Database__Provider=
