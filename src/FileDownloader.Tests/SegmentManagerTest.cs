using System.Collections.Generic;
using System.Linq;
using dotnetCampus.FileDownloader;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MSTest.Extensions.Contracts;

namespace FileDownloader.Tests
{
    [TestClass]
    public class SegmentManagerTest
    {
        [ContractTestCase]
        public void Finished()
        {
            "·ÖÅäÁ½¶Î£¬ÔÚÈ«²¿ÏÂÔØÍê³ÉÖ®ºó£¬ÄÇÃ´ÏÂÔØÍê³É".Test(() =>
            {
                const long fileLength = 1000;
                var segmentManager = new SegmentManager(fileLength);

                var firstDownloadSegment = segmentManager.GetNewDownloadSegment();
                var secondDownloadSegment = segmentManager.GetNewDownloadSegment();
                secondDownloadSegment.DownloadedLength = fileLength / 2;
                firstDownloadSegment.DownloadedLength = fileLength / 2;

                Assert.AreEqual(true, segmentManager.IsFinished());
            });

            "Ö»·ÖÅäÒ»¶Î£¬ÔÚÒ»¶ÎÃ»ÓÐÍê³É£¬ÄÇÃ´ÏÂÔØÃ»ÓÐÍê³É".Test(() =>
            {
                const long fileLength = 1000;
                var segmentManager = new SegmentManager(fileLength);

                var firstDownloadSegment = segmentManager.GetNewDownloadSegment();
                Assert.AreEqual(fileLength, firstDownloadSegment.RequirementDownloadPoint);
                firstDownloadSegment.DownloadedLength = fileLength / 2;

                Assert.AreEqual(false, segmentManager.IsFinished());
            });

            "Ö»·ÖÅäÒ»¶Î£¬ÔÚÒ»¶ÎÏÂÔØÍê³ÉÖ®ºó£¬ÄÇÃ´ÏÂÔØÍê³É".Test(() =>
            {
                const long fileLength = 1000;
                var segmentManager = new SegmentManager(fileLength);

                var firstDownloadSegment = segmentManager.GetNewDownloadSegment();
                Assert.AreEqual(fileLength, firstDownloadSegment.RequirementDownloadPoint);
                firstDownloadSegment.DownloadedLength = fileLength;

                Assert.AreEqual(true, segmentManager.IsFinished());
            });
        }

        [ContractTestCase]
        public void GetNewDownloadSegment()
        {
            "ÔÚ»ñÈ¡µÚ¶þ¶ÎµÄÊ±ºò£¬ÈçµÚÒ»¶ÎÓÐÏÂÔØÄÚÈÝ£¬»áÔÚÏÂÔØÄÚÈÝµãÖ®ºó¼ÌÐøµÚ¶þ¶Î".Test(() =>
            {
                const long fileLength = 1000;
                var segmentManager = new SegmentManager(fileLength);

                var firstDownloadSegment = segmentManager.GetNewDownloadSegment();
                Assert.AreEqual(fileLength, firstDownloadSegment.RequirementDownloadPoint);

                const long downloadLength = 100;
                firstDownloadSegment.DownloadedLength = downloadLength;

                var secondDownloadSegment = segmentManager.GetNewDownloadSegment();

                Assert.AreEqual(0, firstDownloadSegment.StartPoint);
                Assert.AreEqual((fileLength / 2) + (downloadLength / 2), firstDownloadSegment.RequirementDownloadPoint);

                Assert.AreEqual((fileLength / 2) + (downloadLength / 2), secondDownloadSegment.StartPoint);
                Assert.AreEqual(fileLength, secondDownloadSegment.RequirementDownloadPoint);
            });

            "¶à´Î»ñÈ¡½«»á²»¶Ï·Ö¶Î£¬ËùÓÐ·Ö¶ÎºÏÆðÀ´ÊÇÎÄ¼þ´óÐ¡".Test(() =>
            {
                const long fileLength = 1000;
                var segmentManager = new SegmentManager(fileLength);
                var downloadSegmentList = new List<DownloadSegment>();

                for (int i = 0; i < 100; i++)
                {
                    DownloadSegment downloadSegment = segmentManager.GetNewDownloadSegment();
                    downloadSegmentList.Add(downloadSegment);
                }

                var length = downloadSegmentList.Select(temp => temp.RequirementDownloadPoint - temp.StartPoint).Sum();
                Assert.AreEqual(fileLength, length);
            });

            "ÔÚ»ñÈ¡µÚÈý¶ÎµÄÊ±ºò£¬¿ÉÒÔ»ñÈ¡µÚÒ»¶ÎºÍµÚ¶þ¶ÎµÄÖÐ¼ä".Test(() =>
            {
                const long fileLength = 1000;
                var segmentManager = new SegmentManager(fileLength);

                var firstDownloadSegment = segmentManager.GetNewDownloadSegment();
                Assert.AreEqual(fileLength, firstDownloadSegment.RequirementDownloadPoint);

                var secondDownloadSegment = segmentManager.GetNewDownloadSegment();

                var thirdDownloadSegment = segmentManager.GetNewDownloadSegment();
                Assert.AreEqual(250, thirdDownloadSegment.StartPoint);
            });

            "ÔÚ»ñÈ¡µÚ¶þ¶ÎµÄÊ±ºò£¬½«ÐÞ¸ÄµÚÒ»¶ÎÐèÒªÏÂÔØµÄ³¤¶È£¬Í¬Ê±µÚ¶þ¶Î´ÓÖÐ¼ä¿ªÊ¼".Test(() =>
            {
                const long fileLength = 1000;
                var segmentManager = new SegmentManager(fileLength);

                var firstDownloadSegment = segmentManager.GetNewDownloadSegment();
                Assert.AreEqual(fileLength, firstDownloadSegment.RequirementDownloadPoint);

                var secondDownloadSegment = segmentManager.GetNewDownloadSegment();

                Assert.AreEqual(0, firstDownloadSegment.StartPoint);
                Assert.AreEqual(fileLength / 2, firstDownloadSegment.RequirementDownloadPoint);

                Assert.AreEqual(fileLength / 2, secondDownloadSegment.StartPoint);
                Assert.AreEqual(fileLength, secondDownloadSegment.RequirementDownloadPoint);
            });

            "µÚÒ»¶ÎÏÂÔØÄÚÈÝµÄ³¤¶ÈÊÇÎÄ¼þµÄ³¤¶È".Test(() =>
            {
                const long fileLength = 1000;
                var segmentManager = new SegmentManager(fileLength);

                var downloadSegment = segmentManager.GetNewDownloadSegment();

                Assert.AreEqual(fileLength, downloadSegment.RequirementDownloadPoint);
            });

            "Ä¬ÈÏµÚÒ»¶ÎÏÂÔØÄÚÈÝÊÇ´ÓÁã¿ªÊ¼".Test(() =>
            {
                const long fileLength = 1000;
                var segmentManager = new SegmentManager(fileLength);
                var downloadSegment = segmentManager.GetNewDownloadSegment();
                Assert.AreEqual(0, downloadSegment.StartPoint);
            });
        }
    }
}
