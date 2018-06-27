﻿using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Cache;
using System.Threading;
using Calamari.Extensions;
using Calamari.Integration.FileSystem;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octopus.Versioning;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;

namespace Calamari.Integration.Packages.Download
{
    public class GitHubPackageDownloader : IPackageDownloader
    {
        private static readonly IPackageDownloaderUtils PackageDownloaderUtils = new PackageDownloaderUtils();
        readonly CalamariPhysicalFileSystem fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
        public static readonly string DownloadingExtension = ".downloading";
        const string Extension = ".zip";
        const char OwnerRepoSeperator = '/';

        public PackagePhysicalFileMetadata DownloadPackage(
            string packageId,
            IVersion version,
            string feedId,
            Uri feedUri,
            ICredentials feedCredentials,
            bool forcePackageDownload,
            int maxDownloadAttempts,
            TimeSpan downloadAttemptBackoff)
        {
            
            var cacheDirectory = PackageDownloaderUtils.GetPackageRoot(feedId);
            fileSystem.EnsureDirectoryExists(cacheDirectory);

            if (!forcePackageDownload)
            {
                var downloaded = SourceFromCache(packageId, version, cacheDirectory);
                if (downloaded != null)
                {
                    Log.VerboseFormat("Package was found in cache. No need to download. Using file: '{0}'", downloaded.FullFilePath);
                    return downloaded;
                }
            }

            return DownloadPackage(packageId, version, feedUri, feedCredentials, cacheDirectory, maxDownloadAttempts, downloadAttemptBackoff);
        }


        private void SplitPackageId(string packageId, out string owner, out string repo)
        {
            var parts = packageId.Split(OwnerRepoSeperator);
            if (parts.Length > 1)
            {
                owner = parts[0];
                repo = parts[1];
            }
            else
            {
                owner = null;
                repo = packageId;
            }
        }


        PackagePhysicalFileMetadata SourceFromCache(string packageId, IVersion version, string cacheDirectory)
        {
            Log.VerboseFormat("Checking package cache for package {0} v{1}", packageId, version.ToString());

            var files = fileSystem.EnumerateFilesRecursively(cacheDirectory, PackageName.ToSearchPatterns(packageId, version, new [] { Extension }));

            foreach (var file in files)
            {
                var package = PackageName.FromFile(file);
                if (package == null)
                    continue;

                if (string.Equals(package.PackageId, packageId, StringComparison.OrdinalIgnoreCase) && package.Version.Equals(version))
                {
                    return PackagePhysicalFileMetadata.Build(file, package);
                }
            }

            return null;
        }


        PackagePhysicalFileMetadata DownloadPackage(
            string packageId,
            IVersion version,
            Uri feedUri,
            ICredentials feedCredentials,
            string cacheDirectory,
            int maxDownloadAttempts,
            TimeSpan downloadAttemptBackoff)
        {
            Log.Info("Downloading GitHub package {0} v{1} from feed: '{2}'", packageId, version, feedUri);
            Log.VerboseFormat("Downloaded package will be stored in: '{0}'", cacheDirectory);
            fileSystem.EnsureDirectoryExists(cacheDirectory);
            fileSystem.EnsureDiskHasEnoughFreeSpace(cacheDirectory);

            SplitPackageId(packageId, out string owner, out string repository);
            if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repository))
            {
                throw new InvalidOperationException(
                    "Invalid PackageId for GitHub feed. Expecting format `<owner>/<repo>`");
            }
            
            var page = 0;
            JArray req = null;
            while (req == null || (req.Count != 0 &&  req.Count < 1000))
            {
                var uri = feedUri.AbsoluteUri + $"repos/{Uri.EscapeUriString(owner)}/{Uri.EscapeUriString(repository)}/tags?page={++page}&per_page=1000";
                req = PerformRequest(feedCredentials, uri) as JArray;
                if (req == null)
                {
                    break;
                }

                foreach (var tag in req)
                {
                    if (!TryParseVersion((string) tag["name"], out IVersion v) || !version.Equals(v)) continue;

                    var zipball = (string) tag["zipball_url"];
                    return DownloadFile(zipball, cacheDirectory, packageId, version, feedCredentials, maxDownloadAttempts, downloadAttemptBackoff);
                }
            }

            throw new Exception("Unable to find package {0} v{1} from feed: '{2}'");
        }


        static bool TryParseVersion(string input, out IVersion version)
        {
            if (input == null)
            {
                version = null;
                return false;
            }

            if (input[0].Equals('v') || input[0].Equals('V'))
            {
                input = input.Substring(1);
            }

            return VersionFactory.TryCreateVersion(input, out version, VersionFormat.Semver);
        }

        PackagePhysicalFileMetadata DownloadFile(string uri, string cacheDirectory, string packageId, IVersion version,
            ICredentials feedCredentials, int maxDownloadAttempts,
            TimeSpan downloadAttemptBackoff)
        {
            var localDownloadName =
                Path.Combine(cacheDirectory, PackageName.ToCachedFileName(packageId, version, Extension));

            var tempPath = Path.GetTempFileName();
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            for (var retry = 0; retry < maxDownloadAttempts; ++retry)
            {
                try
                {
                    if (retry != 0) Log.Verbose($"Download Attempt #{retry + 1}");

                    using (var client = new WebClient())
                    {
                        client.CachePolicy = new RequestCachePolicy(RequestCacheLevel.CacheIfAvailable);
                        client.Headers.Set(HttpRequestHeader.UserAgent, GetUserAgent());
                        client.Credentials = feedCredentials;
                        client.DownloadFileWithProgress(uri, tempPath, (progress, total) => Log.ServiceMessages.Progress(progress, $"Downloading {packageId} v{version}"));

                        DeNestContents(tempPath, localDownloadName);
                        return PackagePhysicalFileMetadata.Build(localDownloadName);
                    }
                }
                catch (WebException)
                {
                    Thread.Sleep(downloadAttemptBackoff);
                }
            }

            throw new Exception("Failed to download the package.");
        }

        JToken PerformRequest(ICredentials feedCredentials, string uri)
        {
            try
            {
                using (var client = new WebClient())
                {
                    client.CachePolicy = new RequestCachePolicy(RequestCacheLevel.BypassCache);
                    client.Headers.Set(HttpRequestHeader.UserAgent, GetUserAgent());
                    client.Headers.Set("Accept", "application/vnd.github.v3+json");
                    client.Credentials = feedCredentials;
                    using (var readStream = client.OpenRead(uri))
                    {
                        var reader =
                            new JsonTextReader(new StreamReader(readStream ?? throw new InvalidOperationException()));
                        return JToken.Load(reader);
                    }
                }
            }
            catch (WebException ex) when (ex.Response is HttpWebResponse response)
            {
                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    VerifyRateLimit(response);
                }
                else if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new Exception($"Failed to authenticate GitHub request");
                }
                else if ((int)response.StatusCode == 422) //Unprocessable Entity
                {
                    throw new Exception($"Error performing request");
                }

                throw;
            }
        }

        void VerifyRateLimit(HttpWebResponse response)
        {
            var remainingRequests = response.Headers.GetValues("X-RateLimit-Remaining").FirstOrDefault();
            if (remainingRequests == "0")
            {
                int secondsToWait = -1;
                if (int.TryParse(response.Headers.GetValues("X-RateLimit-Reset").FirstOrDefault(), out int reset))
                {
                    TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
                    secondsToWait = reset - (int)t.TotalSeconds;
                }
                
                throw new Exception($"GitHub request rate limit has been hit. Try operation again in  {secondsToWait} seconds. " +
                                    $"Unauthenticated users can perform 10 search requests and 60 non search HTTP requests to GitHub per minute per IP address." +
                                    $"It is reccomended that you provide credentials to increase this limit.");
            }
        }

        string GetUserAgent()
        {
            return $"OctopusDeploy-Calamari/{GetType().Assembly.GetInformationalVersion()}";
        }

        /// <summary>
        /// Takes files from the root inner directory, and moves down to root.
        /// Currently only relevent for Git archives.
        ///  e.g. /Dir/MyFile => /MyFile
        /// This was created indpependantly from the version above since Calamari needs to support .net 4.0 here which does not have `System.IO.Compression` library.
        /// The reason this library is preferred over `SharpCompress` is that it can update zips in-place and it preserves empty directories.
        /// https://github.com/adamhathcock/sharpcompress/issues/62
        /// https://github.com/adamhathcock/sharpcompress/issues/34
        /// https://github.com/adamhathcock/sharpcompress/issues/242
        /// Unfortunately the server needs the same logic so that we can ensure that the Hash comparisons match.
        /// </summary>
        /// <param name="fileName"></param>
        static void DeNestContents(string src, string dest)
        {
            int rootPathSeperator = -1;
            using (var readerStram = File.Open(src, FileMode.Open, FileAccess.ReadWrite))
            using (var reader = ReaderFactory.Open(readerStram))
            {
                using (var writerStream = File.Open(dest, FileMode.CreateNew, FileAccess.ReadWrite))
                using (var writer = WriterFactory.Open(writerStream, ArchiveType.Zip, new ZipWriterOptions(CompressionType.Deflate)))
                {
                    while (reader.MoveToNextEntry())
                    {
                        var entry = reader.Entry;
                        if (!reader.Entry.IsDirectory)
                        {
                            if (rootPathSeperator == -1)
                            {
                                rootPathSeperator = entry.Key.IndexOf('/') + 1;
                            }

                            try
                            {
                                var newFilePath = entry.Key.Substring(rootPathSeperator);
                                if (newFilePath != String.Empty)
                                {
                                    writer.Write(newFilePath, reader.OpenEntryStream());
                                }
                            }
                            catch (Exception)
                            {

                            }
                        }
                    }
                }
            }
        }
    }

    


}
