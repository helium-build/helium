FROM mcr.microsoft.com/dotnet/core/sdk:3.0 as build-dotnet

COPY docker/helium-engine/windows/UnixToLocalhost/ C:/UnixToLocalhost/

WORKDIR C:/UnixToLocalhost/
RUN dotnet publish -c Release -r win-x64 --self-contained true

FROM mcr.microsoft.com/powershell:6.2.3-windowsservercore-1903 as build-jdk

SHELL ["pwsh.exe", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]
USER ContainerAdministrator
RUN net user helium /add

RUN Invoke-WebRequest -Uri "https://github.com/AdoptOpenJDK/openjdk11-binaries/releases/download/jdk-11.0.4%2B11/OpenJDK11U-jdk_x64_windows_hotspot_11.0.4_11.zip" -OutFile "OpenJDK11U-jdk_x64_windows_hotspot_11.0.4_11.zip"
RUN Expand-Archive -Path "OpenJDK11U-jdk_x64_windows_hotspot_11.0.4_11.zip" -DestinationPath 'C:\'

RUN Invoke-WebRequest -Uri "https://piccolo.link/sbt-1.3.3.zip" -OutFile "sbt-1.3.3.zip"
RUN Expand-Archive -Path "sbt-1.3.3.zip" -DestinationPath 'C:\'
RUN setx /M PATH \"C:\sbt\bin;C:\jdk-11.0.4+11\bin;$env:Path\"
RUN echo $env:Path

WORKDIR C:/helium-src
USER helium
COPY --chown=helium:helium src C:/helium-src/src/
COPY --chown=helium:helium build.sbt C:/helium-src/build.sbt
COPY --chown=helium:helium project C:/helium-src/project/

SHELL ["cmd", "/S", "/C"]
# For some reason, sbt only looks for a dependency locally on the first run.
RUN sbt assembly || sbt assembly

FROM mcr.microsoft.com/powershell:6.2.3-nanoserver-1903

USER ContainerAdministrator
RUN net user helium /add

COPY --chown=ContainerAdministrator:ContainerAdministrator docker/helium-engine/windows/helium/ C:/helium/
COPY --from=build-dotnet --chown=ContainerAdministrator:ContainerAdministrator C:/UnixToLocalhost/bin/Release/netcoreapp3.0/win-x64/publish/ C:/helium/UnixToLocalhost/
COPY --from=build-jdk --chown=ContainerAdministrator:ContainerAdministrator C:/helium-src/target/scala-2.13/Helium-assembly-0.1.0-SNAPSHOT.jar C:/helium/lib/helium.jar

USER helium
RUN mkdir C:\\sdk

WORKDIR C:/work/
