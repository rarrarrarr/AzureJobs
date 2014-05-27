﻿using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace AzureJobs.Common
{
    public class Logger
    {
        private string _logFile;
        private static object _syncRoot = new object();

        public Logger(string path)
        {
            _logFile = GetLogFilePath(path);
        }

        public void Write(params object[] messages)
        {
            string messageString = string.Join(", ", messages);
            Trace.WriteLine(messageString);
            Console.WriteLine(messageString);

            try
            {
                lock (_syncRoot)
                {
                    File.AppendAllText(_logFile, Environment.NewLine + messageString);
                }
            }
            catch
            {
                // Do nothing
            }
        }

        private string GetLogFilePath(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            var entry = Assembly.GetEntryAssembly();
            string name = entry.ManifestModule.Name;
            string logFile = Path.Combine(path, name + ".csv");

            if (!File.Exists(logFile))
                File.WriteAllText(logFile, "Date, Filename, Original, Optimized");

            return logFile;
        }
    }
}