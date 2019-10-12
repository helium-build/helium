FROM openjdk:11.0.4-jdk-stretch as build

RUN apt-get update && apt-get install -y build-essential

RUN useradd -m helium && \
	mkdir -p /helium-src/ && \
	mkdir -p /docker-launcher/ && \
	mkdir -p /tools/ && \
	chown -R helium:helium /helium-src/ /docker-launcher/ /tools/

USER helium

WORKDIR /tools/
RUN wget https://piccolo.link/sbt-1.3.2.tgz && tar xf sbt-1.3.2.tgz

ENV PATH "/tools/sbt/bin:${PATH}"

WORKDIR /helium-src/
COPY --chown=helium:helium src /helium-src/src/
COPY --chown=helium:helium build.sbt /helium-src/build.sbt
COPY --chown=helium:helium project /helium-src/project/

RUN sbt assembly

WORKDIR /docker-launcher/
COPY --chown=helium:helium docker/helium-engine/linux/docker-launcher /docker-launcher/

RUN gcc --std=c99 -D_POSIX_C_SOURCE=200809L -o docker-launcher docker-launcher.c

FROM openjdk:11.0.4-jdk-stretch

RUN apt-get update && \
	apt-get install -y apt-transport-https ca-certificates curl gnupg2 software-properties-common && \
	curl -fsSL https://download.docker.com/linux/debian/gpg | apt-key add - && \
	add-apt-repository "deb [arch=amd64] https://download.docker.com/linux/debian $(lsb_release -cs) stable" && \
	apt-get update && \
	apt-get install -y docker-ce-cli socat && \
	useradd -m helium && \
	mkdir -p /helium/cache/ && \
	chown helium:helium /helium/cache/

COPY --chown=root:root docker/helium-engine/linux/helium/ /helium/
COPY --chown=root:root sdks /helium/sdks/
COPY --from=build --chown=root:root /helium-src/target/scala-2.13/Helium-assembly-0.1.0-SNAPSHOT.jar /helium/helium.jar
COPY --from=build --chown=root:root /docker-launcher/docker-launcher /helium/docker-launcher

RUN chmod u+s /helium/docker-launcher

USER helium
WORKDIR /helium/
ENTRYPOINT ["/helium/helium"]

