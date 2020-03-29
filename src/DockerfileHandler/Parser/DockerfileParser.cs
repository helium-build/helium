using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Helium.DockerfileHandler.Commands;

namespace Helium.DockerfileHandler.Parser
{
    // https://github.com/moby/buildkit/blob/c60a1eb215d795a12e43ceff6a5ed67ce1ad958d/frontend/dockerfile/parser/parser.go
    public static class DockerfileParser
    {
        
        private static readonly Regex tokenEscapeRegex = new Regex(@"^#[ \t]*escape[ \t]*=[ \t]*(?<escapechar>.).*$");
        internal static readonly char[] whitespaceChars = new char[] { '\t', '\v', '\f', '\r', ' ' };

        public static async Task<DockerfileInfo> Parse(TextReader reader, IReadOnlyDictionary<string, string> buildArgs) {
            var unconsumedBuildArgs = new HashSet<string>(buildArgs.Keys);
            var commands = await ReadCommands(reader, unconsumedBuildArgs, buildArgs).ToListAsync();
            
            return new DockerfileInfo(
                unconsumedBuildArgs,
                commands
            );
        }

        private sealed class CurrentBuild
        {
            public CurrentBuild(FromCommand fromCommand, Dictionary<string, string> buildArgs, List<CommandBase> commands) {
                FromCommand = fromCommand;
                BuildArgs = buildArgs;
                Commands = commands;
            }

            public FromCommand FromCommand { get; }
            public Dictionary<string, string> BuildArgs { get; }
            public List<CommandBase> Commands { get; }

            public DockerfileBuild ToDockerfileBuild() =>
                new DockerfileBuild(FromCommand, Commands);
        }

        private static async IAsyncEnumerable<DockerfileBuild> ReadCommands(TextReader reader, HashSet<string> unconsumedBuildArgs, IReadOnlyDictionary<string, string> buildArgs) {
            var multiBuildArgs = new Dictionary<string, string>();
            CurrentBuild? currentBuild = null;
            
            var parseOptions = new ParseOptions(multiBuildArgs);

            await foreach(var line in ReadLines(reader, parseOptions)) {
                var (cmd, flags, args) = SplitCommand(line);

                if(cmd == "from") {
                    if(currentBuild != null) {
                        yield return currentBuild.ToDockerfileBuild();
                    }
                    
                    var fromCommand = CommandParsers.From(args, flags, parseOptions);
                    currentBuild = new CurrentBuild(
                        fromCommand: fromCommand,
                        buildArgs: new Dictionary<string, string>(),
                        commands: new List<CommandBase>()
                    );
                }
                else if(cmd == "arg") {
                    var argCommand = CommandParsers.Arg(args, flags, parseOptions);
                    unconsumedBuildArgs.Remove(argCommand.Name);
                    
                    if(currentBuild == null) {
                        string? value;

                        if(!buildArgs.TryGetValue(argCommand.Name, out value)) {
                            value = argCommand.DefaultValue;
                        }

                        if(value == null) {
                            multiBuildArgs.Remove(argCommand.Name);
                        }
                        else {
                            multiBuildArgs[argCommand.Name] = value;
                        }
                    }
                    else {
                        string? value;
                        
                        if(!buildArgs.TryGetValue(argCommand.Name, out value) && !multiBuildArgs.TryGetValue(argCommand.Name, out value)) {
                            value = argCommand.DefaultValue;
                        }

                        if(value == null) {
                            currentBuild.BuildArgs.Remove(argCommand.Name);
                        }
                        else {
                            currentBuild.BuildArgs[argCommand.Name] = value;
                        }
                    }
                }
                else {
                    if(currentBuild == null) {
                        throw new DockerfileSyntaxException("Command '{cmd}' may not precede 'from'.");
                    }
                    
                    currentBuild.Commands.Add(ParseCommand(cmd, flags, args, parseOptions));
                }
            }
            
            if(currentBuild != null) {
                yield return currentBuild.ToDockerfileBuild();
            }
        }

        private static async IAsyncEnumerable<string> ReadUnescapedLines(TextReader reader, ParseOptions parseOptions) {
            string? line;
            while((line = await reader.ReadLineAsync()) != null) {
                line = line.TrimStart();

                if(parseOptions.LookForDirectives) {

                    var match = tokenEscapeRegex.Match(line);
                    if(match.Success) {
                        if(parseOptions.EscapeSeen) {
                            throw new DockerfileSyntaxException("Only one escape parser directive can be used");
                        }

                        parseOptions.EscapeSeen = true;
                        parseOptions.EscapeChar = match.Groups["escapechar"].Value[0];
                    }
                    else {
                        parseOptions.LookForDirectives = false;
                    }

                }
                
                if(line.StartsWith('#')) {
                    continue;
                }

                yield return line;
            }
        }

        private static async IAsyncEnumerable<string> ReadFullLines(TextReader reader, ParseOptions parseOptions) {
            var enumerator = ReadUnescapedLines(reader, parseOptions).GetAsyncEnumerator();

            var sb = new StringBuilder();
            while(await enumerator.MoveNextAsync()) {
                var line = enumerator.Current;
                if(!CheckEscapedLine(parseOptions, ref line)) {
                    yield return line;
                    continue;
                }

                sb.Clear();
                sb.Append(line);

                bool isEscape = true;
                while(isEscape && await enumerator.MoveNextAsync()) {
                    line = enumerator.Current;
                    isEscape = CheckEscapedLine(parseOptions, ref line);
                    sb.Append(line);
                }

                yield return sb.ToString();
            }
        }

        private static bool CheckEscapedLine(ParseOptions parseOptions, ref string line) {
            var trimmedLine = line.TrimEnd(whitespaceChars);
            bool isEscape = trimmedLine.EndsWith(parseOptions.EscapeChar);
            if(isEscape) {
                line = trimmedLine.TrimEnd(parseOptions.EscapeChar);
            }
            
            return isEscape;
        }

        private static IAsyncEnumerable<string> ReadLines(TextReader reader, ParseOptions parseOptions) =>
            ReadFullLines(reader, parseOptions).Where(line => line.Length > 0);

        private static (string cmd, List<string> flags, string args) SplitCommand(string line) {
            var cmdLine = line.Trim(whitespaceChars).Split(whitespaceChars, 2, StringSplitOptions.RemoveEmptyEntries);

            var cmd = cmdLine[0].ToLowerInvariant();
            var flags = new List<string>();
            string args = "";
            if(cmdLine.Length > 1) {
                args = ExtractBuilderFlags(cmdLine[1], flags);
            }

            return (cmd, flags, args);
        }

        enum FlagsPhase {
            Spaces,
            Flag,
            Quote,
        }

        private static string ExtractBuilderFlags(string line, List<string> flags) {
            
            var phase = FlagsPhase.Spaces;
            var word = new StringBuilder();
            char quote = default;
            bool blankOK = false;

            for(int i = 0; i < line.Length; ++i) {
                char ch = line[i];
                switch(phase) {
                    case FlagsPhase.Spaces when char.IsWhiteSpace(ch):
                        break;
                    
                    case FlagsPhase.Spaces when ch != '-' || i == line.Length - 1 || line[i + 1] != '-':
                        return line.Substring(i);
                    
                    case FlagsPhase.Spaces:
                        word.Append(ch);
                        break;
                    
                    case FlagsPhase.Flag when char.IsWhiteSpace(ch):
                        phase = FlagsPhase.Spaces;
                        if(word.Equals("--".AsSpan())) {
                            return line.Substring(i);
                        }

                        if(blankOK || word.Length > 0) {
                            flags.Add(word.ToString());
                        }

                        word.Clear();
                        blankOK = false;
                        break;
                    
                    case FlagsPhase.Flag when ch == '\'' || ch == '"':
                        quote = ch;
                        blankOK = true;
                        phase = FlagsPhase.Quote;
                        break;
                    
                    case FlagsPhase.Quote when ch == '\\':
                    case FlagsPhase.Flag when ch == '\\':
                        if(i == line.Length - 1) {
                            break;
                        }

                        ++i;
                        word.Append(line[i]);
                        break;

                    case FlagsPhase.Quote when ch == quote:
                        phase = FlagsPhase.Flag;
                        break;
                    
                    case FlagsPhase.Quote:
                    case FlagsPhase.Flag:
                        word.Append(ch);
                        break;
                }
            }

            if(phase != FlagsPhase.Spaces && !word.Equals("--".AsSpan()) && (blankOK || word.Length > 0)) {
                flags.Add(word.ToString());
            }
            
            return "";
        }

        private static CommandBase ParseCommand(string cmd, List<string> flags, string args, ParseOptions parseOptions) =>
            cmd switch {
                "run" => CommandParsers.Run(args, flags, parseOptions),
                "cmd" => CommandParsers.Cmd(args, flags, parseOptions),
                "label" => CommandParsers.Label(args, flags, parseOptions),
                "maintainer" => CommandParsers.Maintainer(args, flags, parseOptions),
                "expose" => CommandParsers.Expose(args, flags, parseOptions),
                "env" => CommandParsers.Env(args, flags, parseOptions),
                "add" => CommandParsers.Add(args, flags, parseOptions),
                "copy" => CommandParsers.Copy(args, flags, parseOptions),
                "entrypoint" => CommandParsers.Entrypoint(args, flags, parseOptions),
                "volume" => CommandParsers.Volume(args, flags, parseOptions),
                "user" => CommandParsers.User(args, flags, parseOptions),
                "workdir" => CommandParsers.Workdir(args, flags, parseOptions),
                "onbuild" => CommandParsers.OnBuild(args, flags, parseOptions),
                "stopsignal" => CommandParsers.StopSignal(args, flags, parseOptions),
                "healthcheck" => CommandParsers.HealthCheck(args, flags, parseOptions),
                "shell" => CommandParsers.Shell(args, flags, parseOptions),
                _ => throw new DockerfileSyntaxException($"Unknown command '{cmd}'"),
            };
    }
}