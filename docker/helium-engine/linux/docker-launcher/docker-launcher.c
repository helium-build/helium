#include <stdio.h>
#include <string.h>
#include <stdbool.h>
#include <stdlib.h>

#include <regex.h>
#include <unistd.h>
#include <errno.h>

typedef struct {
	const char* local;
	const char* host;
} mount_mapping;

bool matches_regex(const char* str, const char* regex);

bool check_mount(char** mountSpec);

bool startsWith(const char* str, const char* prefix) {
	return strncmp(str, prefix, strlen(prefix)) == 0;
}

int main(int argc, char** argv) {

	char* dockerArgs[argc + 1];
	dockerArgs[0] = strdup("docker");

	size_t i = 1;
	if(i < argc && strcmp(argv[i], "run") == 0) {
		dockerArgs[i] = argv[i];
		++i;
	}
	else {
		goto invalid_arguments;
	}

	for(; i < argc; ++i) {
		dockerArgs[i] = argv[i];\
		if(strcmp(argv[i], "--rm") == 0) {}
		else if(strcmp(argv[i], "-it") == 0) {}
		else if(strcmp(argv[i], "--network") == 0) {
			++i;
			if(i < argc && strcmp(argv[i], "none") == 0) {
				dockerArgs[i] = argv[i];
			}
			else {
				fprintf(stderr, "Argument --network must have a value.\n");
				goto invalid_arguments;
			}
		}
		else if(strcmp(argv[i], "--hostname") == 0) {
			++i;
			if(i >= argc) {
				fprintf(stderr, "Argument --hostname must have a value.\n");
				goto invalid_arguments;
			}

			dockerArgs[i] = argv[i];
		}
		else if(strcmp(argv[i], "-e") == 0) {
			++i;
			if(i >= argc){
				fprintf(stderr, "Argument -e must have a value.\n");
				goto invalid_arguments;
			}
			dockerArgs[i] = argv[i];
		}
		else if(strcmp(argv[i], "-v") == 0) {
			++i;
			if(i >= argc) {
				fprintf(stderr, "Argument -v must have a value.\n");
				goto invalid_arguments;
			}

			dockerArgs[i] = argv[i];
			if(!check_mount(&dockerArgs[i])) { goto invalid_arguments; }
		}
		else if(matches_regex(argv[i], "^helium-build/build-env\\:[a-z0-9\\-]+$")) {
			break;
		}
		else {
			fprintf(stderr, "Unknown argument %s.\n", argv[i]);
			goto invalid_arguments;
		}
	}

	if(i == argc) {
		fprintf(stderr, "Docker image not specified.\n");
		return 1;
	}

	for(; i < argc; ++i) {
		dockerArgs[i] = argv[i];
	}

	dockerArgs[argc] = NULL;

	execv("/usr/bin/docker", dockerArgs);

	int errorNum = errno;
	fprintf(stderr, "Could not execute docker: %s\n", strerror(errorNum));
	return 1;

invalid_arguments:
	fprintf(stderr, "Invalid Docker Arguments\n");
	return 1;

}

bool updatePrefix(char** path, const char* prefix, const char* replacementFile) {

	if(!startsWith(*path, prefix)) {
		return false;
	}

	FILE *f = fopen(replacementFile, "rb");
	if(f == NULL) {
		fprintf(stderr, "Could not open file.\n");
		return false;
	}
	if(fseek(f, 0, SEEK_END)) {
		fprintf(stderr, "Could not seek 1.\n");
		goto fail;
	}
	long fileSize = ftell(f);
	if(fileSize == -1L) {
		fprintf(stderr, "Could not get length.\n");
		goto fail;
	}
	if(fseek(f, 0, SEEK_SET)) {
		fprintf(stderr, "Could not seek 2.\n");
		goto fail;
	}

	char* newPath = malloc(fileSize + strlen(*path) - strlen(prefix) + 1);
	size_t bytesRead = fread(newPath, 1, fileSize, f);
	if(ferror(f)) {
		fprintf(stderr, "Could not read file.\n");
		goto fail;
	}
	fclose(f);

	newPath[bytesRead] = '\0';
	strcpy(newPath + bytesRead, *path + strlen(prefix));
	*path = newPath;

	return true;

fail:
	fclose(f);
	return false;
}

bool matches_regex(const char* str, const char* regexStr) {
	regmatch_t matches[1];

	regex_t regex;
	int res = regcomp(&regex, regexStr, REG_EXTENDED);
	if(res != 0) {
		fprintf(stderr, "Invalid mount specification.\n");
		return false;
	}

	res = regexec(&regex, str, 1, matches, 0);
	regfree(&regex);

	if(res == REG_NOMATCH) {
		return false;
	}
	else if(res != 0) {
		char error[1024];
		regerror(res, &regex, error, sizeof(error));
		fprintf(stderr, "Regex match failed: %s\n", error);
		return false;
	}
	else {
		return true;
	}
}

bool check_mount(char** mountSpec) {

	if(!matches_regex(*mountSpec, "^[^\\:]+\\:[^\\:]+(\\:ro)?$")) {
		fprintf(stderr, "Mount spec %s did not pass regular expression\n", *mountSpec);
		return false;
	}
	else if(updatePrefix(mountSpec, "/workspace/", "/helium/realpaths/workspace")) {
		return true;
	}
	else if(updatePrefix(mountSpec, "/helium/cache/", "/helium/realpaths/cache")) {
		return true;
	}
	else {
		fprintf(stderr, "Mount spec did not begin with correct prefix.\n");
		return false;
	}
}

