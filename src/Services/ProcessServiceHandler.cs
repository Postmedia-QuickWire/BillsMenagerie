using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Common.Services
{

    public interface IProcessServiceHandler
    {
        public bool StartService();
        public Task<bool> StopServiceAsync();

        public Task<bool> RestartServiceAsync();

        public bool StopService();
        public bool RestartService();

        public bool IsServiceRunning { get; }

        public void ReportMemoryUsage(); //log
        public long GetPrivateMemoryUsage();
        public long GetMemoryUsage();

        //public ProcessServiceHandlerConfig Config { get; }
    }

    public class ProcessServiceHandlerConfig
    {
        /// <summary>
        /// The path to the executable that will be run as a service.
        /// </summary>
        public string Executable { get; set; }

        /// <summary>
        /// The command-line parameters to pass to the executable when starting the service.
        /// </summary>
        public string Parameters { get; set; }

        /// <summary>
        /// The working directory for the service process. This is where the process will be started from.
        /// </summary>
        public string WorkingDirectory { get; set; }

        /// <summary>
        /// The name of the logger to use for logging messages from the service handler. If not specified, the default logger will be used.
        /// </summary>
        public string LoggerName { get; set; }

        /// <summary>
        /// A unique key to identify the service. This is for using the same IProcessServiceHandler interface for multiple services.
        /// reference service using [FromKeyedServices("ServiceKey")] IProcessServiceHandler attribute in ctor
        /// </summary>
        public string ServiceKey { get; set; }

        /// <summary>
        /// A dictionary of environment variables to set for the service process. The key is the variable name, and the value is the variable value.
        /// </summary>
        public Dictionary<string, string> EnvironmentVariables { get; set; } = new Dictionary<string, string>();
    }

    public static class ProcessServiceHandlerExtensions
    {
        public static IServiceCollection AddProcessServiceHandler(
             this IServiceCollection services, Action<ProcessServiceHandlerConfig> setconf)
        {
            var config = new ProcessServiceHandlerConfig();
            setconf(config);

            if (String.IsNullOrEmpty(config.ServiceKey))
            {
                services.AddSingleton<IProcessServiceHandler>(x =>
                {
                    return new ProcessServiceHandler(config, x.GetRequiredService<ILoggerFactory>());
                });
            }
            else
            {
                services.AddKeyedSingleton<IProcessServiceHandler>(config.ServiceKey, (x,c) =>
                {
                    return new ProcessServiceHandler(config, x.GetRequiredService<ILoggerFactory>());
                });
            }

            return services;
        }


        // good if we have a object that inherits from ProcessServiceHandler and we want to register it as a singleton (keyed)
        public static IServiceCollection AddProcessServiceHandler(this IServiceCollection services, ProcessServiceHandler handler)
        {

            if (String.IsNullOrEmpty(handler.Config?.ServiceKey))
            {
                services.AddSingleton<IProcessServiceHandler>(handler);
            }
            else
            {
                services.AddKeyedSingleton<IProcessServiceHandler>(handler.Config.ServiceKey, handler);
            }

            return services;
        }

        public static IServiceCollection AddProcessServiceHandler<T>(this IServiceCollection services, string ServiceKey) where T : ProcessServiceHandler
        {

            if (String.IsNullOrEmpty(ServiceKey))
            {
                services.AddSingleton<IProcessServiceHandler, T> ();
            }
            else
            {
                services.AddKeyedSingleton<IProcessServiceHandler, T>(ServiceKey);
            }

            return services;
        }

    }

    public class ProcessServiceHandler : IProcessServiceHandler
    {
        protected ProcessServiceHandlerConfig _config;
        protected ILogger _logger;
        protected Process _process;
        protected string _logName;
        private readonly ILoggerFactory _loggerfactory;

        public ProcessServiceHandlerConfig Config => _config;

        // set true when we call StopService so we can expect the exit event
        protected bool StopServiceCalled { get; set; } = false;

        // used for classes that inherit from this class and want to set the config in their constructor
        public ProcessServiceHandler(ILoggerFactory loggerfactory)
        {
            _loggerfactory = loggerfactory;
        }

        public ProcessServiceHandler(ProcessServiceHandlerConfig config, ILoggerFactory loggerfactory)
        {
            _loggerfactory = loggerfactory;
            SetConfig(config);
        }

        public virtual void SetConfig(ProcessServiceHandlerConfig config)
        {
            _config = config;
            if (!string.IsNullOrEmpty(config.LoggerName))
            {
                _logName = config.LoggerName;
                _logger = _loggerfactory.CreateLogger(config.LoggerName);
            }
            else
            {
                _logName = nameof(ProcessServiceHandler);
                _logger = _loggerfactory.CreateLogger<ProcessServiceHandler>();
            }

            InitializeProcess();
        }

        // MUST set the _config before calling this method, otherwise it will throw an exception
        protected void InitializeProcess()
        {
            ArgumentNullException.ThrowIfNull(_config);
            _process = new Process();
            _process.StartInfo.FileName = _config.Executable;
            _process.StartInfo.Arguments = _config.Parameters;
            _process.StartInfo.WorkingDirectory = _config.WorkingDirectory;
            _process.StartInfo.UseShellExecute = false;
            _process.StartInfo.RedirectStandardOutput = true;
            _process.StartInfo.RedirectStandardError = true;
            _process.StartInfo.CreateNoWindow = true;
            if(_config.EnvironmentVariables != null)
            {
                foreach (var kvp in _config.EnvironmentVariables)
                {
                    _process.StartInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
                }
            }
            _process.OutputDataReceived += (sender, args) => LogStdOut(args.Data);
            _process.ErrorDataReceived += (sender, args) => LogStdError(args.Data);

            _process.EnableRaisingEvents = true;
            _process.Exited += ProcessExited;
        }
    
        protected virtual void ProcessExited(object sender, EventArgs e)
        {
            // ah do something - just not sure
            _logger.LogInformation("Process exited: {0}", _process?.ExitCode);

        }

        protected virtual void LogStdOut(string data)
        {
            if (!string.IsNullOrEmpty(data))
            {
                _logger.LogInformation(data);
            }
        }

        protected virtual void LogStdError(string data)
        {
            if (!string.IsNullOrEmpty(data))
            {
                _logger.LogError(data);
            }
        }

        public void ReportMemoryUsage()
        {
            // Refresh the cached metrics
            _process.Refresh(); 

            long bytesUsed = _process.WorkingSet64;
            double mbUsed = bytesUsed / (1024.0 * 1024.0);

            _logger.LogInformation("Physical Memory (Working Set): {mbUsed:F2} MB", mbUsed);
            _logger.LogInformation("Private Memory: {pmem:F2} MB", _process.PrivateMemorySize64 / (1024.0 * 1024.0));
        }

        public long GetMemoryUsage()
        {
            try
            {
                _process.Refresh(); // Refresh the cached metrics
                return _process.WorkingSet64;
            }
            catch (Exception e)
            {
                _logger.LogError("Error occurred while getting memory usage for service: {srv}, {e}", _logName, e.Message);
                return 0;
            }
        }

        public long GetPrivateMemoryUsage()
        {
            try
            {
                _process.Refresh(); // Refresh the cached metrics
                return _process.PrivateMemorySize64;
            }
            catch (Exception e)
            {
                _logger.LogError("Error occurred while getting private memory usage for service: {srv}, {e}", _logName, e.Message);
                return 0;
            }
        }

        public bool IsServiceRunning
        {
            get
            {
                try
                {
                     return _process?.HasExited == false;
                }
                catch (Exception){ 
                }
                return false;
            }
        }

        public async Task<bool> RestartServiceAsync()
        {
            _logger.LogInformation("Restarting service {srv}...", _logName);
            if (IsServiceRunning)
            {
                await StopServiceAsync();
            }
            return StartService();
        }

        public bool RestartService()
        {
            _logger.LogInformation("Restarting service {srv}...", _logName);
            if (IsServiceRunning)
            {
                StopService();
            }

            return StartService();
        }

        public bool StartService()
        {
            _logger.LogInformation("Starting service {srv}...", _logName);
            if (!IsServiceRunning)
            {
                StopServiceCalled = false;
                try
                {
                    InitializeProcess();
                    //_process.Refresh();
                    _logger.LogInformation("Starting Service, {exe}", _config.Executable);
                    bool ok = _process.Start();
                    if (ok)
                    {
                        _process.BeginOutputReadLine();
                        _process.BeginErrorReadLine();
                        _logger.LogDebug("Service started successfully: {srv}", _logName);
                    }
                    else
                    {
                        _logger.LogError("Failed to start service: {srv}", _logName);
                    }
                }
                catch(Exception e)
                {
                    _logger.LogError(e, "Error occurred while starting service: {srv}, {e}", _logName, e.Message);
                }
            }
            return true;
        }

        public async Task<bool> StopServiceAsync()
        {
            if (IsServiceRunning)
            {
                StopServiceCalled = true;
                _process.Kill();
                await _process.WaitForExitAsync();
            }
            return true;
        }

        public bool StopService()
        {
            if (IsServiceRunning)
            {
                StopServiceCalled = true;
                _process.Kill();
                _process.WaitForExit();
            }
            return true;
        }


        public static void KillProcess(string processName)
        {
            Process[] localByName = Process.GetProcessesByName(processName);

            foreach (Process p in localByName)
            {
                try
                {
                    p.Kill();
                    p.WaitForExit(60000); // Wait for the process to fully exit
                }
                catch (Exception ex)
                {
                    throw new Exception($"KillProcesses: Error killing process: {ex.Message}");
                }
            }
        }

    }
}
