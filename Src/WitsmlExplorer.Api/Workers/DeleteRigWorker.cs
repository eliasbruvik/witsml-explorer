using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Serilog;

using Witsml;

using WitsmlExplorer.Api.Jobs;
using WitsmlExplorer.Api.Models;
using WitsmlExplorer.Api.Query;
using WitsmlExplorer.Api.Services;

namespace WitsmlExplorer.Api.Workers
{
    public class DeleteRigWorker : BaseWorker<DeleteRigJob>, IWorker
    {
        private readonly IWitsmlClient witsmlClient;
        public JobType JobType => JobType.DeleteRigs;

        public DeleteRigWorker(IWitsmlClientProvider witsmlClientProvider)
        {
            witsmlClient = witsmlClientProvider.GetClient();
        }

        public override async Task<(WorkerResult, RefreshAction)> Execute(DeleteRigJob job)
        {
            Verify(job);

            var wellUid = job.RigReferences.WellUid;
            var wellboreUid = job.RigReferences.WellboreUid;
            var rigUids = job.RigReferences.RigUids;
            var queries = RigQueries.DeleteRigQuery(wellUid, wellboreUid, rigUids);
            bool error = false;
            var successUids = new List<string>();
            var errorReasons = new List<string>();
            var errorEnitities = new List<EntityDescription>();

            var results = await Task.WhenAll(queries.Select(async (query) =>
            {
                var result = await witsmlClient.DeleteFromStoreAsync(query);
                var rig = query.Rigs.First();
                if (result.IsSuccessful)
                {
                    Log.Information("{JobType} - Job successful", GetType().Name);
                    successUids.Add(rig.Uid);
                }
                else
                {
                    Log.Error("Failed to delete rig. WellUid: {WellUid}, WellboreUid: {WellboreUid}, Uid: {rigUid}, Reason: {Reason}",
                    wellUid,
                    wellboreUid,
                    query.Rigs.First().Uid,
                    result.Reason);
                    error = true;
                    errorReasons.Add(result.Reason);
                    errorEnitities.Add(new EntityDescription
                    {
                        WellName = rig.NameWell,
                        WellboreName = rig.NameWellbore,
                        ObjectName = rig.Name
                    });
                }
                return result;
            }));

            var refreshAction = new RefreshRigs(witsmlClient.GetServerHostname(), wellUid, wellboreUid, RefreshType.Update);
            var successString = successUids.Count > 0 ? $"Deleted Rigs: {string.Join(", ", successUids)}." : "";

            if (!error)
            {
                return (new WorkerResult(witsmlClient.GetServerHostname(), true, successString), refreshAction);
            }

            return (new WorkerResult(witsmlClient.GetServerHostname(), false, $"{successString} Failed to delete some Rigs", errorReasons.First(), errorEnitities.First()), successUids.Count > 0 ? refreshAction : null);
        }

        private static void Verify(DeleteRigJob job)
        {
            if (!job.RigReferences.RigUids.Any()) throw new ArgumentException("A minimum of one Rig UID is required");
            if (string.IsNullOrEmpty(job.RigReferences.WellUid)) throw new ArgumentException("WellUid is required");
            if (string.IsNullOrEmpty(job.RigReferences.WellboreUid)) throw new ArgumentException("WellboreUid is required");
        }
    }
}