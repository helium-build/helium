FROM mcr.microsoft.com/dotnet/core/sdk:3.1 as build

COPY src/ C:/src/

WORKDIR C:/src/Engine
RUN dotnet publish -c Release -r win-x64 --self-contained true

FROM mcr.microsoft.com/powershell:6.2.3-nanoserver-1903

USER ContainerAdministrator
RUN net user helium /add

COPY --chown=ContainerAdministrator:ContainerAdministrator docker/helium-engine/windows/helium/ C:/helium/
COPY --from=build --chown=ContainerAdministrator:ContainerAdministrator C:/src/Engine/bin/Release/netcoreapp3.0/win-x64/publish/ C:/helium/lib/

ENV HELIUM_BASE_DIR C:/helium
ENV HELIUM_CONTENT_ROOT C:/helium/lib

USER helium
RUN mkdir C:\\sdk

WORKDIR C:/work/
