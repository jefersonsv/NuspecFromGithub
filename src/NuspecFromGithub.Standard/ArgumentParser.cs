using CommandLineParser.Arguments;
using CommandLineParser.Exceptions;
using System;

namespace NuspecFromGithub.Standard
{
    public static class ArgumentParser
    {
        public static CommandLineParser.CommandLineParser Get()
        {
            try
            {
                var parser = new CommandLineParser.CommandLineParser();

                ValueArgument<string> project = new ValueArgument<string>(
                    'p', "project", "Specify the project path file");
                project.Optional = false;

                ValueArgument<string> github = new ValueArgument<string>(
                    'g', "github", "Specify the username/repository of github");
                github.Optional = false;

                SwitchArgument force = new SwitchArgument('f', "force", "Force recreate file", false);

                parser.Arguments.Add(project);
                parser.Arguments.Add(force);
                parser.Arguments.Add(github);

                return parser;
            }
            catch (CommandLineArgumentException ex)
            {
                if (args.Count() > 0)
                {
                    Console.WriteLine(ex.Message);
                }
                parser.ShowUsage();
            }
        }
    }
}