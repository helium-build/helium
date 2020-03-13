namespace netstd Helium.CI.Common.Protocol

exception InvalidState {}
exception UnknownOutput {}

struct Version {
    1: i32 protocolVersion,
    2: string heliumVersion,
}

struct BuildStatus {
    1: string output,
}

enum OutputType {
    ARTIFACT = 1,
    REPLAY = 2,
}

service BuildAgent {
    bool authenticate(1: string serverKey) throws (1: InvalidState error),
    bool supportsPlatform(1: string platform) throws (1: InvalidState error)
    void sendWorkspace(1: binary chunk) throws (1: InvalidState error),
    void startBuild(1: string task) throws (1: InvalidState error),
    BuildStatus getStatus() throws (1: InvalidState error),
    i32 getExitCode() throws (1: InvalidState error),
    
    list<string> artifacts() throws (1: InvalidState error),
    void openOutput(1: OutputType type, 2: string name) throws (1: InvalidState error, 2: UnknownOutput unknownOutput),
    binary readOutput() throws (1: InvalidState error),
}
