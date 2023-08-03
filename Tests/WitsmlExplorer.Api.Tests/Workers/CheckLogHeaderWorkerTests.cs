using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Moq;

using Serilog;

using Witsml;
using Witsml.Data;
using Witsml.Extensions;
using Witsml.ServiceReference;

using WitsmlExplorer.Api.Jobs;
using WitsmlExplorer.Api.Models;
using WitsmlExplorer.Api.Models.Reports;
using WitsmlExplorer.Api.Services;
using WitsmlExplorer.Api.Workers;

using Xunit;

namespace WitsmlExplorer.Api.Tests.Workers
{
    public class CheckLogHeaderWorkerTests
    {
        private const string LogUid = "8cfad887-3e81-40f0-9034-178be642df65";
        private const string LogName = "Test log";
        private const string WellUid = "W-5209671";
        private const string WellboreUid = "B-5209671";
        private const string DepthDataRow1 = "100,501";
        private const string DepthDataRow2 = "101,51,502";
        private const string DepthDataRow3 = "102,52,";
        private const string DepthDataFirstRowForTQ = "101,51";
        private const string TimeDataRow1 = "2023-04-19T00:00:00Z,501";
        private const string TimeDataRow2 = "2023-04-19T00:00:01Z,51,502";
        private const string TimeDataRow3 = "2023-04-19T00:00:02Z,52,";
        private const string TimeDataFirstRowForTQ = "2023-04-19T00:00:01Z,51";
        private readonly Mock<IWitsmlClient> _witsmlClient;
        private readonly CheckLogHeaderWorker _worker;

        public CheckLogHeaderWorkerTests()
        {
            Mock<IWitsmlClientProvider> witsmlClientProvider = new();
            _witsmlClient = new Mock<IWitsmlClient>();
            witsmlClientProvider.Setup(provider => provider.GetClient()).Returns(_witsmlClient.Object);
            ILoggerFactory loggerFactory = new LoggerFactory();
            loggerFactory.AddSerilog(Log.Logger);
            ILogger<CheckLogHeaderJob> logger = loggerFactory.CreateLogger<CheckLogHeaderJob>();
            _worker = new CheckLogHeaderWorker(logger, witsmlClientProvider.Object);
        }

        [Fact]
        public async Task CheckLogHeader_Depth_CorrectData_IsValid()
        {
            CheckLogHeaderJob job = CreateJobTemplate(WitsmlLog.WITSML_INDEX_TYPE_MD);
            JobInfo jobInfo = new();
            job.JobInfo = jobInfo;

            SetupClient(_witsmlClient, WitsmlLog.WITSML_INDEX_TYPE_MD, shouldBeConsistent: true);
            (_, _) = await _worker.Execute(job);

            Assert.IsType<CheckLogHeaderReport>(jobInfo.Report);
            CheckLogHeaderReport report = (CheckLogHeaderReport)jobInfo.Report;
            Assert.Equal(LogUid, report.LogReference.Uid);
            Assert.Equal(WellUid, report.LogReference.WellUid);
            Assert.Equal(WellboreUid, report.LogReference.WellboreUid);
            Assert.Empty(report.ReportItems);
        }

        [Fact]
        public async Task CheckLogHeader_Depth_IncorrectData_IsInvalid()
        {
            CheckLogHeaderJob job = CreateJobTemplate(WitsmlLog.WITSML_INDEX_TYPE_MD);
            JobInfo jobInfo = new();
            job.JobInfo = jobInfo;


            SetupClient(_witsmlClient, WitsmlLog.WITSML_INDEX_TYPE_MD, shouldBeConsistent: false);
            (_, _) = await _worker.Execute(job);

            Assert.IsType<CheckLogHeaderReport>(jobInfo.Report);
            CheckLogHeaderReport report = (CheckLogHeaderReport)jobInfo.Report;
            Assert.Equal(LogUid, report.LogReference.Uid);
            Assert.Equal(WellUid, report.LogReference.WellUid);
            Assert.Equal(WellboreUid, report.LogReference.WellboreUid);
            List<CheckLogHeaderReportItem> reportItems = (List<CheckLogHeaderReportItem>)report.ReportItems;
            Assert.Single(reportItems);
            Assert.Equal("TQ", reportItems[0].Mnemonic);
            Assert.Equal("101", reportItems[0].HeaderEndIndex);
            Assert.Equal("102", reportItems[0].DataEndIndex);
            Assert.Equal("101", reportItems[0].HeaderStartIndex);
            Assert.Equal("101", reportItems[0].DataStartIndex);
        }

        [Fact]
        public async Task CheckLogHeader_Time_CorrectData_IsValid()
        {
            CheckLogHeaderJob job = CreateJobTemplate(WitsmlLog.WITSML_INDEX_TYPE_DATE_TIME);
            JobInfo jobInfo = new();
            job.JobInfo = jobInfo;

            SetupClient(_witsmlClient, WitsmlLog.WITSML_INDEX_TYPE_DATE_TIME, shouldBeConsistent: true);
            (_, _) = await _worker.Execute(job);

            Assert.IsType<CheckLogHeaderReport>(jobInfo.Report);
            CheckLogHeaderReport report = (CheckLogHeaderReport)jobInfo.Report;
            Assert.Equal(LogUid, report.LogReference.Uid);
            Assert.Equal(WellUid, report.LogReference.WellUid);
            Assert.Equal(WellboreUid, report.LogReference.WellboreUid);
            Assert.Empty(report.ReportItems);
        }

        [Fact]
        public async Task CheckLogHeader_Time_IncorrectData_IsInvalid()
        {
            CheckLogHeaderJob job = CreateJobTemplate(WitsmlLog.WITSML_INDEX_TYPE_DATE_TIME);
            JobInfo jobInfo = new();
            job.JobInfo = jobInfo;


            SetupClient(_witsmlClient, WitsmlLog.WITSML_INDEX_TYPE_DATE_TIME, shouldBeConsistent: false);
            (_, _) = await _worker.Execute(job);

            Assert.IsType<CheckLogHeaderReport>(jobInfo.Report);
            CheckLogHeaderReport report = (CheckLogHeaderReport)jobInfo.Report;
            Assert.Equal(LogUid, report.LogReference.Uid);
            Assert.Equal(WellUid, report.LogReference.WellUid);
            Assert.Equal(WellboreUid, report.LogReference.WellboreUid);
            List<CheckLogHeaderReportItem> reportItems = (List<CheckLogHeaderReportItem>)report.ReportItems;
            Assert.Single(reportItems);
            Assert.Equal("TQ", reportItems[0].Mnemonic);
            Assert.Equal("2023-04-19T00:00:01Z", reportItems[0].HeaderEndIndex);
            Assert.Equal("2023-04-19T00:00:02Z", reportItems[0].DataEndIndex);
            Assert.Equal("2023-04-19T00:00:01Z", reportItems[0].HeaderStartIndex);
            Assert.Equal("2023-04-19T00:00:01Z", reportItems[0].DataStartIndex);
        }

        private static void SetupClient(Mock<IWitsmlClient> witsmlClient, string indexType, bool shouldBeConsistent)
        {
            witsmlClient.Setup(client =>
                client.GetFromStoreNullableAsync(It.IsAny<WitsmlLogs>(), It.IsAny<OptionsIn>()))
                .Returns((WitsmlLogs logs, OptionsIn options) =>
            {
                if (options.MaxReturnNodes == 1)
                {
                    if (logs.Logs[0].LogData.MnemonicList == "")
                    {
                        return Task.FromResult(GetTestLogDataFirstRow(indexType));
                    }
                    return Task.FromResult(GetTestLogDataFirstRowForMnemonic(indexType));
                }
                else if (options.RequestLatestValues == 1)
                {
                    return Task.FromResult(GetTestLogDataLatestValues(indexType));
                }
                return Task.FromResult(GetTestLogHeader(shouldBeConsistent));
            });
        }

        private static CheckLogHeaderJob CreateJobTemplate(string indexType)
        {
            return new CheckLogHeaderJob
            {
                LogReference = new LogObject
                {
                    Uid = LogUid,
                    Name = LogName,
                    WellUid = WellUid,
                    WellboreUid = WellboreUid,
                    IndexType = indexType,
                }
            };
        }

        public static WitsmlLogs GetTestLogHeader(bool shouldBeConsistent)
        {
            return new WitsmlLogs
            {
                Logs = new WitsmlLog
                {
                    UidWell = WellUid,
                    UidWellbore = WellboreUid,
                    Uid = LogUid,
                    StartIndex = new WitsmlIndex("100"),
                    EndIndex = new WitsmlIndex("102"),
                    StartDateTimeIndex = "2023-04-19T00:00:00Z",
                    EndDateTimeIndex = "2023-04-19T00:00:02Z",
                    LogCurveInfo = new List<WitsmlLogCurveInfo>()
                    {
                        new WitsmlLogCurveInfo()
                        {
                            Mnemonic = "Depth",
                            MinIndex = new WitsmlIndex("100"),
                            MaxIndex = new WitsmlIndex("102"),
                            MinDateTimeIndex = "2023-04-19T00:00:00Z",
                            MaxDateTimeIndex = "2023-04-19T00:00:02Z"
                        },
                        new WitsmlLogCurveInfo()
                        {
                            Mnemonic = "TQ",
                            MinIndex = new WitsmlIndex("101"),
                            MaxIndex = new WitsmlIndex(shouldBeConsistent ? "102" : "101"),
                            MinDateTimeIndex = "2023-04-19T00:00:01Z",
                            MaxDateTimeIndex = shouldBeConsistent ? "2023-04-19T00:00:02Z" : "2023-04-19T00:00:01Z"
                        },
                        new WitsmlLogCurveInfo()
                        {
                            Mnemonic = "ROP",
                            MinIndex = new WitsmlIndex("100"),
                            MaxIndex = new WitsmlIndex("101"),
                            MinDateTimeIndex = "2023-04-19T00:00:00Z",
                            MaxDateTimeIndex = "2023-04-19T00:00:01Z"
                        },
                    },
                }.AsSingletonList()
            };
        }

        public static WitsmlLogs GetTestLogDataLatestValues(string indexType)
        {
            bool isDepthLog = indexType == WitsmlLog.WITSML_INDEX_TYPE_MD;
            return new WitsmlLogs
            {
                Logs = new WitsmlLog
                {
                    UidWell = WellUid,
                    UidWellbore = WellboreUid,
                    Uid = LogUid,
                    LogData = new WitsmlLogData()
                    {
                        MnemonicList = "Depth,TQ,ROP",
                        Data = new List<WitsmlData>()
                        {
                            new WitsmlData(){Data = isDepthLog ? DepthDataRow2 : TimeDataRow2},
                            new WitsmlData(){Data = isDepthLog ? DepthDataRow3 : TimeDataRow3}
                        }
                    }
                }.AsSingletonList()
            };
        }

        public static WitsmlLogs GetTestLogDataFirstRow(string indexType)
        {
            bool isDepthLog = indexType == WitsmlLog.WITSML_INDEX_TYPE_MD;
            return new WitsmlLogs
            {
                Logs = new WitsmlLog
                {
                    UidWell = WellUid,
                    UidWellbore = WellboreUid,
                    Uid = LogUid,
                    LogData = new WitsmlLogData()
                    {
                        MnemonicList = "Depth,ROP",
                        Data = new List<WitsmlData>()
                        {
                            new WitsmlData(){Data = isDepthLog ? DepthDataRow1 : TimeDataRow1},
                        }
                    }
                }.AsSingletonList()
            };
        }

        public static WitsmlLogs GetTestLogDataFirstRowForMnemonic(string indexType)
        {
            bool isDepthLog = indexType == WitsmlLog.WITSML_INDEX_TYPE_MD;
            return new WitsmlLogs
            {
                Logs = new WitsmlLog
                {
                    UidWell = WellUid,
                    UidWellbore = WellboreUid,
                    Uid = LogUid,
                    LogData = new WitsmlLogData()
                    {
                        MnemonicList = "Depth,TQ",
                        Data = new List<WitsmlData>()
                        {
                            new WitsmlData()
                            {
                                Data = isDepthLog ? DepthDataFirstRowForTQ : TimeDataFirstRowForTQ
                            },
                        }
                    }
                }.AsSingletonList()
            };
        }
    }
}
