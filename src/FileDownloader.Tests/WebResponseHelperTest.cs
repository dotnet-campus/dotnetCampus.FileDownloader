using dotnetCampus.FileDownloader.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MSTest.Extensions.Contracts;

namespace FileDownloader.Tests
{
    [TestClass]
    public class WebResponseHelperTest
    {
        [ContractTestCase]
        public void GetFileNameFromContentDispositionText()
        {
            "传入使用 UTF-8 编码的字符串，可以解析出文件名".Test(() =>
            {
                var contentDispositionText = "attachment; filename=__.dll; filename*=UTF-8''%E9%80%97%E6%AF%94.dll";
                var fileName = WebResponseHelper.GetFileNameFromContentDispositionText(contentDispositionText);
                Assert.AreEqual("逗比.dll", fileName);
            });
        }
    }
}
