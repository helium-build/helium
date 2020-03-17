namespace netstd Helium.CI.Common.Protocol

exception InvalidState {}
exception UnknownOutput {}

struct Version {
    1: i32 protocolVersion,
    2: string heliumVersion,
}

struct BuildStatus {
    1: binary output,
}

enum OutputType {
    ARTIFACT = 1,
    REPLAY = 2,
}

struct BuildExitCode {
    1: optional i32 exitCode,
}

service BuildAgent {
    bool supportsPlatform(1: string platform),
    void sendWorkspace(1: binary chunk) throws (1: InvalidState error),
    void startBuild(1: string task) throws (1: InvalidState error),
    BuildStatus getStatus() throws (1: InvalidState error),
    BuildExitCode getExitCode() throws (1: InvalidState error),
    
    list<string> artifacts() throws (1: InvalidState error),
    void openOutput(1: OutputType type, 2: string name) throws (1: InvalidState error, 2: UnknownOutput unknownOutput),
    binary readOutput() throws (1: InvalidState error),
}
