param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

dotnet --info
dotnet restore BankSwitch.sln
dotnet build BankSwitch.sln -c $Configuration --no-restore
dotnet test BankSwitch.sln -c $Configuration --no-build
