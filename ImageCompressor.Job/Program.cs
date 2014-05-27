﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AzureJobs.Common;

namespace ImageCompressor.Job
{
    class Program
    {
        private static string _folder = @"D:\home\site\wwwroot\";
        private static string[] _filters = { "*.png", "*.jpg", "*.jpeg", "*.gif" };
        private static ImageCompressor _compressor = new ImageCompressor();
        private static Dictionary<string, DateTime> _cache = new Dictionary<string, DateTime>();
        private static Logger _log;
        private static FileHashStore _store;
        private static object _syncRoot = new object();
        private static bool _isProcessing;

        static void Main(string[] args)
        {
            //_folder = @"C:\Users\madsk\Documents\GitHub\AzureJobs\Azurejobs.Web\ImageOptimization\img";
            _log = new Logger(Path.Combine(_folder, "app_data"));
            _store = new FileHashStore(Path.Combine(_folder, "app_data\\ImageOptimizerHashTable.xml"), _log);
            _compressor.Finished += WriteToLog;
          
            QueueExistingFiles();
            ProcessQueue();
            StartListener();

            Timer timer = new Timer((o) => ProcessQueue());
            timer.Change(1000, 5000);

            while (true)
            {
                Thread.Sleep(2000);
            }
        }

        private static void QueueExistingFiles()
        {
            foreach (string filter in _filters)
                foreach (string file in Directory.EnumerateFiles(_folder, filter, SearchOption.AllDirectories))
                {
                    AddToQueue(file, DateTime.MinValue);
                }
        }

        private static void AddToQueue(string file, DateTime date)
        {
            _cache[file] = date;
        }

        public static void StartListener()
        {
            foreach (string filter in _filters)
            {
                FileSystemWatcher w = new FileSystemWatcher(_folder);
                w.Filter = filter;
                w.IncludeSubdirectories = true;
                w.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime;
                w.Changed += (s, e) => AddToQueue(e.FullPath, DateTime.Now);
                w.EnableRaisingEvents = true;
            }
        }

        private static void ProcessQueue()
        {
            if (_isProcessing)
                return;

            _isProcessing = true;
            int length = _cache.Count - 1;

            for (int i = length; i >= 0; i--)
            {
                if (_cache.Count < i)
                    continue;

                var entry = _cache.ElementAt(i);

                try
                {
                    // The file should be a second old before we start processing
                    if (entry.Value > DateTime.Now.AddSeconds(-2))
                        continue;

                    if (!_store.HasChangedOrIsNew(entry.Key))
                    {
                        _cache.Remove(entry.Key);
                        continue;
                    }

                    _compressor.CompressFile(entry.Key);
                    _cache.Remove(entry.Key);
                }
                catch (IOException)
                {
                    // do nothing. We'll try again
                }
                catch
                {
                    _cache.Remove(entry.Key);
                }
            }

            _isProcessing = false;
        }

        private static void WriteToLog(object sender, CompressionResult e)
        {
            _store.Save(e.OriginalFileName);

            if (e == null || e.ResultFileSize == 0)
                return;

            string name = new Uri(_folder).MakeRelativeUri(new Uri(e.OriginalFileName)).ToString();
            _log.Write(DateTime.Now, name, e.OriginalFileSize, Math.Min(e.ResultFileSize, e.OriginalFileSize));
        }
    }
}