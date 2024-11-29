# migrate.ps1
param(
    [string]$migrationName = "Initial"
)

Write-Host "Creating migration '$migrationName'..."

# Remove existing migrations if needed
dotnet ef migrations remove --force --project SNIF.Infrastructure --startup-project SNIF.API

# Add new migration
dotnet ef migrations add $migrationName --project SNIF.Infrastructure --startup-project SNIF.API

# Update database
dotnet ef database update --project SNIF.Infrastructure --startup-project SNIF.API