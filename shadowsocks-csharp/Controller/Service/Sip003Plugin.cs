﻿using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Shadowsocks.Model;
using Shadowsocks.Util.ProcessManagement;

namespace Shadowsocks.Controller.Service
{
    // https://github.com/shadowsocks/shadowsocks-org/wiki/Plugin
    public sealed class Sip003Plugin : IDisposable
    {
        public IPEndPoint LocalEndPoint { get; private set; }
        public int ProcessId => _started ? _pluginProcess.Id : 0;

        private readonly object _startProcessLock = new object();
        private readonly Job _pluginJob;
        private readonly Process _pluginProcess;
        private bool _started;
        private bool _disposed;

        public static Sip003Plugin CreateIfConfigured(Server server)
        {
            if (server == null)
            {
                throw new ArgumentNullException(nameof(server));
            }

            if (string.IsNullOrWhiteSpace(server.plugin))
            {
                return null;
            }

            return new Sip003Plugin(server.plugin, server.plugin_opts);
        }

        private Sip003Plugin(string plugin, string pluginOpts)
        {
            if (plugin == null) throw new ArgumentNullException(nameof(plugin));

            var appPath = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().GetName().CodeBase).LocalPath);

            _pluginProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = plugin,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    ErrorDialog = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = appPath ?? Environment.CurrentDirectory,
                    Arguments = pluginOpts
                }
            };

            _pluginJob = new Job();
        }

        public bool StartIfNeeded()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            lock (_startProcessLock)
            {
                if (_started && !_pluginProcess.HasExited)
                {
                    return false;
                }

                _pluginProcess.Start();
                _pluginJob.AddProcess(_pluginProcess.Handle);
                _started = true;
            }

            return true;
        }

        static int GetNextFreeTcpPort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                if (!_pluginProcess.HasExited)
                {
                    _pluginProcess.Kill();
                    _pluginProcess.WaitForExit();
                }
            }
            catch (Exception) { }
            finally
            {
                try
                {
                    _pluginProcess.Dispose();
                    _pluginJob.Dispose();
                }
                catch (Exception) { }

                _disposed = true;
            }
        }
    }
}