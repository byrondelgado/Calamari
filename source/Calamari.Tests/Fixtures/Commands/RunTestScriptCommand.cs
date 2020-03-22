﻿using System.IO;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;

namespace Calamari.Tests.Fixtures.Commands
{
    /// <summary>
    /// A cut down command that runs a script without any journaling, variable substitution or
    /// other optional steps.
    /// </summary>
    [Command("run-test-script", Description = "Invokes a PowerShell or ScriptCS script")]
    public class RunTestScriptCommand : Command
    {
        private string scriptFile;
        private readonly IVariables variables;
        private readonly CombinedScriptEngine scriptEngine;

        public RunTestScriptCommand(
            IVariables variables,
            CombinedScriptEngine scriptEngine)
        {
            Options.Add("script=", "Path to the script to execute.", v => scriptFile = Path.GetFullPath(v));

            this.variables = variables;
            this.scriptEngine = scriptEngine;
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);
            
            var runner = new CommandLineRunner(ConsoleLog.Instance, variables);
            Log.VerboseFormat("Executing '{0}'", scriptFile);
            var result = scriptEngine.Execute(new Script(scriptFile, ""), variables, runner);

            if (result.ExitCode == 0 && result.HasErrors && variables.GetFlag(SpecialVariables.Action.FailScriptOnErrorOutput, false))
            {
                return -1;
            }

            return result.ExitCode;
        }
    }
}
