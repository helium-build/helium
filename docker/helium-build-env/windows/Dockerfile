FROM mcr.microsoft.com/dotnet/core/sdk:3.1 as build

COPY src/ C:/src/

WORKDIR C:/src/LocalhostToUnix/
RUN dotnet publish -c Release -r win-x64 --self-contained true

FROM mcr.microsoft.com/powershell:6.2.3-windowsservercore-1903

USER ContainerAdministrator
RUN net user helium /add

COPY --chown=helium:helium docker/helium-build-env/windows/helium/ C:/helium/
COPY --from=build --chown=helium:helium C:/src/LocalhostToUnix/bin/Release/netcoreapp3.0/win-x64/publish/ C:/helium/LocalhostToUnix/

USER helium
RUN mkdir C:\\sdk

WORKDIR C:/sources/
CMD ["cmd"]
ENTRYPOINT ["pwsh", "C:\\helium\\launch.ps1"]
