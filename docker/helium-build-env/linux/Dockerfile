FROM mcr.microsoft.com/dotnet/core/sdk:3.1 as build

COPY src/ /src/

WORKDIR /src/LocalhostToUnix/
RUN dotnet publish -c Release -r linux-x64 --self-contained true

FROM debian:buster-20190708

RUN apt-get update && apt-get install -y libicu63 libssl1.1 wget curl lsof

RUN useradd -m helium

COPY --chown=helium:helium docker/helium-build-env/linux/helium /helium/
COPY --from=build --chown=helium:helium /src/LocalhostToUnix/bin/Release/netcoreapp3.0/linux-x64/publish/ /helium/LocalhostToUnix/

RUN mkdir /sdk/ && chown -R helium:helium /sdk/ /helium/

USER helium

WORKDIR /sources

ENTRYPOINT ["/helium/launch.sh"]
