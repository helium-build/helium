sudo docker run --rm -p 8081:8080 -v /var/run/docker.sock:/var/run/docker.sock -v "$(pwd)/cache:/helium/cache" -v "$(pwd)/conf:/helium/conf" -v "$(pwd)/workspaces/:/workspaces/" helium-build/agent
