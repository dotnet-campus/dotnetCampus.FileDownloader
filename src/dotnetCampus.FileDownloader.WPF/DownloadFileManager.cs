using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace dotnetCampus.FileDownloader.WPF
{
    public class DownloadFileManager
    {
        public async Task<List<DownloadFileInfo>> ReadDownloadedFileList()
        {
            var file = GetStorageFilePath();

            if (!File.Exists(file))
            {
                var list = new List<DownloadFileInfo>();
#if DEBUG
                for (int i = 0; i < 100; i++)
                {
                    list.Add(new DownloadFileInfo()
                    {
                        FileName = "lindexi.data",
                        AddedTime = DateTime.Now.ToString(),
                        DownloadProcess = "100/100",
                        DownloadSpeed = "10MB/s",
                        DownloadUrl = "http://blog.lindexi.com",
                        FilePath = @"C:\lindexi\lindexi.data",
                        FileSize = "100 GB"
                    });
                }
#endif

                return list;
            }

            var text = await File.ReadAllTextAsync(file);

            var downloadFileInfoList = JsonConvert.DeserializeObject<List<DownloadFileInfo>>(text);
            return downloadFileInfoList;
        }

        public async Task WriteDownloadedFileListToFile(List<DownloadFileInfo> downloadFileInfoList)
        {
            var file = GetStorageFilePath();

            var text = JsonConvert.SerializeObject(downloadFileInfoList);

            await File.WriteAllTextAsync(file, text);
        }

        private string GetStorageFilePath()
        {
            string folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            var file = Path.Combine(folder!, StorageFile);

            return file;
        }

        private const string StorageFile = "DownloadedFileList.json";
    }
}