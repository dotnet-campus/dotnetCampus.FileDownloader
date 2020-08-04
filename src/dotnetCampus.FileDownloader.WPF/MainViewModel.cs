using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace dotnetCampus.FileDownloader.WPF
{
    public class MainViewModel
    {
        public MainViewModel()
        {
            var loggerFactory = LoggerFactory.Create(builder => { });

            _loggerFactory = loggerFactory;
        }

        public async void Init()
        {
            var downloadedFileInfoList = await DownloadFileListManager.ReadDownloadedFileList();

            if (downloadedFileInfoList != null)
            {
                foreach (var downloadFileInfo in downloadedFileInfoList)
                {
                    downloadFileInfo.IsFinished = true;
                }

                DownloadFileInfoList.AddRange(downloadedFileInfoList);
            }
        }

        public ObservableCollection<DownloadFileInfo> DownloadFileInfoList { get; } = new ObservableCollection<DownloadFileInfo>();

        private DownloadFileListManager DownloadFileListManager { get; } = new DownloadFileListManager();

        private ISharedArrayPool SharedArrayPool { get; } = new FileDownloaderSharedArrayPool();

        public async void AddDownloadFile()
        {
            var url = AddFileDownloadViewModel.CurrentDownloadUrl;
            var file = AddFileDownloadViewModel.CurrentDownloadFilePath;

            var logger = _loggerFactory.CreateLogger<SegmentFileDownloader>();
            using var progress = new FileDownloadSpeedProgress();

            file = Path.GetFullPath(file);

            var downloadFileInfo = new DownloadFileInfo()
            {
                FileName = Path.GetFileName(file),
                AddedTime = DateTime.Now.ToString(),
                DownloadUrl = url,
                FilePath = file
            };

            DownloadFileInfoList.Add(downloadFileInfo);

            progress.ProgressChanged += (sender, downloadProgress) =>
            {
                downloadFileInfo.DownloadSpeed = downloadProgress.DownloadSpeed;
                downloadFileInfo.FileSize = downloadProgress.FileSize;
                downloadFileInfo.DownloadProcess = downloadProgress.DownloadProcess;
            };

            _ = DownloadFileListManager.WriteDownloadedFileListToFile(DownloadFileInfoList.ToList());

            var segmentFileDownloader = new SegmentFileDownloader(url, new FileInfo(file), logger, progress,
                sharedArrayPool: SharedArrayPool, bufferLength: FileDownloaderSharedArrayPool.BufferLength);
            await segmentFileDownloader.DownloadFile();

            progress.Stop();

            downloadFileInfo.DownloadSpeed = "";
            downloadFileInfo.DownloadProcess = "完成";
            downloadFileInfo.IsFinished = true;

            _ = DownloadFileListManager.WriteDownloadedFileListToFile(DownloadFileInfoList.ToList());

            // 后续优化多任务下载的时候的回收
            _ = Task.Delay(TimeSpan.FromSeconds(3))
                .ContinueWith(_ => ((FileDownloaderSharedArrayPool) SharedArrayPool).Clean());
        }

        private readonly ILoggerFactory _loggerFactory;

        public AddFileDownloadViewModel AddFileDownloadViewModel { get; } = new AddFileDownloadViewModel();

    }

    class FileDownloaderSharedArrayPool : ISharedArrayPool
    {
        public const int BufferLength = ushort.MaxValue;

        public byte[] Rent(int minLength)
        {
            if (minLength != BufferLength)
            {
                throw new ArgumentException($"Can not receive minLength!={BufferLength}");
            }

            lock (Pool)
            {
                for (var i = 0; i < Pool.Count; i++)
                {
                    var reference = Pool[i];
                    if (reference.TryGetTarget(out var byteList))
                    {
                        Pool.RemoveAt(i);
                        return byteList;
                    }
                    else
                    {
                        Pool.RemoveAt(i);
                        i--;
                    }
                }
            }

            return new byte[BufferLength];
        }

        public void Return(byte[] array)
        {
            lock (Pool)
            {
                Pool.Add(new WeakReference<byte[]>(array));
            }
        }

        public void Clean()
        {
            lock (Pool)
            {
                GC.Collect();
                GC.WaitForFullGCComplete();

                Pool.RemoveAll(reference => !reference.TryGetTarget(out _));

                Pool.Capacity = Pool.Count;
            }
        }

        private List<WeakReference<byte[]>> Pool { get; } = new List<WeakReference<byte[]>>();
    }
}