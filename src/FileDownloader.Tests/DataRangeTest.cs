
using dotnetCampus.FileDownloader.Utils.BreakpointResumptionTransmissions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using MSTest.Extensions.Contracts;

namespace FileDownloader.Tests
{
    [TestClass]
    public class DataRangeTest
    {
        [ContractTestCase]
        public void TryMerge()
        {
            "传入两个不相邻的 DataRange 对象，返回不可合并".Test(() =>
            {
                var a = new DataRange(0, 1);
                var b = new DataRange(3, 2);
                var result = DataRange.TryMerge(a, b, out var data);
                Assert.AreEqual(false, result);
            });

            "给定两个相邻的 DataRange 对象，传入顺序是先将传入起点较大的，再传入起点较小的，可以进行合并".Test(() =>
            {
                var a = new DataRange(0, 3);
                var b = new DataRange(3, 2);

                // 传入顺序是先将传入起点较大的，再传入起点较小的
                var result = DataRange.TryMerge(b, a, out var data);
                Assert.IsTrue(result);
                Assert.AreEqual(a.StartPoint, data.StartPoint);
                Assert.AreEqual(a.Length + b.Length, data.Length);
            });

            "给定两个相邻的 DataRange 对象，可以进行合并".Test(() =>
            {
                var a = new DataRange(0, 3);
                var b = new DataRange(3, 2);

                var result = DataRange.TryMerge(a, b, out var data);
                Assert.IsTrue(result);
                Assert.AreEqual(a.StartPoint, data.StartPoint);
                Assert.AreEqual(a.Length + b.Length, data.Length);
            });
        }
    }
}
