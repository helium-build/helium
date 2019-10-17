FROM mcr.microsoft.com/dotnet/core/sdk:3.0 as build-dotnet

COPY docker/helium-engine/windows/UnixToLocalhost/ C:/UnixToLocalhost/

WORKDIR C:/UnixToLocalhost/
RUN dotnet publish -c Release -r win-x64 --self-contained true

FROM mcr.microsoft.com/powershell:6.2.3-nanoserver-1903

USER ContainerAdministrator
RUN net user helium /add

COPY --chown=ContainerAdministrator:ContainerAdministrator docker/helium-engine/windows/helium/ C:/helium/
COPY --from=build-dotnet --chown=ContainerAdministrator:ContainerAdministrator C:/UnixToLocalhost/bin/Release/netcoreapp3.0/win-x64/publish/ C:/helium/UnixToLocalhost/

USER helium
RUN mkdir C:\\sdk

WORKDIR C:/work/
