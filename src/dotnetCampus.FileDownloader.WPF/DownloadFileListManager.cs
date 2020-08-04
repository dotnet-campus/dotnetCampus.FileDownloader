using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace dotnetCampus.FileDownloader.WPF
{
    /// <summary>
    /// 下载文件列表管理
    /// </summary>
    public class DownloadFileListManager
    {
        /// <summary>
        /// 读取本地存储的下载列表
        /// </summary>
        /// <returns></returns>
        public async Task<List<DownloadFileInfo>> ReadDownloadedFileList()
        {
            var file = GetStorageFilePath();

            if (!File.Exists(file))
            {
                var list = new List<DownloadFileInfo>();

                return list;
            }

            var text = await File.ReadAllTextAsync(file);

            var downloadFileInfoList = JsonConvert.DeserializeObject<List<DownloadFileInfo>>(text);
            return downloadFileInfoList;
        }

        /// <summary>
        /// 写入下载列表
        /// </summary>
        /// <param name="downloadFileInfoList"></param>
        /// <returns></returns>
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