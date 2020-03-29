using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Helium.DockerfileHandler.Parser
{
    // https://github.com/moby/buildkit/blob/adde225dcb4d4773c157e5ca7f268f707eac4088/frontend/dockerfile/shell/lex.go
    public class ShellOps
    {
        private readonly IReadOnlyDictionary<string, string> env;
        private readonly char escapeChar;

        public ShellOps(IReadOnlyDictionary<string, string> env, char escapeChar) {
            this.env = env;
            this.escapeChar = escapeChar;
        }

        public string Process(string line) {
            var result = new StringBuilder();
            ProcessStopOn(CodePoints(line), result, null);
            return result.ToString();
        }
            


        private IEnumerator<int> CodePoints(string line) {
            for(int i = 0; i < line.Length; ++i) {
                int codePoint = char.ConvertToUtf32(line, i);
                yield return codePoint;
                if(codePoint >= 0xFFFF) ++i;
            }
        }

        private void ProcessStopOn(IEnumerator<int> input, StringBuilder result, int? stopChar) {
            while(input.MoveNext()) {
                noAdvance:
                switch(input.Current) {
                    case var ch when ch == stopChar:
                        return;
                    
                    case '\'':
                        ProcessSingleQuote(input, result);
                        break;
                    
                    case '"':
                        ProcessDoubleQuote(input, result);
                        break;
                    
                    case '$':
                        if(ProcessDollar(input, result)) {
                            goto noAdvance;
                        }
                        break;
                    
                    case var ch:
                        result.Append(ch);
                        break;
                }
            }

            if(stopChar != null) {
                throw new DockerfileSyntaxException($"Unexpected EOF. Expected {stopChar}");
            }
        }
        
        private void ProcessSingleQuote(IEnumerator<int> input, StringBuilder result) {
            while(input.MoveNext()) {
                switch(input.Current) {
                    case '\'':
                        return;
                    
                    case var ch:
                        result.Append(char.ConvertFromUtf32(ch));
                        break;
                }
            }
            
            throw new DockerfileSyntaxException("Unexpected EOF. Expected '");
        }

        private void ProcessDoubleQuote(IEnumerator<int> input, StringBuilder result) {
            while(input.MoveNext()) {
                noAdvance:
                switch(input.Current) {
                    case '"':
                        return;
                    
                    case '$':
                        if(ProcessDollar(input, result)) {
                            goto noAdvance;
                        }
                        break;
                    
                    case var ch when ch == escapeChar:
                        if(input.MoveNext()) {
                            var escaped = input.Current;
                            if(escaped != '"' && escaped != '$' && escaped != escapeChar) {
                                result.Append(char.ConvertFromUtf32(ch));
                            }
                            result.Append(char.ConvertFromUtf32(escaped));
                        }
                        break;
                    
                    case var ch:
                        result.Append(char.ConvertFromUtf32(ch));
                        break;
                }
            }
            
            throw new DockerfileSyntaxException("Unexpected EOF. Expected \"");
        }
        
        // Returns true if it advanced past the variable. Caller should not call MoveNext
        private bool ProcessDollar(IEnumerator<int> input, StringBuilder result) {
            if(!input.MoveNext()) throw new DockerfileSyntaxException("Unexpected EOF. Expected variable.");
            var firstCh = input.Current;

            if(firstCh != '{') {
                bool hasAdvanced = ProcessName(input, out var name);
                if(name == "") {
                    result.Append("$");
                }
                else {
                    if(env.TryGetValue(name, out var value)) {
                        result.Append(value);
                    }
                }
                return hasAdvanced;
            }
            else {
                if(!input.MoveNext()) {
                    throw new DockerfileSyntaxException("Unexpected EOF. Missing }");
                }

                switch(input.Current) {
                    case '{':
                    case '}':
                    case ':':
                        throw new DockerfileSyntaxException("Bad substitution");
                }

                bool hasAdvanced = ProcessName(input, out var name);
                
                if(!hasAdvanced && !input.MoveNext()) {
                    throw new DockerfileSyntaxException("Unexpected EOF. Missing }");
                }

                switch(input.Current) {
                    case '}':
                    {
                        if(env.TryGetValue(name, out var value)) {
                            result.Append(value);
                        }
                        break;
                    }

                    case '?':
                    {
                        var message = new StringBuilder();
                        ProcessStopOn(input, message, '}');

                        if(!env.TryGetValue(name, out var value)) {
                            throw new DockerfileSyntaxException($"Variable {name} cannot be undefined: {message}");
                        }

                        result.Append(value);
                        break;
                    }

                    case ':':
                    {
                        if(!hasAdvanced && !input.MoveNext()) {
                            throw new DockerfileSyntaxException("Unexpected EOF. Missing }");
                        }

                        int modifier = input.Current;
                
                        var word = new StringBuilder();
                        ProcessStopOn(input, word, '}');

                        switch(modifier) {
                            case '+':
                            {
                                if(env.TryGetValue(name, out var value) && !string.IsNullOrEmpty(value)) {
                                    result.Append(word);
                                }
                                break;
                            }

                            case '-':
                            {
                                if(env.TryGetValue(name, out var value) && !string.IsNullOrEmpty(value)) {
                                    result.Append(value);
                                }
                                else {
                                    result.Append(word);
                                }
                                break;
                            }

                            case '?':
                            {
                                if(!env.TryGetValue(name, out var value) || string.IsNullOrEmpty(value)) {
                                    throw new DockerfileSyntaxException($"Variable {name} cannot be empty: {word}");
                                }

                                result.Append(value);
                                break;
                            }
                        }
                        break;
                    }
                }

                return false;
            }
        }

        // Always comes in with the first name character as current.
        // Returns true if it advanced past the variable.
        private bool ProcessName(IEnumerator<int> input, out string name) {
            if(char.IsDigit(char.ConvertFromUtf32(input.Current), 0)) {
                var sb = new StringBuilder();
                do {
                    sb.Append(input.Current);
                } while(input.MoveNext() && char.IsDigit(char.ConvertFromUtf32(input.Current), 0));

                name = sb.ToString();
                return true;
            }
            else if(IsSpecialParam(input.Current)) {
                name = char.ConvertFromUtf32(input.Current);
                return false;
            }
            else {
                var sb = new StringBuilder();

                for(int ch = input.Current;
                    ch == '_' || char.IsLetterOrDigit(char.ConvertFromUtf32(ch), 0);
                    ch = input.Current) {

                    if(!input.MoveNext()) {
                        name = sb.ToString();
                        return false;
                    }
                }

                name = sb.ToString();
                return true;
            }
        }

        private bool IsSpecialParam(int ch) {
            switch(ch) {
                case '@':
                case '*':
                case '#':
                case '?':
                case '-':
                case '$':
                case '!':
                case '0':
                    return true;
                
                default:
                    return false;
            }
        }
    }
}