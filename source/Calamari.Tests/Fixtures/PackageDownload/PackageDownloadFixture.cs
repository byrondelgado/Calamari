﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
using Microsoft.SqlServer.Server;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.PackageDownload
{
    [TestFixture]
    public class PackageDownloadFixture : CalamariFixture
    {
        const string FeedUriEnvironmentVariable = "CALAMARI_AUTHFEED";
        const string FeedUsernameEnvironmentVariable = "CALAMARI_AUTHUSERNAME";
        const string FeedPasswordEnvironmentVariable = "CALAMARI_AUTHPASSWORD";

        string PublicFeedId = "feeds-myget";
        string PublicFeedUri = "https://www.myget.org/F/octopusdeploy-tests";
        string AuthFeedId = "feeds-authmyget";
        string AuthFeedUri = Environment.GetEnvironmentVariable(FeedUriEnvironmentVariable);
        string FeedUsername = Environment.GetEnvironmentVariable(FeedUsernameEnvironmentVariable);
        string FeedPassword = Environment.GetEnvironmentVariable(FeedPasswordEnvironmentVariable);
        string InvalidFeedPassword = "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
        string FileShareFeedId = "feeds-local";

        string PackageId = "OctoConsole";
        string InvalidPackageId = "OctConsole";
        string PackageVersion = "1.0.0.0";
        string InvalidPackageVersion = "1.0.0.x";
        string ExpectedPackageHash = "40d78a00090ba7f17920a27cc05d5279bd9a4856";
        string ExpectedPackageSize = "6346";
        string FileSharePackageId = "Acme.Web";
        string FileSharePackageVersion = "1.0.0";
        string FileSharePackageExpectedSize = "3341";

        string GetPackageDownloadFolder()
        {
            string currentDirectory = typeof(PackageDownloadFixture).Assembly.FullLocalPath();
            string targetFolder = "source\\";
            int index = currentDirectory.LastIndexOf(targetFolder, StringComparison.OrdinalIgnoreCase);
            string solutionRoot = currentDirectory.Substring(0, index + targetFolder.Length);

            var packageDirectory = Path.Combine(solutionRoot, "Calamari.Tests\\bin\\Fixtures\\PackageDownload");

            return packageDirectory;
        }

        CalamariResult DownloadPackage(string packageId,
            string packageVersion,
            string feedId,
            string feedUri,
            string feedUsername = "",
            string feedPassword = "",
            bool forcePackageDownload = false)
        {
            var calamari = Calamari()
                .Action("download-package")
                .Argument("packageId", packageId)
                .Argument("packageVersion", packageVersion)
                .Argument("feedId", feedId)
                .Argument("feedUri", feedUri);

            if (!String.IsNullOrWhiteSpace(feedUsername))
                calamari.Argument("feedUsername", feedUsername);

            if (!String.IsNullOrWhiteSpace(feedPassword))
                calamari.Argument("feedPassword", feedUsername);

            if (forcePackageDownload)
                calamari.Flag("forcePackageDownload");

            return Invoke(calamari);

        }

        [SetUp]
        public void SetUp()
        {
            var workingDirectory = GetPackageDownloadFolder();
            if (!Directory.Exists(workingDirectory))
                Directory.CreateDirectory(workingDirectory);

            Directory.SetCurrentDirectory(workingDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            var workingDirectory = GetPackageDownloadFolder() + "\\Work";
            if(Directory.Exists(workingDirectory))
                Directory.Delete(workingDirectory, true);
        }

        [Test]
        public void ShouldDownloadPackage()
        {
            var result = DownloadPackage(PackageId, PackageVersion, PublicFeedId, PublicFeedUri);

            result.AssertZero();

            result.AssertOutput("Downloading NuGet package {0} {1} from feed: '{2}'", PackageId,
                PackageVersion, PublicFeedUri);
            result.AssertOutput("Downloaded package will be stored in: '{0}\\Work\\{1}'",
                GetPackageDownloadFolder(), PublicFeedId);
            result.AssertOutput("Found package {0} version {1}", PackageId, PackageVersion);
            result.AssertOutput("##octopus[setVariable name=\"{0}\" value=\"{1}\"]", "Package.Hash", ExpectedPackageHash);
            result.AssertOutput("##octopus[setVariable name=\"{0}\" value=\"{1}\"]", "Package.Size", ExpectedPackageSize);
            result.AssertOutput("##octopus[setVariable name=\"Package.InstallationDirectoryPath\" value=\"{0}\\Work\\{1}\\{2}.{3}", GetPackageDownloadFolder(), PublicFeedId, PackageId, PackageVersion);
            result.AssertOutput("Package {0} {1} successfully downloaded from feed: '{2}'", PackageId, PackageVersion, PublicFeedUri);
        }

        [Test]
        public void ShouldUsePackageFromCache()
        {
            DownloadPackage(PackageId, PackageVersion, PublicFeedId, PublicFeedUri);

            var result = DownloadPackage(PackageId, PackageVersion, PublicFeedId, PublicFeedUri);

            result.AssertZero();

            result.AssertOutput("Checking package cache for package {0} {1}", PackageId,
                PackageVersion);
            result.AssertOutput("Package was found in cache. No need to download. Using file: '{0}\\Work\\{1}\\{2}.{3}",
                    GetPackageDownloadFolder(), PublicFeedId, PackageId, PackageVersion);
            result.AssertOutput("##octopus[setVariable name=\"{0}\" value=\"{1}\"]", "Package.Hash", ExpectedPackageHash);
            result.AssertOutput("##octopus[setVariable name=\"{0}\" value=\"{1}\"]", "Package.Size", ExpectedPackageSize);
            result.AssertOutput("##octopus[setVariable name=\"Package.InstallationDirectoryPath\" value=\"{0}\\Work\\{1}\\{2}.{3}", GetPackageDownloadFolder(), PublicFeedId, PackageId, PackageVersion);
        }

        [Test]
        public void ShouldByPassCacheAndDownloadPackage()
        {
            DownloadPackage(PackageId, PackageVersion, PublicFeedId, PublicFeedUri);
            
            var result = DownloadPackage(PackageId, PackageVersion, PublicFeedId, PublicFeedUri, forcePackageDownload: true);

            result.AssertZero();

            result.AssertOutput("Downloading NuGet package {0} {1} from feed: '{2}'", PackageId,
                PackageVersion, PublicFeedUri);
            result.AssertOutput("Downloaded package will be stored in: '{0}\\Work\\{1}'",
                GetPackageDownloadFolder(), PublicFeedId);
            result.AssertOutput("Found package {0} version {1}", PackageId, PackageVersion);
            result.AssertOutput("##octopus[setVariable name=\"{0}\" value=\"{1}\"]", "Package.Hash", ExpectedPackageHash);
            result.AssertOutput("##octopus[setVariable name=\"{0}\" value=\"{1}\"]", "Package.Size", ExpectedPackageSize);
            result.AssertOutput("##octopus[setVariable name=\"Package.InstallationDirectoryPath\" value=\"{0}\\Work\\{1}\\{2}.{3}", GetPackageDownloadFolder(), PublicFeedId, PackageId, PackageVersion);
            result.AssertOutput("Package {0} {1} successfully downloaded from feed: '{2}'", PackageId, PackageVersion, PublicFeedUri);
        }

        [Test]
        [AuthenticatedTest(FeedUriEnvironmentVariable, FeedUsernameEnvironmentVariable, FeedPasswordEnvironmentVariable)]
        public void PrivateNuGetFeedShouldDownloadPackage()
        {
            var result = DownloadPackage(PackageId, PackageVersion, AuthFeedId, AuthFeedUri, FeedUsername, FeedPassword);

            result.AssertZero();

            result.AssertOutput("Downloading NuGet package {0} {1} from feed: '{2}'", PackageId,
                PackageVersion, PublicFeedUri);
            result.AssertOutput("Downloaded package will be stored in: '{0}\\Work\\{1}'",
                GetPackageDownloadFolder(), AuthFeedId);
            result.AssertOutput("Found package {0} version {1}", PackageId, PackageVersion);
            result.AssertOutput("##octopus[setVariable name=\"{0}\" value=\"{1}\"]", "Package.Hash", ExpectedPackageHash);
            result.AssertOutput("##octopus[setVariable name=\"{0}\" value=\"{1}\"]", "Package.Size", ExpectedPackageSize);
            result.AssertOutput("##octopus[setVariable name=\"Package.InstallationDirectoryPath\" value=\"{0}\\Work\\{1}\\{2}.{3}", GetPackageDownloadFolder(), AuthFeedId, PackageId, PackageVersion);
            result.AssertOutput("Package {0} {1} successfully downloaded from feed: '{2}'", PackageId, PackageVersion, AuthFeedUri);
        }

        [Test]
        [AuthenticatedTest(FeedUriEnvironmentVariable, FeedUsernameEnvironmentVariable, FeedPasswordEnvironmentVariable)]
        public void PrivateNuGetFeedShouldUsePackageFromCache()
        {
            DownloadPackage(PackageId, PackageVersion, AuthFeedId, AuthFeedUri, FeedUsername, FeedPassword);
            var result = DownloadPackage(PackageId, PackageVersion, AuthFeedId, AuthFeedUri, FeedUsername, FeedPassword);

            result.AssertZero();

            result.AssertOutput("Checking package cache for package {0} {1}", PackageId,
                PackageVersion);
            result.AssertOutput("Package was found in cache. No need to download. Using file: '{0}\\Work\\{1}\\{2}.{3}",
                    GetPackageDownloadFolder(), AuthFeedId, PackageId, PackageVersion);
            result.AssertOutput("##octopus[setVariable name=\"{0}\" value=\"{1}\"]", "Package.Hash", ExpectedPackageHash);
            result.AssertOutput("##octopus[setVariable name=\"{0}\" value=\"{1}\"]", "Package.Size", ExpectedPackageSize);
            result.AssertOutput("##octopus[setVariable name=\"Package.InstallationDirectoryPath\" value=\"{0}\\Work\\{1}\\{2}.{3}", GetPackageDownloadFolder(), AuthFeedId, PackageId, PackageVersion);
        }

        [Test]
        [AuthenticatedTest(FeedUriEnvironmentVariable, FeedUsernameEnvironmentVariable, FeedPasswordEnvironmentVariable)]
        public void PrivateNuGetFeedShouldByPassCacheAndDownloadPackage()
        {
            DownloadPackage(PackageId, PackageVersion, AuthFeedId, AuthFeedUri, FeedUsername, FeedPassword);
            var result = DownloadPackage(PackageId, PackageVersion, AuthFeedId, AuthFeedUri, FeedUsername, FeedPassword, true);

            result.AssertZero();

            result.AssertOutput("Downloading NuGet package {0} {1} from feed: '{2}'", PackageId,
                PackageVersion, AuthFeedUri);
            result.AssertOutput("Downloaded package will be stored in: '{0}\\Work\\{1}'",
                GetPackageDownloadFolder(), AuthFeedId);
            result.AssertOutput("Found package {0} version {1}", PackageId, PackageVersion);
            result.AssertOutput("##octopus[setVariable name=\"{0}\" value=\"{1}\"]", "Package.Hash", ExpectedPackageHash);
            result.AssertOutput("##octopus[setVariable name=\"{0}\" value=\"{1}\"]", "Package.Size", ExpectedPackageSize);
            result.AssertOutput("##octopus[setVariable name=\"Package.InstallationDirectoryPath\" value=\"{0}\\Work\\{1}\\{2}.{3}", GetPackageDownloadFolder(), AuthFeedId, PackageId, PackageVersion);
            result.AssertOutput("Package {0} {1} successfully downloaded from feed: '{2}'", PackageId, PackageVersion, AuthFeedUri);
        }

        [Test]
        [AuthenticatedTest(FeedUriEnvironmentVariable, FeedUsernameEnvironmentVariable, FeedPasswordEnvironmentVariable)]
        public void PrivateNuGetFeedShouldFailDownloadPackageWhenInvalidCredentials()
        {
            var result = DownloadPackage(PackageId, PackageVersion, AuthFeedId, AuthFeedUri, FeedUsername, InvalidFeedPassword);

            result.AssertNonZero();

            result.AssertOutput("Downloading NuGet package {0} {1} from feed: '{2}'", PackageId,
                PackageVersion, AuthFeedUri);
            result.AssertOutput("Downloaded package will be stored in: '{0}\\Work\\{1}'",
                GetPackageDownloadFolder(), AuthFeedId);
            result.AssertErrorOutput("Unable to download package: The remote server returned an error: (403) Forbidden.");
        }

        [Test]
        public void FileShareFeedShouldDownloadPackage()
        {
            using (var acmeWeb = new TemporaryFile(PackageBuilder.BuildSamplePackage(FileSharePackageId, FileSharePackageVersion)))
            {
                var result = DownloadPackage(FileSharePackageId, FileSharePackageVersion, FileShareFeedId, acmeWeb.DirectoryPath);

                result.AssertZero();

                result.AssertOutput("Downloading NuGet package {0} {1} from feed: '{2}'", FileSharePackageId,
                    FileSharePackageVersion, new Uri(acmeWeb.DirectoryPath));
                result.AssertOutput("Downloaded package will be stored in: '{0}\\Work\\{1}'",
                    GetPackageDownloadFolder(), FileShareFeedId);
                result.AssertOutput("Found package {0} version {1}", FileSharePackageId, FileSharePackageVersion);
                result.AssertOutput("##octopus[setVariable name=\"{0}\" value=\"{1}\"]", "Package.Hash",
                    acmeWeb.Hash);
                result.AssertOutput("##octopus[setVariable name=\"{0}\" value=\"{1}\"]", "Package.Size",
                    FileSharePackageExpectedSize);
                result.AssertOutput("##octopus[setVariable name=\"Package.InstallationDirectoryPath\" value=\"{0}\\Work\\{1}\\{2}.{3}",
                        GetPackageDownloadFolder(), FileShareFeedId, FileSharePackageId, FileSharePackageVersion);
                result.AssertOutput("Package {0} {1} successfully downloaded from feed: '{2}'", FileSharePackageId,
                    FileSharePackageVersion, acmeWeb.DirectoryPath);
            }
        }

        [Test]
        public void FileShareFeedShouldUsePackageFromCache()
        {
            using (var acmeWeb = new TemporaryFile(PackageBuilder.BuildSamplePackage(FileSharePackageId, FileSharePackageVersion)))
            {
                DownloadPackage(FileSharePackageId, FileSharePackageVersion, FileShareFeedId, acmeWeb.DirectoryPath);

                var result = DownloadPackage(FileSharePackageId, FileSharePackageVersion, FileShareFeedId, acmeWeb.DirectoryPath);
                result.AssertZero();

                result.AssertOutput("Checking package cache for package {0} {1}", FileSharePackageId, FileSharePackageVersion);
                result.AssertOutput("Package was found in cache. No need to download. Using file: '{0}\\Work\\{1}\\{2}.{3}",
                        GetPackageDownloadFolder(), FileShareFeedId, FileSharePackageId, FileSharePackageVersion);
                result.AssertOutput("##octopus[setVariable name=\"{0}\" value=\"{1}\"]", "Package.Hash",
                    acmeWeb.Hash);
                result.AssertOutput("##octopus[setVariable name=\"{0}\" value=\"{1}\"]", "Package.Size",
                    FileSharePackageExpectedSize);
                result.AssertOutput("##octopus[setVariable name=\"Package.InstallationDirectoryPath\" value=\"{0}\\Work\\{1}\\{2}.{3}",
                        GetPackageDownloadFolder(), FileShareFeedId, FileSharePackageId, FileSharePackageVersion);
            }
        }

        [Test]
        public void FileShareFeedShouldByPassCacheAndDownloadPackage()
        {
            using (
                var acmeWeb =
                    new TemporaryFile(PackageBuilder.BuildSamplePackage(FileSharePackageId, FileSharePackageVersion)))
            {
                DownloadPackage(FileSharePackageId, FileSharePackageVersion, FileShareFeedId, acmeWeb.DirectoryPath);
                var result = DownloadPackage(FileSharePackageId, FileSharePackageVersion, FileShareFeedId, acmeWeb.DirectoryPath,
                    forcePackageDownload: true);

                result.AssertZero();

                result.AssertOutput("Downloading NuGet package {0} {1} from feed: '{2}'", FileSharePackageId, 
                    FileSharePackageVersion, new Uri(acmeWeb.DirectoryPath));
                result.AssertOutput("Downloaded package will be stored in: '{0}\\Work\\{1}'",
                    GetPackageDownloadFolder(), FileShareFeedId);
                result.AssertOutput("Found package {0} version {1}", FileSharePackageId, FileSharePackageVersion);
                result.AssertOutput("##octopus[setVariable name=\"{0}\" value=\"{1}\"]", "Package.Hash",
                    acmeWeb.Hash);
                result.AssertOutput("##octopus[setVariable name=\"{0}\" value=\"{1}\"]", "Package.Size",
                    FileSharePackageExpectedSize);
                result.AssertOutput("##octopus[setVariable name=\"Package.InstallationDirectoryPath\" value=\"{0}\\Work\\{1}\\{2}.{3}",
                        GetPackageDownloadFolder(), FileShareFeedId, FileSharePackageId, FileSharePackageVersion);
                result.AssertOutput("Package {0} {1} successfully downloaded from feed: '{2}'", FileSharePackageId,
                    FileSharePackageVersion, acmeWeb.DirectoryPath);
            }
        }

        [Test]
        public void FileShareFeedShouldFailDownloadPackageWhenInvalidFileShare()
        {
            using (
                var acmeWeb =
                    new TemporaryFile(PackageBuilder.BuildSamplePackage(FileSharePackageId, FileSharePackageVersion)))
            {
                var invalidFileShareUri = acmeWeb.DirectoryPath + "\\InvalidPath";

                var result = DownloadPackage(FileSharePackageId, FileSharePackageVersion, FileShareFeedId, invalidFileShareUri);
                result.AssertNonZero();

                result.AssertOutput("Downloading NuGet package {0} {1} from feed: '{2}'", FileSharePackageId,
                    FileSharePackageVersion, new Uri(invalidFileShareUri));
                result.AssertErrorOutput("Unable to download package: Could not find package {0} {1} in feed: '{2}'",
                    FileSharePackageId, FileSharePackageVersion, new Uri(invalidFileShareUri));
                result.AssertErrorOutput("Failed to download package {0} {1} from feed: '{2}'",
                    FileSharePackageId, FileSharePackageVersion, invalidFileShareUri);
            }
        }

        [Test]
        [Ignore("Need to get this setup and running somehow...need to think of a way to do it so it works across borders (aka TC or other members of the team)")]
        public void FileShareFeedShouldFailDownloadPackageWhenNoPermissions()
        {
            //TODO: Yeah
        }

        [Test]
        public void ShouldFailWhenNoPackageId()
        {
            var result = DownloadPackage("", PackageVersion, PublicFeedId, PublicFeedUri);
            result.AssertNonZero();

            result.AssertErrorOutput("No package ID was specified");
        }

        [Test]
        public void ShouldFailWhenInvalidPackageId()
        {
            var result = DownloadPackage(InvalidPackageId, PackageVersion, PublicFeedId, PublicFeedUri);
            result.AssertNonZero();

            result.AssertErrorOutput("Failed to download package {0} {1} from feed: '{2}'", InvalidPackageId, PackageVersion, PublicFeedUri);
        }

        [Test]
        public void ShouldFailWhenNoPackageVersion()
        {
            var result = DownloadPackage(PackageId, "", PublicFeedId, PublicFeedUri);
            result.AssertNonZero();

            result.AssertErrorOutput("No package version was specified");
        }

        [Test]
        public void ShouldFailWhenInvalidPackageVersion()
        {
            var result = DownloadPackage(PackageId, InvalidPackageVersion, PublicFeedId, PublicFeedUri);
            result.AssertNonZero();

            result.AssertErrorOutput("Package version specified is not a valid semantic version");
        }

        [Test]
        public void ShouldFailWhenNoFeedId()
        {
            var result = DownloadPackage(PackageId, PackageVersion, "", PublicFeedUri);
            result.AssertNonZero();

            result.AssertErrorOutput("No feed ID was specified");
        }

        [Test]
        public void ShouldFailWhenNoFeedUri()
        {
            var result = DownloadPackage(PackageId, PackageVersion, PublicFeedId, "");
            result.AssertNonZero();

            result.AssertErrorOutput("No feed URI was specified");
        }

        [Test]
        public void ShouldFailWhenInvalidFeedUri()
        {
            var result = DownloadPackage(PackageId, PackageVersion, PublicFeedId, "www.myget.org/F/octopusdeploy-tests");
            result.AssertNonZero();

            result.AssertOutput("URI specified is not a valid URI");
        }

        [Test]
        public void ShouldFailWhenUsernameIsSpecifiedButNoPassword()
        {
            var result = DownloadPackage(PackageId, PackageVersion, PublicFeedId, PublicFeedUri, FeedUsername);
            result.AssertNonZero();

            result.AssertErrorOutput("A username was specified but no password was provided");
        }
    }
}