FROM mcr.microsoft.com/dotnet/core/sdk:3.0 as build

COPY src/ C:/src/

WORKDIR C:/src/JobExecutor
RUN dotnet publish -c Release -r win-x64

FROM mcr.microsoft.com/dotnet/core/runtime:3.0.1-nanoserver-1903

COPY --chown=ContainerAdministrator:ContainerAdministrator docker/helium-engine/windows/helium/ C:/helium/
COPY --from=build --chown=ContainerAdministrator:ContainerAdministrator C:/src/JobExecutor/bin/Release/netcoreapp3.0/win-x64/publish/ C:/helium/job-executor

