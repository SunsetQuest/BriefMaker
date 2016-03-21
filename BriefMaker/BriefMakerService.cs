// BriefMaker - converts market stream data to time-interval snapshots
// This projected is licensed under the terms of the MIT license.
// NO WARRANTY. THE SOFTWARE IS PROVIDED TO YOU “AS IS” AND “WITH ALL FAULTS.”
// ANY USE OF THE SOFTWARE IS ENTIRELY AT YOUR OWN RISK.
// Created by Ryan S. White in 2013; Last updated in 2016.

using System.ServiceModel;

// Information Source http://stackoverflow.com/a/11267980/2352507

namespace BM
{

    public class BriefMakerService : IBriefMaker
    {
        public void AddDataStreamMomentUsingWCF(byte[] data)
        {
            BriefMaker form = BriefMaker.currentInstance;
            form.mySynchronizationContext.Send(_ => form.AddDataStreamMomentUsingWCF(data), null);
        }
    }

    [ServiceContract(Namespace = "http://Microsoft.ServiceModel.Samples")]
    public interface IBriefMaker
    {
        [OperationContract]
        void AddDataStreamMomentUsingWCF(byte[] data);
    }
}


