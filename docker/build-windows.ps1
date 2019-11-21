
docker build --isolation process -t helium-build/build-env:windows-servercore-1903 -f .\helium-build-env\windows\Dockerfile ../
docker build --isolation process -t helium-build/build-engine -f .\helium-engine\windows\Dockerfile ../
