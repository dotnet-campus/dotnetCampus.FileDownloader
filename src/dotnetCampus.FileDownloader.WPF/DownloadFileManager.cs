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
                return new List<DownloadFileInfo>();
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