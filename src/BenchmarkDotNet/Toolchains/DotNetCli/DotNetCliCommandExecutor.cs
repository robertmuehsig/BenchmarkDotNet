using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using JetBrains.Annotations;

namespace BenchmarkDotNet.Toolchains.DotNetCli
{
    [PublicAPI]
    public static class DotNetCliCommandExecutor
    {
        [PublicAPI]
        public static DotNetCliCommandResult Execute(DotNetCliCommand parameters)
        {
            using (var process = new Process { StartInfo = BuildStartInfo(parameters.CliPath, parameters.GenerateResult.ArtifactsPaths.BuildArtifactsDirectoryPath, parameters.Arguments, parameters.EnvironmentVariables) })
            {
                parameters.Logger.WriteLineInfo($"// start {parameters.CliPath ?? "dotnet"} {parameters.Arguments} in {parameters.GenerateResult.ArtifactsPaths.BuildArtifactsDirectoryPath}");
                
                var stopwatch = Stopwatch.StartNew();
                process.Start();

                string standardOutput = process.StandardOutput.ReadToEnd();
                string standardError = process.StandardError.ReadToEnd();

                process.WaitForExit();
                stopwatch.Stop();

                parameters.Logger.WriteLineInfo($"// command took {stopwatch.Elapsed.TotalSeconds:0.##}s and exited with {process.ExitCode}");

                return process.ExitCode <= 0
                    ? DotNetCliCommandResult.Success(stopwatch.Elapsed, standardOutput)
                    : DotNetCliCommandResult.Failure(stopwatch.Elapsed, standardError, standardOutput);
            }
        }
        
        internal static string GetDotNetSdkVersion()
        {
            using (var process = new Process { StartInfo = BuildStartInfo(customDotNetCliPath: null, workingDirectory: string.Empty, arguments: "--version") })
            {
                try
                {
                    process.Start();
                }
                catch (Win32Exception) // dotnet cli is not installed
                {
                    return null;
                }

                string output = process.StandardOutput.ReadToEnd();

                process.WaitForExit();

                // first line contains something like ".NET Command Line Tools (1.0.0-beta-001603)"
                return Regex.Split(output, Environment.NewLine, RegexOptions.Compiled)
                    .FirstOrDefault(line => !string.IsNullOrEmpty(line));
            }
        }

        internal static ProcessStartInfo BuildStartInfo(string customDotNetCliPath, string workingDirectory, string arguments,
            IReadOnlyList<EnvironmentVariable> environmentVariables = null, bool redirectStandardInput = false)
        {
            const string dotnetMultiLevelLookupEnvVarName = "DOTNET_MULTILEVEL_LOOKUP";

            var startInfo = new ProcessStartInfo
            {
                FileName = customDotNetCliPath ?? "dotnet",
                WorkingDirectory = workingDirectory,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = redirectStandardInput
            };

            if (environmentVariables != null)
                foreach (var environmentVariable in environmentVariables)
                    startInfo.EnvironmentVariables[environmentVariable.Key] = environmentVariable.Value;

            if (!string.IsNullOrEmpty(customDotNetCliPath) && (environmentVariables == null || environmentVariables.All(envVar => envVar.Key != dotnetMultiLevelLookupEnvVarName)))
                startInfo.EnvironmentVariables[dotnetMultiLevelLookupEnvVarName] = "0";

            return startInfo;
        }
    }
}