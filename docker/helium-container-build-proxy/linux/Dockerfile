FROM mcr.microsoft.com/dotnet/core/sdk:3.1 as build

COPY src/ /src/

WORKDIR /src/Engine.ContainerBuildProxy/
RUN dotnet publish -c Release -r linux-x64 --self-contained true

FROM debian:buster-20190708

RUN apt-get update && apt-get install -y libicu63 libssl1.1

RUN useradd -m helium

COPY --chown=helium:helium docker/helium-build-env/linux/helium /helium/
COPY --from=build --chown=helium:helium /src/Engine.ContainerBuildProxy/bin/Release/netcoreapp3.0/linux-x64/publish/ /helium/ContainerBuildProxy/

USER helium

WORKDIR /home/helium

CMD ["/helium/ContainerBuildProxy/Helium.Engine.ContainerBuildProxy"]
