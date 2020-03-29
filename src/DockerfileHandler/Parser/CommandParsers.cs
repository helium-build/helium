using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Helium.DockerfileHandler.Commands;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Helium.DockerfileHandler.Parser
{
    internal static class CommandParsers
    {
        public static FromCommand From(string args, List<string> flags, ParseOptions parseOptions) {
            foreach(var flag in flags) {
                if(flag.StartsWith("--platform=", StringComparison.InvariantCulture)) {
                    throw new NotSupportedException();
                }
                else {
                    throw new DockerfileSyntaxException("Invalid FROM flags");
                }
            }

            var parts = ParseStringsWhitespaceDelimited(args);
            if(parts.Length == 1) {
                return new FromCommand(image: SubstitueArgs(parts[0], parseOptions.BuildArgs), asName: null);
            }
            else if(parts.Length == 3 && parts[1].Equals("AS", StringComparison.InvariantCultureIgnoreCase)) {
                return new FromCommand(image: SubstitueArgs(parts[0], parseOptions.BuildArgs), asName: SubstitueArgs(parts[2], parseOptions.BuildArgs));
            }
            else {
                throw new DockerfileSyntaxException("Invalid FROM command.");
            }
        }
        
        public static ArgCommand Arg(string args, List<string> flags, ParseOptions parseOptions) {
            if(flags.Count > 0) {
                throw new DockerfileSyntaxException("Invalid ARG flags");
            }
            
            var declaredArgs = ParseNameOrNameVal(args, parseOptions).ToList();
            if(declaredArgs.Count != 1) {
                throw new DockerfileSyntaxException("ARG expects one argument");
            }

            var arg = declaredArgs[0];
            
            return new ArgCommand(arg.name, arg.value);
        }

        public static CommandBase Run(string args, List<string> flags, ParseOptions parseOptions) {

            if(flags.Count > 0) {
                throw new DockerfileSyntaxException("Invalid RUN flags");
            }
            
            var execArgs = ParseMaybeJson(args);

            if(execArgs != null) {
                return new RunExecCommand(execArgs, parseOptions.BuildArgs);
            }
            else {
                return new RunShellCommand(args, parseOptions.BuildArgs);
            }
        }

        public static CommandBase Cmd(string args, List<string> flags, ParseOptions parseOptions) {
            throw new NotImplementedException();
        }

        public static CommandBase Label(string args, List<string> flags, ParseOptions parseOptions) {
            throw new NotImplementedException();
        }

        public static CommandBase Maintainer(string args, List<string> flags, ParseOptions parseOptions) {
            throw new NotImplementedException();
        }

        public static CommandBase Expose(string args, List<string> flags, ParseOptions parseOptions) {
            throw new NotImplementedException();
        }

        public static CommandBase Env(string args, List<string> flags, ParseOptions parseOptions) {
            throw new NotImplementedException();
        }

        public static CommandBase Add(string args, List<string> flags, ParseOptions parseOptions) {
            throw new NotImplementedException();
        }

        public static CommandBase Copy(string args, List<string> flags, ParseOptions parseOptions) {
            throw new NotImplementedException();
        }

        public static CommandBase Entrypoint(string args, List<string> flags, ParseOptions parseOptions) {
            throw new NotImplementedException();
        }

        public static CommandBase Volume(string args, List<string> flags, ParseOptions parseOptions) {
            throw new NotImplementedException();
        }

        public static CommandBase User(string args, List<string> flags, ParseOptions parseOptions) {
            throw new NotImplementedException();
        }

        public static CommandBase Workdir(string args, List<string> flags, ParseOptions parseOptions) {
            throw new NotImplementedException();
        }
        public static CommandBase OnBuild(string args, List<string> flags, ParseOptions parseOptions) {
            throw new NotImplementedException();
        }

        public static CommandBase StopSignal(string args, List<string> flags, ParseOptions parseOptions) {
            throw new NotImplementedException();
        }

        public static CommandBase HealthCheck(string args, List<string> flags, ParseOptions parseOptions) {
            throw new NotImplementedException();
        }

        public static CommandBase Shell(string args, List<string> flags, ParseOptions parseOptions) {
            throw new NotImplementedException();
        }

        private static string SubstitueArgs(string part, IReadOnlyDictionary<string,string> buildArgs) {
            throw new NotImplementedException();
        }

        private static string[] ParseStringsWhitespaceDelimited(string args) =>
            args.Split(DockerfileParser.whitespaceChars, StringSplitOptions.RemoveEmptyEntries);

        private static List<string>? ParseMaybeJson(string args) {
            try {
                return JArray.Parse(args).Cast<string>().ToList();
            }
            catch {
                return null;
            }
        }
        
        private static IEnumerable<(string name, string? value)> ParseNameOrNameVal(string args, ParseOptions parseOptions) =>
            ParseWords(args, parseOptions).Select<string, (string, string?)>(word => {
                int eqPos = word.IndexOf('=');
                if(eqPos < 0) {
                    return (word.Substring(0, eqPos), word.Substring(eqPos + 1));
                }
                else {
                    return (word, null);
                }
            });

        enum WordPhase {
            Spaces,
            Word,
            Quote,
        }

        private static IEnumerable<string> ParseWords(string rest, ParseOptions parseOptions) {
            
            var phase = WordPhase.Spaces;
            var word = new StringBuilder();
            char quote = default;
            bool blankOK = false;
            int charWidth = 1;
            
            for(int i = 0; i < rest.Length; i += charWidth) {
                void UpdateCharWidth() {
                    charWidth = char.ConvertToUtf32(rest, i) > 0xFFFF ? 2 : 1;
                }

                UpdateCharWidth();
                
                void AppendCurrent() {
                    word.Append(rest[i]);
                    if(charWidth > 1) {
                        word.Append(rest[i + 1]);
                    }
                }

                switch(phase) {
                    case WordPhase.Spaces when char.IsWhiteSpace(rest, i):
                        break;
                    
                    case WordPhase.Spaces:
                        AppendCurrent();
                        break;
                    
                    case WordPhase.Word when char.IsWhiteSpace(rest, i):
                        phase = WordPhase.Spaces;
                        if(blankOK || word.Length > 0) {
                            yield return word.ToString();
                        }

                        word.Clear();
                        blankOK = false;
                        break;
                    
                    case WordPhase.Word when rest[i] == '\'' || rest[i] == '"':
                        quote = rest[i];
                        blankOK = true;
                        phase = WordPhase.Quote;
                        goto case WordPhase.Word;

                    case WordPhase.Quote when rest[i] == quote:
                        phase = WordPhase.Word;
                        goto case WordPhase.Word;
                    
                    case WordPhase.Quote when rest[i] == parseOptions.EscapeChar:
                    case WordPhase.Word when rest[i] == parseOptions.EscapeChar:
                        if(i == rest.Length - 1) {
                            break;
                        }
                        
                        AppendCurrent();

                        ++i;
                        UpdateCharWidth();
                        AppendCurrent();
                        break;
                    
                    case WordPhase.Quote:
                    case WordPhase.Word:
                        AppendCurrent();
                        break;
                }
            }

            if(phase != WordPhase.Spaces && !word.Equals("--".AsSpan()) && (blankOK || word.Length > 0)) {
                yield return word.ToString();
            }
        }

    }
}