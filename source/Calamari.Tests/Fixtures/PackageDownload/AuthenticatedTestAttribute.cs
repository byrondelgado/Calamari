﻿using System;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.PackageDownload
{
    [AttributeUsage(AttributeTargets.Method)]
    public class AuthenticatedTestAttribute : Attribute, ITestAction
    {
        readonly string feedUri;
        readonly string feedUsernameVariable;
        readonly string feedPasswordVariable;

        public AuthenticatedTestAttribute(string feedUri, string feedUsernameVariable, string feedPasswordVariable)
        {
            this.feedUri = feedUri;
            this.feedUsernameVariable = feedUsernameVariable;
            this.feedPasswordVariable = feedPasswordVariable;
        }

        public void BeforeTest(TestDetails testDetails)
        {
            Console.WriteLine($"Environment.GetEnvironmentVariable(\"{feedUri}\") = \"{Environment.GetEnvironmentVariable(feedUri)}\"");
            if (String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(feedUri)) ||
                String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(feedUsernameVariable)) ||
                String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(feedPasswordVariable)))
            {
                Assert.Ignore("The authenticated feed tests were skipped because the " + feedUri + ", " + feedUsernameVariable + " and " +feedPasswordVariable + " environment variables are not set.");
            }
        }

        public void AfterTest(TestDetails testDetails)
        {
        }

        public ActionTargets Targets { get; private set; }
    }
}
