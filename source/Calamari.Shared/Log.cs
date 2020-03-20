﻿using System;
using System.Globalization;
using System.Text;
using Calamari.Integration.Processes;
using Octopus.Versioning;
using Octostache;

namespace Calamari
{
    public interface ILog
    {
        void Verbose(string message);
        void VerboseFormat(string messageFormat, params object[] args);
        void Info(string message);
        void InfoFormat(string messageFormat, params object[] args);
        void Warn(string message);
        void WarnFormat(string messageFormat, params object[] args);
        void Error(string message);
        void ErrorFormat(string messageFormat, params object[] args);
        void SetOutputVariableButDoNotAddToVariables(string name, string value, bool isSensitive = false);
        void SetOutputVariable(string name, string value, IVariables variables, bool isSensitive = false);
        void NewOctopusArtifact(string fullPath, string name, long fileLength);

        void PackageFound(string packageId, IVersion packageVersion, string packageHash,
            string packageFileExtension, string packageFullPath, bool exactMatchExists = false);

        void Progress(int percentage, string message);
        void DeltaVerification(string remotePath, string hash, long size);
        void DeltaVerificationError(string error);
        string FormatLink(string uri, string description = null);
    }
    
    public static class Log
    {
        public static void Verbose(string message)
            => ConsoleLog.Instance.Verbose(message);

        public static void VerboseFormat(string message, params object[] args)
            => ConsoleLog.Instance.VerboseFormat(message, args);

        public static void Info(string message)
            => ConsoleLog.Instance.Info(message);

        public static void Info(string message, params object[] args)
            => ConsoleLog.Instance.InfoFormat(message, args);

        public static void Warn(string message)
            => ConsoleLog.Instance.Warn(message);

        public static void WarnFormat(string message, params object[] args)
            => ConsoleLog.Instance.WarnFormat(message, args);

        public static void Error(string message)
            => ConsoleLog.Instance.Error(message);

        public static void ErrorFormat(string message, params object[] args)
            => ConsoleLog.Instance.ErrorFormat(message, args);
        
        public static void SetOutputVariable(string name, string value, IVariables variables, bool isSensitive = false)
            => ConsoleLog.Instance.SetOutputVariable(name, value, variables, isSensitive);

    }
    
    public class ConsoleLog : AbstractLog
    {
        readonly IndentedTextWriter stdOut;
        readonly IndentedTextWriter stdErr;
        
        public static ConsoleLog Instance = new ConsoleLog();

        ConsoleLog()
        {
            stdOut = new IndentedTextWriter(Console.Out, "  ");
            stdErr = new IndentedTextWriter(Console.Error, "  ");
        }

        protected override void StdOut(string message)
            => stdOut.WriteLine(message);

        protected override void StdErr(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            stdErr.WriteLine(message);
            Console.ResetColor();
        }
    }

    public abstract class AbstractLog : ILog
    {
        string stdOutMode;

        readonly object sync = new object();

        protected abstract void StdOut(string message);
        protected abstract void StdErr(string message);

        void SetMode(string mode)
        {
            if (stdOutMode == mode) return;
            StdOut("##octopus[stdout-" + mode + "]");
            stdOutMode = mode;
        }

        public virtual void Verbose(string message)
        {
            lock (sync)
            {
                SetMode("verbose");
                StdOut(message);
            }
        }

        public virtual void VerboseFormat(string messageFormat, params object[] args)
            => Verbose(string.Format(messageFormat, args));

        public virtual void Info(string message)
        {
            lock (sync)
            {
                SetMode("default");
                StdOut(message);
            }
        }

        public virtual void InfoFormat(string messageFormat, params object[] args)
            => Info(String.Format(messageFormat, args));

        public virtual void Warn(string message)
        {
            lock (sync)
            {
                SetMode("warning");
                StdOut(message);
            }
        }

        public virtual void WarnFormat(string messageFormat, params object[] args)
            => Warn(String.Format(messageFormat, args));

        public virtual void Error(string message)
        {
            lock (sync)
                StdErr(message);
        }

        public virtual void ErrorFormat(string messageFormat, params object[] args)
            => Error(string.Format(messageFormat, args));
        
        
        public void SetOutputVariableButDoNotAddToVariables(string name, string value, bool isSensitive = false)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (value == null) throw new ArgumentNullException(nameof(value));

            Info(isSensitive
                ? $"##octopus[setVariable name=\"{ConvertServiceMessageValue(name)}\" value=\"{ConvertServiceMessageValue(value)}\" sensitive=\"{ConvertServiceMessageValue(Boolean.TrueString)}\"]"
                : $"##octopus[setVariable name=\"{ConvertServiceMessageValue(name)}\" value=\"{ConvertServiceMessageValue(value)}\"]");
        }

        public void SetOutputVariable(string name, string value, IVariables variables, bool isSensitive = false)
        {
            SetOutputVariableButDoNotAddToVariables(name, value, isSensitive);
            variables?.SetOutputVariable(name, value);
        }

        public void NewOctopusArtifact(string fullPath, string name, long fileLength)
        {
            Info($"##octopus[createArtifact path=\"{ConvertServiceMessageValue(fullPath)}\" name=\"{ConvertServiceMessageValue(name)}\" length=\"{ConvertServiceMessageValue(fileLength.ToString())}\"]");
        }

        public void PackageFound(string packageId, IVersion packageVersion, string packageHash,
            string packageFileExtension, string packageFullPath, bool exactMatchExists = false)
        {
            if (exactMatchExists)
                Verbose("##octopus[calamari-found-package]");

            VerboseFormat("##octopus[foundPackage id=\"{0}\" version=\"{1}\" versionFormat=\"{2}\" hash=\"{3}\" remotePath=\"{4}\" fileExtension=\"{5}\"]",
                ConvertServiceMessageValue(packageId),
                ConvertServiceMessageValue(packageVersion.ToString()),
                ConvertServiceMessageValue(packageVersion.Format.ToString()),
                ConvertServiceMessageValue(packageHash),
                ConvertServiceMessageValue(packageFullPath),
                ConvertServiceMessageValue(packageFileExtension));
        }

        public void Progress(int percentage, string message)
        {
            VerboseFormat("##octopus[progress percentage=\"{0}\" message=\"{1}\"]",
                ConvertServiceMessageValue(percentage.ToString(CultureInfo.InvariantCulture)),
                ConvertServiceMessageValue(message));
        }

        public void DeltaVerification(string remotePath, string hash, long size)
        {
            VerboseFormat("##octopus[deltaVerification remotePath=\"{0}\" hash=\"{1}\" size=\"{2}\"]",
                ConvertServiceMessageValue(remotePath),
                ConvertServiceMessageValue(hash),
                ConvertServiceMessageValue(size.ToString(CultureInfo.InvariantCulture)));
        }

        public void DeltaVerificationError(string error)
        {
            VerboseFormat("##octopus[deltaVerification error=\"{0}\"]", ConvertServiceMessageValue(error));
        }

        public string FormatLink(string uri, string description = null)
            => $"[{description ?? uri}]({uri})";
        
        static string ConvertServiceMessageValue(string value)
            => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    }
}