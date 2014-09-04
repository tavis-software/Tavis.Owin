using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Owin.Host.HttpListener;
using Microsoft.Owin.Logging;
using Topshelf;
using Topshelf.HostConfigurators;
using Topshelf.Logging;
using Topshelf.Runtime;
using TraceFactoryDelegate = System.Func<string, System.Func<
    System.Diagnostics.TraceEventType,
    int,
    object,
    System.Exception, 
    System.Func<object,System.Exception,string>,
    bool
>
>;

namespace Tavis.Owin
{
    public class OwinServiceHost : ServiceControl
    {

        public string ServiceDescription { get; set; }
        public string ServiceDisplayName { get; set; }
        public string ServiceName { get; set; }

        private  Uri _HostUrl;
        private readonly Func<IDictionary<string, object>, Task> _appFunc;
        private static readonly LogWriter _log = HostLogger.Get<OwinServiceHost>();
        private IDisposable _server;
        
        
        /// <summary>
        /// Create an OwinServiceHost
        /// </summary>
        /// <param name="defaultUrl">Currently only one URI.  Ideally multiple should be supported.</param>
        /// <param name="appFunc"></param>
        public OwinServiceHost(Uri defaultUrl, Func<IDictionary<string,object>, Task> appFunc )
        {
            _HostUrl = defaultUrl;
            _appFunc = appFunc;
            
        }

        /// <summary>
        /// Call this method to initialize and run the service
        /// </summary>
        /// <param name="extraConfig"></param>
        /// <returns></returns>
        public TopshelfExitCode Initialize(Action<HostConfigurator> extraConfig = null)
        {
            return HostFactory.Run(configurator =>
            {
                Configure(configurator);
                if (extraConfig != null) extraConfig(configurator);
            });
        }

        private void Configure(HostConfigurator x)
        {
            //HostLogger.UseLogger(new TraceHostLoggerConfigurator());  Do this outside the class
            x.Service(settings => this);
            
            x.BeforeInstall(BeforeInstall);
            x.AfterUninstall(AfterUnInstall);
            
            x.RunAsNetworkService();  // Ideally this should be configurable

            // I'm debating whether to pick this up from attributes of the executing assembly, or be explicit about it.
            if (ServiceDescription != null) x.SetDescription(ServiceDescription);
            if (ServiceDisplayName != null) x.SetDisplayName(ServiceDisplayName);
            x.SetServiceName(ServiceName);
        }

        bool ServiceControl.Start(HostControl hostControl)
        {
            LoadConfig();
            _log.Info("Creating HTTP listener at " + _HostUrl.AbsoluteUri);
            _server = CreateHttpListenerServer(new List<Uri>() { _HostUrl }, _appFunc,  TopShelfKatanaLoggerAdapter.CreateOwinDelegate(_log));
            return _server != null;
        }

        bool ServiceControl.Stop(HostControl hostControl)
        {
            _server.Dispose();
            _server = null;
            return true;
        }
       

        private void BeforeInstall()
        {
            Console.Write("Enter host URL [{0}]:",_HostUrl);
            var uri = AskForUri();
            if (uri != null)
            {
                _HostUrl = uri;
                StoreConfig();
            }
            AddUrlAcl(_HostUrl);

        }

        private void AfterUnInstall()
        {
            LoadConfig();
            DeleteAcl(_HostUrl);
        }

        private void AddUrlAcl(Uri uri)
        {
            _log.Info("Adding Url ACL for Network Service for " + uri.AbsoluteUri);
            var process = Process.Start(Environment.ExpandEnvironmentVariables("%SystemRoot%\\System32\\netsh.exe"),
                String.Format("http add urlacl url={0} user=\"NT AUTHORITY\\NetworkService\"", uri.OriginalString));
            process.WaitForExit();
            _log.Debug("Completed with exit code " + process.ExitCode);
  
        }

        private void StoreConfig()
        {
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var host = config.AppSettings.Settings["Host"];
            if (host != null)
            {
                host.Value = _HostUrl.AbsoluteUri;
            }
            else
            {
                config.AppSettings.Settings.Add("Host",_HostUrl.AbsoluteUri);
            }
            _log.Info("Saving selected URL to configuration file");
            config.Save(ConfigurationSaveMode.Modified);
            
        }

        private void LoadConfig()
        {
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var hostUrl = config.AppSettings.Settings["Host"];
            if (hostUrl != null) _HostUrl = new Uri(hostUrl.Value);  // Override default URL if one is stored
        }

        private Uri AskForUri()
        {
            Uri newUri = null;
            bool tryAgain = false;
            do
            {
                var inputUrl = Console.ReadLine();
                if (Uri.IsWellFormedUriString(inputUrl, UriKind.Absolute))
                {
                    inputUrl += inputUrl.EndsWith("/") ? "" : "/";

                    newUri = new Uri(inputUrl);
                }
                else
                {
                    if (!String.IsNullOrWhiteSpace(inputUrl))
                    {
                        tryAgain = true;
                    }
                }
            } while (tryAgain);

            return newUri;
        }

        private void DeleteAcl(Uri uri)
        {
            _log.Info("Deleting Url ACL for Network Service for " + uri.AbsoluteUri);
            var process = Process.Start(Environment.ExpandEnvironmentVariables("%SystemRoot%\\System32\\netsh.exe"), String.Format("http delete urlacl url={0}", uri.AbsoluteUri));
            process.WaitForExit();
            _log.Debug("Completed with exit code " + process.ExitCode);
        }
       

        public static IDisposable CreateHttpListenerServer(List<Uri> baseAddresses, Func<IDictionary<string, object>, Task> appFunc, TraceFactoryDelegate loggerFunc)
        {

            var props = new Dictionary<string, object>();

            var addresses = baseAddresses.Select(baseAddress => new Dictionary<string, object>()
            {
                {"host", baseAddress.Host}, 
                {"port", baseAddress.Port.ToString()}, 
                {"scheme", baseAddress.Scheme}, 
                {"path", baseAddress.AbsolutePath}

            }).Cast<IDictionary<string, object>>().ToList();
            
            props["host.Addresses"] = addresses;

            props["server.LoggerFactory"] = loggerFunc; 
            OwinServerFactory.Initialize(props);
            return OwinServerFactory.Create(appFunc, props);
        }
    }
    

   

    public class TopShelfKatanaLoggerAdapter : ILogger
    {
        private readonly string _name;
        private readonly LogWriter _logWriter;

        public static TraceFactoryDelegate CreateOwinDelegate(LogWriter logWriter)
        {
            return (name) =>
            {
                var logger = new TopShelfKatanaLoggerAdapter(name, logWriter);
                return logger.WriteCore;
            };

        }
        public TopShelfKatanaLoggerAdapter(string name, LogWriter logWriter)
        {
            _name = name;
            _logWriter = logWriter;
        }

        public bool WriteCore(TraceEventType eventType, int eventId, object state, Exception exception, Func<object, Exception, string> formatter)
        {
            switch (eventType)
            {
                    case TraceEventType.Information:
                        _logWriter.Info(_name + ":" + state);
                    break;
                    case TraceEventType.Error:
                    case TraceEventType.Critical:
                    _logWriter.Error(_name + ":" + state, exception);
                    break;
                default:
                    _logWriter.Debug(_name + ":" + state);
                    break;
            }
            return true;

        }
    }
}