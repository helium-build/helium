#!/bin/bash -x

mkdir -p helium-build-env/linux/helium/install/root
mkdir -p helium-build-env/linux/helium/install/home

docker build -t helium-build/build-env:debian-buster-20190708 -f helium-build-env/linux/Dockerfile ../

cpp -traditional-cpp -undef -o helium-engine/linux/Dockerfile.gen helium-engine/linux/Dockerfile.in
docker build -t helium-build/engine -f helium-engine/linux/Dockerfile.gen ../
