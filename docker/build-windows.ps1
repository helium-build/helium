
docker build --isolation process -t helium-build/build-env:windows-servercore-1903 -f .\helium-build-env\windows\Dockerfile ../
docker build --isolation process -t helium-build/job-executor -f .\helium-job-executor\windows\Dockerfile ../
