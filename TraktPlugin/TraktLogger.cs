﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using MediaPortal.Configuration;

namespace TraktPlugin
{
    public static class TraktLogger
    {
        private static string logFilename = Config.GetFile(Config.Dir.Log,"TraktPlugin.log");
        private static string backupFilename = Config.GetFile(Config.Dir.Log, "TraktPlugin.bak");

        static TraktLogger()
        {
            // default logging before we load settings
            TraktSettings.LogLevel = 2;

            if (File.Exists(logFilename))
            {
                if (File.Exists(backupFilename))
                {
                    try
                    {
                        File.Delete(backupFilename);
                    }
                    catch
                    {
                        Error("Failed to remove old backup log");
                    }
                }
                try
                {
                    File.Move(logFilename, backupFilename);
                }
                catch
                {
                    Error("Failed to move logfile to backup");
                }
            }    
        }

        public static void Info(String log)
        {
            if(TraktSettings.LogLevel >= 2)
                writeToFile(String.Format(createPrefix(), "Info", log));
        }

        public static void Info(String format, params Object[] args)
        {
            Info(String.Format(format, args));
        }

        public static void Info(String format, params String[] args)
        {
            Info(String.Format(format, args));
        }

        public static void Debug(String log)
        {
            if(TraktSettings.LogLevel >= 3)
                writeToFile(String.Format(createPrefix(), "Debug", log));
        }

        public static void Debug(String format, params String[] args)
        {
            Debug(String.Format(format, args));
        }

        public static void Error(String log)
        {
            if(TraktSettings.LogLevel >= 0)
                writeToFile(String.Format(createPrefix(), "Error", log));
        }

        public static void Error(String format, params String[] args)
        {
            Error(String.Format(format, args));
        }

        public static void Warning(String log)
        {
            if(TraktSettings.LogLevel >= 1)
                writeToFile(String.Format(createPrefix(), "Warning", log));
        }

        public static void Warning(String format, params String[] args)
        {
            Warning(String.Format(format, args));
        }

        private static String createPrefix()
        {
            return DateTime.Now + String.Format("[{0}][{1}]", Thread.CurrentThread.Name, Thread.CurrentThread.ManagedThreadId) +  "[{0}] {1}";
        }

        private static void writeToFile(String log)
        {
            try
            {
                StreamWriter sw = File.AppendText(logFilename);
                sw.WriteLine(log);
                sw.Close();
            }
            catch
            {
                Error("Failed to write out to log");
            }
        }
    }
}
