﻿using System;
using System.Linq;
using Calamari.Shared;
using Calamari.Shared.Commands;
using Calamari.Shared.FileSystem;

namespace Calamari.Azure.Deployment.Conventions
{
    public class FindCloudServicePackageConvention : IConvention
    {
        readonly ICalamariFileSystem fileSystem;

        public FindCloudServicePackageConvention(ICalamariFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public void Run(IExecutionContext deployment)
        {
           deployment.Variables.Set(SpecialVariables.Action.Azure.CloudServicePackagePath, FindPackage(deployment.CurrentDirectory)); 
        }

        string FindPackage(string workingDirectory)
        {
            var packages = fileSystem.EnumerateFiles(workingDirectory, "*.cspkg").ToList();

            if (packages.Count == 0)
            {
                // Try subdirectories
                packages = fileSystem.EnumerateFilesRecursively(workingDirectory, "*.cspkg").ToList();
            }

            if (packages.Count == 0)
            {
                throw new CommandException("Your package does not appear to contain any Azure Cloud Service package (.cspkg) files.");
            }

            if (packages.Count > 1)
            {
                throw new CommandException("Your deployment package contains more than one Cloud Service package (.cspkg) file, which is unsupported. Files: "
                    + string.Concat(packages.Select(p => Environment.NewLine + " - " + p)));
            }

            return packages.Single();
        }
    }
}