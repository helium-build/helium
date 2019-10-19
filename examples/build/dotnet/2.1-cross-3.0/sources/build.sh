
dotnet run --framework netcoreapp2.1
dotnet run --framework netcoreapp3.0
dotnet pack
dotnet nuget push bin/Debug/*.nupkg
