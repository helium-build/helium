


sudo docker run --rm -it \
	-e HELIUM_CI_SERVER_HOST=192.168.40.116 \
	-e HELIUM_CI_SERVER_PORT=6000 \
	-e HELIUM_CI_AGENT_KEY=1F1E9E1F27BD512ED5E7FE1DBDF643D4EC9FD6A6A0B5CB71C1EB9C7B63FE1089 \
	-e HELIUM_CI_AGENT_MAX_JOBS=2 \
	-v /var/run/docker.sock:/var/run/docker.sock \
	-v "$(pwd)/conf:/helium/conf:ro" \
	-v "$(pwd)/cache:/helium/cache" \
	-v "$(pwd)/workspaces/:/workspaces/" \
	helium-build/agent
