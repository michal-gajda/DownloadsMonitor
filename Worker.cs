namespace DownloadsMonitor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Domain.Commands;
    using Domain.Queries;
    using Extensions;
    using MediatR;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    public class Worker : BackgroundService
    {
        private const string DefaultFolderName = "Downloads";
        private readonly IReadOnlyList<string> extensions = new List<string> { ".azw", ".azw3", ".epub", ".mobi", ".pdf", };
        private readonly FileSystemWatcher fileSystemWatcher = new FileSystemWatcher();
        private readonly ILogger<Worker> logger;
        private readonly IMediator mediator;
        private volatile bool disposed;

        public Worker(ILogger<Worker> logger, IMediator mediator)
        {
            this.logger = logger;
            this.mediator = mediator;
            var personalFolder = Path.GetDirectoryName(Environment.GetFolderPath(Environment.SpecialFolder.Personal));

            if (string.IsNullOrWhiteSpace(personalFolder))
            {
                return;
            }

            this.fileSystemWatcher.Path = Path.Combine(personalFolder, DefaultFolderName);

            this.fileSystemWatcher.Created += (sender, e) =>
            {
                var fileInfo = new FileInfo(e.FullPath);

                if (this.extensions.Contains(fileInfo.Extension))
                {
                    try
                    {
                        var md5 = fileInfo.GetMD5();

                        var fileEntry = this.mediator.Send(new GetEntryQuery
                        {
                            Length = fileInfo.Length,
                            Md5 = md5,
                        });

                        if (fileEntry is null)
                        {
                            _ = this.mediator.Send(new AddFileEntryCommand
                            {
                                FileName = fileInfo.Name,
                                Length = fileInfo.Length,
                                MD5 = md5,
                            });
                        }
                        else
                        {
                            _ = this.mediator.Send(new DeleteFileCommand
                            {
                                FullName = fileInfo.FullName,
                                Name = fileInfo.Name,
                            });
                        }
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(exception, exception.Message);
                    }
                }
            };

            this.fileSystemWatcher.Changed += (sender, e) =>
            {
                var fileInfo = new FileInfo(e.FullPath);

                if (this.extensions.Contains(fileInfo.Extension))
                {
                    try
                    {
                        var md5 = fileInfo.GetMD5();

                        var fileEntry = this.mediator.Send(new GetEntryQuery
                        {
                            Length = fileInfo.Length,
                            Md5 = md5,
                        });

                        if (fileEntry is null)
                        {
                            _ = this.mediator.Send(new AddFileEntryCommand
                            {
                                FileName = fileInfo.Name,
                                Length = fileInfo.Length,
                                MD5 = md5,
                            });
                        }
                        else
                        {
                            _ = this.mediator.Send(new DeleteFileCommand
                            {
                                FullName = fileInfo.FullName,
                                Name = fileInfo.Name,
                            });
                        }
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(exception, exception.Message);
                    }
                }
            };
        }

        ~Worker()
        {
            this.Dispose(false);
        }

        public override void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            this.fileSystemWatcher.EnableRaisingEvents = true;

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(5, stoppingToken).ConfigureAwait(false);
            }

            this.fileSystemWatcher.EnableRaisingEvents = false;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                this.fileSystemWatcher.Dispose();
            }

            this.disposed = true;
        }
    }
}
