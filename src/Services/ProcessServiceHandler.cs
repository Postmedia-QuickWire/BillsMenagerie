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
    }

    public class ProcessServiceHandler : IProcessServiceHandler
    {
        private readonly ProcessServiceHandlerConfig _config;
        private readonly ILogger _logger;
        private Process _process;
        private string _logName;
        //private readonly string _serviceKey;
        public ProcessServiceHandler(ProcessServiceHandlerConfig config, ILoggerFactory loggerfactory)
        {
            _config = config;
            if (!string.IsNullOrEmpty(config.LoggerName))
            {
                _logName = config.LoggerName;
                _logger = loggerfactory.CreateLogger(config.LoggerName);
            }
            else
            {
                _logName = nameof(ProcessServiceHandler);
                _logger = loggerfactory.CreateLogger<ProcessServiceHandler>();
            }

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

            _process.Exited += ProcessExited;
        }

        private void ProcessExited(object sender, EventArgs e)
        {
            // ah do something - just not sure
            _logger.LogInformation("Process exited: {0}", _process.ExitCode);
        }

        private void LogStdOut(string data)
        {
            if (!string.IsNullOrEmpty(data))
            {
                _logger.LogInformation(data);
            }
        }

        private void LogStdError(string data)
        {
            if (!string.IsNullOrEmpty(data))
            {
                _logger.LogError(data);
            }
        }

        public bool IsServiceRunning
        {
            get
            {
                try
                {
                    return _process.HasExited == false;
                }
                catch (Exception e){ 
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
                try
                {
                    _process.Refresh();
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
                _process.Kill();
                await _process.WaitForExitAsync();
            }
            return true;
        }

        public bool StopService()
        {
            if (IsServiceRunning)
            {
                _process.Kill();
                _process.WaitForExit();
            }
            return true;
        }
    }
}
