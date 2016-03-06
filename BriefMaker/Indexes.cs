// BriefMaker - converts market stream data to time-interval snapshots
// This projected is licensed under the terms of the MIT license.
// NO WARRANTY. THE SOFTWARE IS PROVIDED TO YOU “AS IS” AND “WITH ALL FAULTS.”
// ANY USE OF THE SOFTWARE IS ENTIRELY AT YOUR OWN RISK.
// Created by Ryan S. White in 2013; Last updated in 2016.

namespace BM
{
    /// <summary>
    /// This class represents a market index.  It can be volume, price, advances, etc.
    /// Examples: TICK-NASD, VOL-NASD, AD-NASD, TICK-NYSE, INDU            
    /// </summary>
    public class Index  //can be volumes or prices
    {
        //private static NLog.Logger log;
        readonly int DB_ID;             // 0-255; just for debugging
        public float updateCt = 0;      // # of updates (day)
        public string symbol;           // only for debugging

        /// <summary>
        /// Contains stats on TickTypes 0-9.
        /// TWS TickTypes: 0=bidSz, 1=bid, 2=ask, 3=askSz, 4=last, 5=last, 6=high, 7=low, 8=vol, 9=close 
        /// </summary>
        public TickTypeAttrib[] tickTypeAttrib = new TickTypeAttrib[10];  //examples: DJIA.Last , DJIA.Volume (aka TickTypes)

        public Index(int DatabaseID, string symbol)       
        {
            //log = NLog.LogManager.GetCurrentClassLogger();
            DB_ID = DatabaseID;
            this.symbol = symbol;

            for (int i = tickTypeAttrib.Length - 1; i >= 0; i--)
                tickTypeAttrib[i].Init();
        }
        
        public void ContributeItem(IB_TickType tickType, float val)
        {
            updateCt++;
            tickTypeAttrib[(int)tickType].AddNewVal(val);
        }
    }

    /// <summary>
    /// Contains the basic stats for a TickType for a specific time-interval.
    /// Examples: Last, thisPeriodUpCt, dayHigh
    /// </summary>
    public struct TickTypeAttrib
    {
        private bool AutoResetOnNextVal;

        public float last;
        public float thisPeriodBegVal;
        public float thisPeriodHigh;    // this field is not useful for ever-growing values like daily volume.
        public float thisPeriodLoww;    // this field is not useful for ever-growing values like daily volume.
        public float thisPeriodUpCt;    // this field is not useful for ever-growing values like daily volume.
        public float thisPeriodUnCt;  
        public float thisPeriodDnCt;    // this field is not useful for ever-growing values like daily volume.
        public float high;              // this field is not useful for ever-growing values like daily volume.
        public float low;               // this field is not useful for ever-growing values like daily volume.

        public void Init()
        {
            AutoResetOnNextVal = true;
        }

        public void StartNexPeriod()
        {
            thisPeriodBegVal = last;
            thisPeriodHigh = last;
            thisPeriodLoww = last;
            thisPeriodUpCt = 0;
            thisPeriodUnCt = 0;
            thisPeriodDnCt = 0;
        }

        public void AddNewVal(float val) 
        {
            last = val;

            if (AutoResetOnNextVal)
            {
                StartNexPeriod();
                AutoResetOnNextVal = false;
            }

            if (val > last)         // Up Tick    
            {
                thisPeriodUpCt++;
                if (thisPeriodHigh < val)
                {
                    thisPeriodHigh = val;
                    if (high < val)
                        high = val;
                }
            }
            else if (val < last)    // Down Tick       
            {
                thisPeriodDnCt++;
                if (thisPeriodLoww > val)
                {
                    thisPeriodLoww = val;
                    if (low > val)
                        low = val;
                }
            }
            else                    // Unchanged Tick
                thisPeriodUnCt++;

        }
    }

    /// <summary>
    /// This represents the tick-type ID for interactive brokers. For different data-sources this might need to be adjusted.
    /// </summary>
    public enum IB_TickType
    {
        bidSz__0 = 0,
        bidPc__1 = 1,
        askPc__2 = 2,
        askSz__3 = 3,
        lastPc_4 = 4,
        lastSz_5 = 5,
        highPc_6 = 6,
        lowPc__7 = 7,
        volume_8 = 8,
        close__9 = 9
    }
}
