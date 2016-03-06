// BriefMaker - converts market stream data to time-interval snapshots
// This projected is licensed under the terms of the MIT license.
// NO WARRANTY. THE SOFTWARE IS PROVIDED TO YOU “AS IS” AND “WITH ALL FAULTS.”
// ANY USE OF THE SOFTWARE IS ENTIRELY AT YOUR OWN RISK.
// Created by Ryan S. White in 2013; Last updated in 2016.

using System;
using System.Collections.Generic;
using System.IO;

namespace BM
{
    public class BriefCoin
    {

        /// <summary>This is the Brief where data is being collected.</summary>
        public FullBrief capturingBrief;
        public object capturingLock = new object();

        /// <summary>This is the Brief where data is being finished up and uploaded.</summary>
        public FullBrief processingBrief;
        public object processingLock = new object();

        //private static NLog.Logger log;

        public BriefCoin(int symbolCt, Index[] indexes)
        {
            capturingBrief = new FullBrief(symbolCt, indexes);
            processingBrief = new FullBrief(symbolCt, indexes);
            //log = NLog.LogManager.GetCurrentClassLogger();
        }

        /// <summary>
        /// Initialize the BriefCoin. One side of the coin processes the briefs and while the other finishes a brief and upload it.
        /// </summary>
        /// <param name="symbolCt">The number of symbols.</param>
        /// <param name="indexes">An Array of market indexes that will be stored.</param>
        /// <param name="mostRecentBrief">The most recent brief in database. Use null if this will be the first row in the database.</param>
        public BriefCoin(int symbolCt, Index[] indexes, byte[] mostRecentBrief = null)
        {
            capturingBrief = new FullBrief(symbolCt, indexes, mostRecentBrief);
            processingBrief = new FullBrief(symbolCt, indexes, mostRecentBrief);
            //log = NLog.LogManager.GetCurrentClassLogger();
        }

        /// <summary>Before calling FlipOver() this thread should not hold any checked out items. </summary>
        public void FlipOver()
        {
            FullBrief tempA = capturingBrief;
            //log.Trace("FlipOver Called\n  1/4 capturingLock: ON -> from " + methodBase.Name );
            System.Threading.Monitor.Enter(capturingLock);//same as lock()
            //log.Trace("...Done\n");
            //log.Trace("  2/4 processingLock: ON");
            System.Threading.Monitor.Enter(processingLock);
            //log.Trace("...Done\n");
            capturingBrief = processingBrief;
            processingBrief = tempA;
            //log.Trace("  3/4 processingLock: OFF");
            System.Threading.Monitor.Exit(processingLock);
            //log.Trace("...Done\n");
            //log.Trace("  4/4 capturingLock: OFF");
            System.Threading.Monitor.Exit(capturingLock);
            //log.Trace("...Done\n");
            //log.Trace("FlipOver Call complete\n");
        }

        /// <summary>capturing Side is generally for Finishing, Reading, And Preparing. </summary>
        public void CheckoutCapturing()
        {
            //log.Trace("CapturingLock: ON");
            System.Threading.Monitor.Enter(capturingLock);
            //log.Trace("...Done\n");
        }

        /// <summary>capturing Side is generally for Finishing, Reading, And Preparing. </summary>
        public void CheckInCapturing()
        {
            //log.Trace("CapturingLock: OFF");
            System.Threading.Monitor.Exit(capturingLock);
            //log.Trace("...Done\n");
        }

        /// <summary>Bottom Side is generally for Writing with captured updates. </summary>
        public void CheckoutProcessing()
        {
            //log.Trace("ProcesingLock: ON");
            System.Threading.Monitor.Enter(processingLock);
            //log.Trace("...Done\n");
        }

        /// <summary>Bottom Side is generally for Writing with captured updates. </summary>
        public void CheckInProcessing()
        {
            //log.Trace("ProcesingLock: OFF");
            System.Threading.Monitor.Exit(processingLock);
            //log.Trace("...Done\n");
        }
    }

    public class FullBrief
    {
        //public int briefID;
        private readonly int symbCt;
        public PartialBriefWithPrices[/*symbols*/] symbols;
        private Index[] indexes; // contains brief like data for simple indexes

        /// <summary>
        /// Initializes BrfSet.
        /// </summary>
        /// <param name="symbCt">The number of Symbols configure for.</param>
        /// <param name="indexes">An Array of market indexes that will be stored.</param>
        /// <param name="lastBrfImage">Most recent brief in database. Use null if this will be the first row in the database.</param>
        public FullBrief(int symbCt, Index[] indexes, byte[] lastBrfImage = null)
        {
            
            this.symbCt = symbCt;
            this.indexes = indexes;
            symbols = new PartialBriefWithPrices[symbCt];
            for (int i = 0; i < symbCt; i++)
                symbols[i] = new PartialBriefWithPrices();

            if (lastBrfImage != null)
            {
                BinaryReader reader = new BinaryReader(new MemoryStream(lastBrfImage));
                
                // skip over headers; don't save anything
                reader.BaseStream.Seek(BriefMaker.HeaderCount * sizeof(float), SeekOrigin.Begin);
                // |----HDRs+Indexes(32)---|---------------------Stocks(32x32)--------------------|

                for (int s = 0; s < symbCt; s++) //for each header item
                {
                    symbols[s].volume_day = 0; reader.ReadSingle();
                    symbols[s].volume_ths = 0; reader.ReadSingle();
                    symbols[s].largTrdPrc = 0; reader.ReadSingle();
                    symbols[s].price_high = 0; reader.ReadSingle();
                    symbols[s].price_loww = 0; reader.ReadSingle();
                    symbols[s].price_last = reader.ReadSingle();
                    symbols[s].price_bidd = reader.ReadSingle();
                    symbols[s].price_askk = reader.ReadSingle();
                    symbols[s].volume_bid = 0; reader.ReadSingle();
                    symbols[s].volume_ask = 0; reader.ReadSingle();
                    symbols[s].price_medn = 0; reader.ReadSingle();
                    symbols[s].price_mean = 0; reader.ReadSingle();
                    symbols[s].price_mode = 0; reader.ReadSingle();
                    symbols[s].buyy_price = reader.ReadSingle();
                    symbols[s].sell_price = reader.ReadSingle();
                    symbols[s].largTrdVol = 0; reader.ReadSingle();
                    symbols[s].prcModeCnt = 0; reader.ReadSingle();
                    symbols[s].vol_at_ask = 0; reader.ReadSingle();
                    symbols[s].vol_no_chg = 0; reader.ReadSingle();
                    symbols[s].vol_at_bid = 0; reader.ReadSingle();
                    symbols[s].BidUpTicks = 0; reader.ReadSingle();
                    symbols[s].BidDnTicks = 0; reader.ReadSingle();
                    symbols[s].sale_count = 0; reader.ReadSingle();
                    symbols[s].extIndex00 = 0; reader.ReadSingle();
                    symbols[s].extIndex01 = 0; reader.ReadSingle();
                    symbols[s].extIndex02 = 0; reader.ReadSingle();
                    symbols[s].extIndex03 = 0; reader.ReadSingle();
                    symbols[s].extIndex04 = 0; reader.ReadSingle();
                    symbols[s].extIndex05 = 0; reader.ReadSingle();
                    symbols[s].extIndex06 = 0; reader.ReadSingle();
                    symbols[s].extIndex07 = 0; reader.ReadSingle();
                    symbols[s].extIndex08 = 0; reader.ReadSingle();
                }
            }
        }

        /// <summary>Let create a byteArray for DB storage. We need brfID so we can fill the headers with time and other date related data.</summary>
        public byte[] GetAsBinary(uint briefID)
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);

            DateTime curTime = new TinyTime6Sec(briefID);
            TimeSpan sinceStart = curTime.TimeOfDay - (new TimeSpan(TinyTime6Sec.START_HOUR,0,0));
            TimeSpan timeRemaining = (new TimeSpan(TinyTime6Sec.END_HOUR,0,0)) - curTime.TimeOfDay;

            ////////////// fill-in header information ///////////////////////
            bw.Write(briefID                                 ); // free_BrfID  0    // BrfID stored in UINT format to aid in debugging
            //bw.Write((float)0.0                            ); // free_slot0  1    // can be filled in by the client with tstNo to aid in debugging
            //bw.Write((float)curTime.Year                   ); // cur_yearrr  2    // Removed because is usually the same      
            //bw.Write((float)curTime.Month                  ); // cur_Monthh  3    // Removed because is usually the same           
            bw.Write((float)curTime.Day                      ); // cur_DayNum  4               
            bw.Write((float)curTime.Hour                     ); // cur_Hourrr  5        
            bw.Write((float)curTime.Minute                   ); // cur_Minute  6        
            bw.Write((float)curTime.Second                   ); // cur_Second  7         
            //bw.Write((float)curTime.Millisecond            ); // cur_MilliS  8    // Removed because always zero
            bw.Write((float)curTime.DayOfWeek                ); // Day_OfWeek  9           
            //bw.Write((float)curTime.DayOfYear              ); // Day_OfYear  10   // Removed - Not really useful for less then a years of data     
            bw.Write((float)curTime.TimeOfDay.TotalMinutes   ); // Total_Mins  11
            bw.Write((float)curTime.TimeOfDay.TotalSeconds   ); // Total_Secs  12   
            bw.Write((float)sinceStart.Hours                 ); // Hours_Open  13
            bw.Write((float)timeRemaining.Hours              ); // Hours_Rema  14
            //bw.Write((float)sinceStart.Minutes             ); // Secon_Open  15   // Removed - Same as curTime.minute
            bw.Write((float)timeRemaining.Minutes            ); // Secon_Rema  16   // note: when changing set 17/*nonInxHdrCt*/
            bw.Write(indexes[0].tickTypeAttrib[(int)IB_TickType.lastPc_4].last);
            bw.Write(indexes[1].tickTypeAttrib[(int)IB_TickType.bidSz__0].last);
            bw.Write(indexes[1].tickTypeAttrib[(int)IB_TickType.bidPc__1].last);
            bw.Write(indexes[1].tickTypeAttrib[(int)IB_TickType.askPc__2].last);
            bw.Write(indexes[2].tickTypeAttrib[(int)IB_TickType.bidPc__1].last);
            bw.Write(indexes[2].tickTypeAttrib[(int)IB_TickType.askPc__2].last);
            bw.Write(indexes[3].tickTypeAttrib[(int)IB_TickType.lastPc_4].last);
            bw.Write(indexes[4].tickTypeAttrib[(int)IB_TickType.bidSz__0].last);
            bw.Write(indexes[4].tickTypeAttrib[(int)IB_TickType.bidPc__1].last);
            bw.Write(indexes[4].tickTypeAttrib[(int)IB_TickType.askPc__2].last);
            bw.Write(indexes[5].tickTypeAttrib[(int)IB_TickType.bidSz__0].last);
            bw.Write(indexes[5].tickTypeAttrib[(int)IB_TickType.bidPc__1].last);
            bw.Write(indexes[5].tickTypeAttrib[(int)IB_TickType.askPc__2].last);
            bw.Write(indexes[6].tickTypeAttrib[(int)IB_TickType.bidPc__1].last);
            bw.Write(indexes[6].tickTypeAttrib[(int)IB_TickType.askPc__2].last);
            bw.Write(indexes[6].tickTypeAttrib[(int)IB_TickType.lastPc_4].last);

            // Reset the headers by filling them with zero.
            while (bw.BaseStream.Length < 32 * 4)
                bw.Write((float)0.0); 

            ////////////// fill-in symbol information ///////////////////////
            for (int s = 0; s < symbCt; s++) 
            {
                bw.Write(symbols[s].volume_day); // 0 |float volume_day|carried         |days total volume
                bw.Write(symbols[s].volume_ths); // 1 |float volume_ths|reset+running   |volume for this time period (calculated)
                bw.Write(symbols[s].largTrdPrc); // 2 |float largTrdPrc|reset+running   |price at when largest trade 
                bw.Write(symbols[s].price_high); // 3 |float price_high|reset+running   |highest price in period
                bw.Write(symbols[s].price_loww); // 4 |float price_loww|reset+running   |lowest price in period
                bw.Write(symbols[s].price_last); // 5 |float price_last|carried         |last trade price
                bw.Write(symbols[s].price_bidd); // 6 |float price_bidd|carried         |last bid price
                bw.Write(symbols[s].price_askk); // 7 |float price_askk|carried         |last ask price
                bw.Write(symbols[s].volume_bid); // 8 |float volume_bid|carried         |last bid size
                bw.Write(symbols[s].volume_ask); // 9 |float volume_ask|carried         |last ask size
                bw.Write(symbols[s].price_medn); // 10|float price_medn|overwritten     |Statistics median for price
                bw.Write(symbols[s].price_mean); // 11|float price_mean|overwritten     |Statistics mean for price
                bw.Write(symbols[s].price_mode); // 12|float price_mode|overwritten     |Statistics mode for price
                bw.Write(symbols[s].buyy_price); // 13|float buyy_price|carried         |A guess at what the buy price might be.
                bw.Write(symbols[s].sell_price); // 14|float sell_price|carried         |A guess at what the sell price might be.
                bw.Write(symbols[s].largTrdVol); // 15|float largTrdVol|reset+running   |The Size of largest trade
                bw.Write(symbols[s].prcModeCnt); // 16|float prcModeCnt|overwritten     |Statistics mode price count
                bw.Write(symbols[s].vol_at_ask); // 17|float vol_at_ask|reset+running   |Volume at ask price
                bw.Write(symbols[s].vol_no_chg); // 18|float vol_no_chg|reset+running   |Volume with no last change in last size.
                bw.Write(symbols[s].vol_at_bid); // 19|float vol_at_bid|reset+running   |Volume at bid price
                bw.Write(symbols[s].BidUpTicks); // 20|float BidUpTicks|reset+running   |How many times the bid went up.
                bw.Write(symbols[s].BidDnTicks); // 21|float BidDnTicks|reset+running   |How many times the bid went down.
                bw.Write(symbols[s].sale_count); // 22|float sale_count|reset+running   |How many trades did we see.
                bw.Write(symbols[s].extIndex00); // 23|float extIndex00|calculated      |6 sec. calculated ATR   
                bw.Write(symbols[s].extIndex01); // 24|float extIndex01|reset+calculated|6 sec. calculated CCI   
                bw.Write(symbols[s].extIndex02); // 25|float extIndex02|reset+calculated|6 sec. calculated EMA   
                bw.Write(symbols[s].extIndex03); // 26|float extIndex03|reset+calculated|6 sec. calculated Kama  
                bw.Write(symbols[s].extIndex04); // 27|float extIndex04|reset+calculated|6 sec. calculated RSI   
                bw.Write(symbols[s].extIndex05); // 28|float extIndex05|reset+calculated|6 sec. calculated SMA   
                bw.Write(symbols[s].extIndex06); // 29|float extIndex06|reset+calculated|6 sec. calculated SarExt
                bw.Write(symbols[s].extIndex07); // 30|float extIndex07|reset+calculated|6 sec. calculated MACD  
                bw.Write(symbols[s].extIndex08); // 31|float extIndex08|reset+calculated|6 sec. calculated Bollinger bands
            }
            
            byte[] ret = ms.ToArray();
            ms.Dispose();
            //bw.Dispose();
            return ret;
        }
    }

    /// <summary>
    /// Contains the brief information and last values for one stock.
    /// </summary>
    public class PartialBriefWithPrices
    {
        /// <summary>A list of the recent trade values for this period.</summary>
        public List<float> prices = new List<float>();
        /// <summary>Contains the last Volume TickType Submitted to this brief. It is needed to prevent duplicate last volume sizes.</summary>
        public int LastVolTickType = 0;
        /// <summary>Contains the last TickType Submitted to this brief. It is used for volume calculations.</summary>
        public IB_TickType lastTickType = 0;

        public float volume_day; // 0
        public float volume_ths; // 1
        public float largTrdPrc; // 2
        public float price_high; // 3
        public float price_loww; // 4
        public float price_last; // 5
        public float price_bidd; // 6
        public float price_askk; // 7
        public float volume_bid; // 8
        public float volume_ask; // 9
        public float price_medn; // 10
        public float price_mean; // 11
        public float price_mode; // 12
        public float buyy_price; // 13
        public float sell_price; // 14
        public float largTrdVol; // 15
        public float prcModeCnt; // 16
        public float vol_at_ask; // 17
        public float vol_no_chg; // 18
        public float vol_at_bid; // 19
        public float BidUpTicks; // 20
        public float BidDnTicks; // 21
        public float sale_count; // 22
        public float extIndex00; // 23
        public float extIndex01; // 24
        public float extIndex02; // 25
        public float extIndex03; // 26
        public float extIndex04; // 27
        public float extIndex05; // 28
        public float extIndex06; // 29
        public float extIndex07; // 30
        public float extIndex08; // 31
    }
}