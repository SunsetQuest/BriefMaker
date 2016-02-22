// BriefMaker - converts market stream data to time-interval snapshots
// This projected is licensed under the terms of the MIT license.
// NO WARRANTY. THE SOFTWARE IS PROVIDED TO YOU “AS IS” AND “WITH ALL FAULTS.”
// ANY USE OF THE SOFTWARE IS ENTIRELY AT YOUR OWN RISK.
// Copyright (c) 2013, 2014, 2015, 2016 Ryan S. White

using System;
using System.ServiceModel;

// Source: https://msdn.microsoft.com/en-us/library/ms730935.aspx

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
        void AddDataStreamMomentUsingWCF(Byte[] data);
    }
}


