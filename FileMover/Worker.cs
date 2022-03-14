using System.ComponentModel;
using System.Net;
using System.Runtime.InteropServices;

namespace FileMover
{
    public class Worker : BackgroundService
    {
        public string networkPath = @"\\wopr\PlayOn";
        NetworkCredential credentials = new NetworkCredential(@"wopr", "McV93v*^6&tY");
        
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker started at: {time}", DateTimeOffset.Now);

            const string recordingFolderPath = @"c:\PlayOn\";

            var recordingFolder = new DirectoryInfo(recordingFolderPath);


            while (!stoppingToken.IsCancellationRequested)
            {
                var recordingFolderFiles = recordingFolder.GetFiles("*.mp4", SearchOption.AllDirectories);

                foreach (var toMove in recordingFolderFiles)
                {
                    try
                    {
                        var age = DateTime.Now.Subtract(toMove.LastWriteTime).TotalMinutes;

                        if (age <= 2)
                        {
                            _logger.LogInformation($"'{toMove}' is too new, {age.ToString()}");
                            continue;
                        }


                        var destinationSubpath = toMove.FullName.Replace(recordingFolderPath, string.Empty, StringComparison.InvariantCultureIgnoreCase).TrimStart('\\');

                        DirectoryInfo channelsFolder;
                        
                        if (toMove.Length > 1674670080)
                        {
                            channelsFolder = new DirectoryInfo(Path.Combine(networkPath, "Movies"));
                        }
                        else
                        {
                            channelsFolder = new DirectoryInfo(Path.Combine(networkPath, "TV"));
                        }

                        var destination = new FileInfo(Path.Combine(channelsFolder.FullName, destinationSubpath));

                        _logger.LogInformation($"{nameof(destination)}='{destination}'");

                        using (new ConnectToSharedFolder(networkPath, credentials))
                        {
                            if (destination.Exists) continue;
                            _logger.LogInformation($"Copying {toMove.FullName} to {destination.FullName}");
                            destination.Directory!.Create();
                            toMove.MoveTo(destination.FullName);
                            _logger.LogInformation($"Moved {toMove.FullName} to {destination.FullName}");
                            var movedFrom = toMove.Directory;
                            toMove.Directory!.Refresh();
                            if (!toMove.Directory.GetFiles().Any())
                            {
                                _logger.LogInformation($"Deleting directory {toMove.Directory.FullName}");
                                toMove.Directory.Delete();
                            }
                        }


                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e.ToString());
                    }                }
                await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

            }
        }
    }

    public class ConnectToSharedFolder : IDisposable
    {
        readonly string _networkName;

        public ConnectToSharedFolder(string networkName, NetworkCredential credentials)
        {
            _networkName = networkName;

            var netResource = new NetResource
            {
                Scope = ResourceScope.GlobalNetwork,
                ResourceType = ResourceType.Disk,
                DisplayType = ResourceDisplaytype.Share,
                RemoteName = networkName
            };

            var userName = string.IsNullOrEmpty(credentials.Domain)
                ? credentials.UserName
                : string.Format(@"{0}\{1}", credentials.Domain, credentials.UserName);

            var result = WNetAddConnection2(
                netResource,
                credentials.Password,
                userName,
                0);

            if (result != 0)
            {
                throw new Win32Exception(result, "Error connecting to remote share");
            }
        }

        ~ConnectToSharedFolder()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            WNetCancelConnection2(_networkName, 0, true);
        }

        [DllImport("mpr.dll")]
        private static extern int WNetAddConnection2(NetResource netResource,
            string password, string username, int flags);

        [DllImport("mpr.dll")]
        private static extern int WNetCancelConnection2(string name, int flags,
            bool force);

        [StructLayout(LayoutKind.Sequential)]
        public class NetResource
        {
            public ResourceScope Scope;
            public ResourceType ResourceType;
            public ResourceDisplaytype DisplayType;
            public int Usage;
            public string LocalName;
            public string RemoteName;
            public string Comment;
            public string Provider;
        }

        public enum ResourceScope : int
        {
            Connected = 1,
            GlobalNetwork,
            Remembered,
            Recent,
            Context
        };

        public enum ResourceType : int
        {
            Any = 0,
            Disk = 1,
            Print = 2,
            Reserved = 8,
        }

        public enum ResourceDisplaytype : int
        {
            Generic = 0x0,
            Domain = 0x01,
            Server = 0x02,
            Share = 0x03,
            File = 0x04,
            Group = 0x05,
            Network = 0x06,
            Root = 0x07,
            Shareadmin = 0x08,
            Directory = 0x09,
            Tree = 0x0a,
            Ndscontainer = 0x0b
        }
    }
}