// BriefMaker - converts market stream data to time-interval snapshots
// This projected is licensed under the terms of the MIT license.
// NO WARRANTY. THE SOFTWARE IS PROVIDED TO YOU “AS IS” AND “WITH ALL FAULTS.”
// ANY USE OF THE SOFTWARE IS ENTIRELY AT YOUR OWN RISK.
// Created by Ryan S. White in 2013; Last updated in 2016.

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using System.IO;
using System.Data.Linq;
using System.ServiceModel;
using System.Threading;
using System.Text;

/* Notes on setup
 * This program takes stream data from two sources (1) a DB and (2) the Recorder program and converts them into brfs.
 * When starting up it gets the last DB brief and then goes back about 450 brfs(45 min) before that and then starts replaying. 
 * After its done replaying it then looks for and connects to the recorder.  The recorder and BriefMaker talk directly
 * to each other to minimize latency.
 * Currently this program is hard-coded for 32 stocks with each having 32 attributes. Also the header size is also 32.
*/

// Change Log
// March 2015 - fixed memory leak
// Feb   2016 - changed how index and indexCt are downloaded
namespace BM
{
    public partial class BriefMaker : Form
    {
        ////////////////// User configurable values. //////////////////
        /// <summary>(6:25:06) Starting Time To for new Briefs. 6:25:00 brief is not included because 
        /// time is recorded at the tail.</summary>
        readonly TimeSpan BeginRecordTime;
        /// <summary>(6:25:01) The time when we begin to process stream moments. 6:25:00 StreamMoment 
        /// is not included because time is recorded in the tail.</summary>
        readonly TimeSpan PreBeginBufferTime;
        /// <summary>(13:00:00) The time when we end processing of stream moments and recording briefs. 
        /// We include 13:00:00 since the time is recorded in the tail.</summary>
        readonly TimeSpan EndRecordTime;
        /// <summary>The number of brief writes to skip if there is not enough data to continue. 
        /// (in briefs; 200 = 20 minutes)</summary>
        int MaxWaitingLoops = 200;
        /// <summary>The number of Briefs to replay to get formulas/hi/low/last/etc filled. (450=45 minutes)</summary>
        public const int ReplayAmount = 450;
        /// <summary>The number of stocks that we are expected to work with. Has only been tested with 32.</summary>
        public const int StockCount = 32;
        /// <summary>The number of attributes that describe a stock for a time period like high, low, last, vol,
        /// largestTrade, MACD, avg.. (Has only been tested with 32.)</summary>
        public const int StockAttribCount = 32;
        /// <summary>The number of header items in the output. The header contains times, dates, dayOfWeek, 
        /// MarketIndexes, etc. 32 would be 32(headers slots) * sizeof(float) => 128bytes.</summary>
        public const int HeaderCount = 32;
        /// <summary>The number of indexes we are pulling in. The indexes are saved in the header.</summary>
        public const int IndexCount = 7;

        ////////////////// Runtime Items //////////////////
        /// <summary>This is the total byte size of each created brief.</summary>
        const int BriefByteSize = (HeaderCount * sizeof(float) + StockCount * StockAttribCount * sizeof(float));
        /// <summary>Allows another application to submit real-time updates.</summary>
        ServiceHost wcfReceiverHost;       
        public static BriefMaker currentInstance;               // For WCF
        public SynchronizationContext mySynchronizationContext; // For WCF
        /// <summary>Causes WCF updates to be ignored; this is for startup or reloads</summary>
        bool suspendWCF = true;
        /// <summary>Contains two BrfSets. Each has a brief and recent prices.</summary>
        public BriefCoin briefCoin;
        /// <summary>This is just for display.</summary>
        int totalCaptureEventsForDisplay = 0;
        /// <summary>Contains all the stock ticker symbols.</summary>
        string[] stockTickers;
        /// <summary>Contains brief like data for simple indexes.</summary>
        Index[] indexes;
        /// <summary>This is used to make sure we don't skip any seconds or process the same second twice. 
        /// It should be rounded down to the nearest second.</summary>
        long mostRecentTickSubmitted = 0;
        /// <summary>For logging to the display, file, or TCPIP connection. This must be initialized in Load() 
        /// so NLog.config is read.</summary>
        private static NLog.Logger log, logWCF;
        /// <summary>Extended Indicators</summary>
        ExtendedIndicator extendedIndicators;
        /// <summary>
        /// For Startup purposes. This is an array tickers and their attributes that tells us if data has been 
        /// received yet. After data is received for every attribute on every ticker then briefs can be created.
        /// </summary>
        bool[,] waitingForData;
        /// <summary>Used only for startup. This is so we don't upload briefs when rerunning the most recent 
        /// briefs from the DB at startup.</summary>
        int onlySaveIfAfter = 0;
        /// <summary>This is the next expected TinyTime. If it is not then a warning is given.</summary>
        int nextExpectedTinyTime;
        /// <summary>Prevents an error from displaying too many times.</summary>
        int[] ErrorCounters = Enumerable.Repeat(500, 16).ToArray();

        List<Brief> bulkBriefsToSubmit = new List<Brief>(1000);

        public BriefMaker()
        {
            InitializeComponent();
            mySynchronizationContext = SynchronizationContext.Current;
            currentInstance = this;

            BeginRecordTime = Properties.Settings.Default.BeginRecordTime;
            PreBeginBufferTime = Properties.Settings.Default.PreBeginBufferTime;
            EndRecordTime = Properties.Settings.Default.EndRecordTime;
        }

        private void BriefMaker_Load(object sender, EventArgs e)
        {
            /////////// Setup logger ///////////
            log = NLog.LogManager.GetCurrentClassLogger();
            logWCF = NLog.LogManager.GetLogger("BM.BriefMaker.WCF");
            log.Info("Starting application.");

            /////////// Setup WCF ///////////  
            // useful link: http://stackoverflow.com/questions/11267071/how-can-a-self-hosted-winform-wcf-service-interact-with-the-main-form
            // Source: https://msdn.microsoft.com/en-us/library/ms730935.aspx
            suspendWCF = true;
            logWCF.Debug("Initializing WCF");
            // Create a URI to serve as the base address.
            Uri baseAddress = new Uri(Properties.Settings.Default.DirectBriefMakerDataSource);
            // Create a ServiceHost instance.
            wcfReceiverHost = new ServiceHost(typeof(BriefMakerService), baseAddress);
            // Start the service.
            wcfReceiverHost.Open();
            logWCF.Debug("Initializing WCF completed");

            // BeginInvoke is just so we see the form during loading
            BeginInvoke((MethodInvoker)delegate
            {
                LoadSystem();
            }); 
        }

        private void LoadSystem()
        {
            System.Diagnostics.Process.GetCurrentProcess().PriorityClass = 
                System.Diagnostics.ProcessPriorityClass.Normal;
            suspendWCF = true;

            using (var dc = new DataClassesBrfsDataContext())
            {
                /////////// Download Stock Symbols ///////////
                stockTickers = (from symb in dc.Symbols
                           where symb.Type.Equals("STK")
                           orderby symb.SymbolID
                           select symb.Name.Trim()).ToArray();
                if (stockTickers.Count() != StockCount)
                {
                    log.Error("There were {0} stock symbols found. Currently BriefMaker "
                        + "supports a hard value of 32 items.", stockTickers.Count());
                    return;
                }

                /////////// Download Market Indexes ///////////
                indexes = (from s in dc.Symbols
                           where s.Type.Equals("IND")
                           orderby s.SymbolID
                           select new Index(s.SymbolID, s.Name.Trim())).ToArray();
                if (indexes.Count() != IndexCount)
                {
                    log.Error("There were {0} index symbols found. Currently BriefMaker " 
                        + "supports a hard value of 32 items.", stockTickers.Count());
                    return;
                }

                /////////// Setup waiting stuff ///////////
                waitingForData = new bool[StockCount, StockAttribCount];
                for (int i = 0; i < StockCount; i++)
                    for (int a = 0; a < StockAttribCount; a++)
                        waitingForData[i, a] = true; //set waiting for data to true for everything

                /////////// Setup Extended Indicators ///////////
                extendedIndicators = new ExtendedIndicator(StockCount);

                /////////// Read 450 Briefs back from database so we can replay them to get extended indexes ///////////
                if (ReplayAmount <= extendedIndicators.MinimumHistorySize)
                    log.Error("backAmt({0}) is less then ei.MinimumHistorySize({1}) ", 
                        ReplayAmount, (extendedIndicators.MinimumHistorySize));

                // Check to see if there are any brief to pick up where we left off.
                if (dc.Briefs.Any()) 
                {
                    // Fetch the last uploaded brief.
                    var lastBrief = (from b in dc.Briefs
                                   orderby b.BriefID descending
                                   select b).First();

                    onlySaveIfAfter = lastBrief.BriefID;
                    nextExpectedTinyTime = lastBrief.BriefID + 1;
                    int brfIDNearWantedStart = lastBrief.BriefID - ReplayAmount;


                    // Since mostRecentTickSubmitted might point to a record that does not 
                    // exist, we need to find the next record back.
                    var startingBrief = (from b in dc.Briefs
                                       where b.BriefID < brfIDNearWantedStart
                                       orderby b.BriefID descending
                                       select b).FirstOrDefault();

                    byte[] startingBriefImg = startingBrief.BriefBytes.ToArray();
                    if (startingBriefImg.Length != BriefByteSize)
                        logWCF.Error("The Brief size was expected to be {0} but was {1}.", BriefByteSize, lastBrief.BriefBytes.Length);

                    //set mostRecentTickSubmitted back some for replay
                    if (startingBrief.BriefID > 0) //make sure its not empty
                        mostRecentTickSubmitted = ((DateTime)(new TinyTime6Sec(startingBrief.BriefID))).Ticks;


                    // Now lets fill stock values
                    // |----HDRs+Indexes(32)---|-------------------32 Stocks X 32 Attributes ----------------|
                    briefCoin = new BriefCoin(StockCount, indexes, startingBriefImg);
                }
                else //the DB is empty, lets PreBeginBufferTime fresh
                {
                    briefCoin = new BriefCoin(StockCount, indexes);
                    mostRecentTickSubmitted = 0;
                }
            } //end using DataClassesBrfsDataContext
            
            LoadLatestStreamMomentsFromDB();
            onlySaveIfAfter = 0; //hack: do we need this?
            suspendWCF = false;
            System.Diagnostics.Process.GetCurrentProcess().PriorityClass = System.Diagnostics.ProcessPriorityClass.RealTime;
        }

        /// <summary>Fetches any new StreamMoments from the DB starting at mostRecentTickSubmitted.</summary>
        private void LoadLatestStreamMomentsFromDB()
        {
            using (var dcsm = new DataClassesStreamMomentsDataContext()) { 
                progressBar1.Maximum = 
                       (from s in dcsm.StreamMoments
                        where s.SnapshotTime >= (new DateTime(mostRecentTickSubmitted + TimeSpan.TicksPerSecond))
                        //&& s.SnapshotTime.Hour > PreBeginBufferTime // very slow
                        //&& s.SnapshotTime.TimeOfDay <= EndRecordTime // very slow
                        orderby s.SnapshotTime
                        select s).Count();
            }

            progressBar1.Value = 0;
            log.Info("Records to Process: {0}", progressBar1.Maximum);

            const int PageSz = 3600;            // read in 1 hour at a time
            byte[] prevStreamMoments = { 0 };   // hold the last in case we need to duplicate it for gaps 
            while(true)                         // run multiple times to make sure there are no stragglers
            {
                log.Info("Beginning read of all StreamMoments from DB beginning at {0} ({1})", mostRecentTickSubmitted/1000000, new DateTime(mostRecentTickSubmitted));

                using (var dcsm_download = new DataClassesStreamMomentsDataContext())
                { //do not remove or else there will be a memory leak

                    var streamMoments = (from s in dcsm_download.StreamMoments
                                         where s.SnapshotTime >= (new DateTime(mostRecentTickSubmitted + TimeSpan.TicksPerSecond))
                                         //&& s.SnapshotTime.Hour > PreBeginBufferTime // very slow
                                         //&& s.SnapshotTime.TimeOfDay <= EndRecordTime // very slow
                                         orderby s.SnapshotTime
                                         select s).Take(PageSz);

                    // Quit if there are no more.
                    if (!streamMoments.Any())
                        break;

                    foreach (var streamMoment in streamMoments)
                    {
                        // Round down to second
                        long streamTimeInTicks = (streamMoment.SnapshotTime.Ticks / TimeSpan.TicksPerSecond) * TimeSpan.TicksPerSecond;

                        // We want to skip any steamMoments that are not between in normal trading hours or the 5 minutes before.
                        DateTime momentTime = new DateTime(streamTimeInTicks);
                        if (!IsNormalBusinessHoursForStreamProcessing(momentTime))
                        {
                            //momentTime = TinyTime6Sec.GetNextValidDateTime(momentTime);
                            mostRecentTickSubmitted = streamTimeInTicks; //momentTime.Ticks - TimeSpan.TicksPerSecond - (60 * 5 * TimeSpan.TicksPerSecond);
                            continue;
                        }

                        byte[] streamMomentBytes = streamMoment.Data.ToArray();
                        //Make sure we are at the next expected second
                        long nextExpectedTicks = (mostRecentTickSubmitted + TimeSpan.TicksPerSecond);
                        if (streamTimeInTicks != nextExpectedTicks)
                        {
                            DateTime nextExpectedDateTime = new DateTime(nextExpectedTicks);
                            log.Debug("Expected {0}({1}) but got {2}({3})", 
                                (new DateTime(nextExpectedTicks)).ToString("HH:mm:ss.ffff"), 
                                (mostRecentTickSubmitted + TimeSpan.TicksPerSecond), 
                                momentTime.ToString("HH:mm:ss.ffff"), streamTimeInTicks);

                            if (0 == mostRecentTickSubmitted)
                            { // brand new  - database is empty
                                mostRecentTickSubmitted = streamTimeInTicks;
                                log.Info("Processing first StreamMoment for new brief set.");
                            }
                            else if (streamTimeInTicks < mostRecentTickSubmitted)
                            {
                                log.Error("Error: StreamMoment update from DB should never appear in the past.");
                                continue; // if its not the next expected second then exit
                            }
                            else if (streamTimeInTicks == mostRecentTickSubmitted)
                            {
                                log.Error("Error: Duplicate StreamMoment from DB.");
                                continue; // if its not the next expected second then exit
                            }
                            else if (streamTimeInTicks > nextExpectedTicks)
                            {
                                long gapSize = streamTimeInTicks - mostRecentTickSubmitted;
                                int gapSizeInSeconds = (int)(gapSize / TimeSpan.TicksPerSecond);
                                string gapSizeForPrint = (new TimeSpan(gapSize)).ToString("g");

                                if (gapSizeInSeconds > 3600)
                                {
                                    if (ErrorCounters[5]-- > 0)
                                        log.Warn("Large gap in StreamMoments from {0} to {1}. minutes={2}", 
                                            (new DateTime(nextExpectedTicks)).ToString("ddd M/d/yy h:mm:ss.f tt"), 
                                            (new DateTime(streamTimeInTicks - TimeSpan.TicksPerSecond)).ToString("h:mm:ss.f"), 
                                            gapSizeForPrint);
                                }
                                else
                                {
                                    if (ErrorCounters[6]-- > 0)
                                        log.Info("Small gap in StreamMoments from {0} to {1}. minutes={2}", 
                                            (new DateTime(nextExpectedTicks)).ToString("ddd M/d/yy h:mm:ss.f tt"), 
                                            (new DateTime(streamTimeInTicks - TimeSpan.TicksPerSecond)).ToString("h:mm:ss.f"), 
                                            gapSizeForPrint);
                                }
                                while (streamTimeInTicks > (mostRecentTickSubmitted + TimeSpan.TicksPerSecond))
                                {
                                    mostRecentTickSubmitted += TimeSpan.TicksPerSecond;

                                    DateTime time = new DateTime(mostRecentTickSubmitted);
                                    if (IsNormalBusinessHoursForStreamProcessing(time))
                                        AddStreamMoment(prevStreamMoments, mostRecentTickSubmitted, true);
                                    else
                                    {
                                        // jump to starting time later in the morning(if pre am) or tomorrow morning(if pm)
                                        if (time.TimeOfDay < PreBeginBufferTime)
                                            mostRecentTickSubmitted = time.Date.Add(PreBeginBufferTime).Ticks - TimeSpan.TicksPerSecond;
                                        else
                                            mostRecentTickSubmitted = time.Date.AddDays(1).Add(PreBeginBufferTime).Ticks - TimeSpan.TicksPerSecond;
                                    }
                                }
                                if (ErrorCounters[8]-- > 0)
                                    log.Info("Finished filling gap in StreamMoments.  mostRecentTickSubmitted={0}", mostRecentTickSubmitted);
                            }
                            else
                            {
                                log.Error("Error: Unknown streamTimeInTicks from DB update.");
                                continue; // if its not the next expected second then exit
                            }
                        }

                        AddStreamMoment(streamMomentBytes, streamTimeInTicks, true);
                        prevStreamMoments = streamMomentBytes;
                        mostRecentTickSubmitted = streamTimeInTicks;
                    }
                } //end using dcsm_download 

                SubmitPendingBriefs();
                
                // update display
                MethodInvoker mi = new MethodInvoker(() => progressBar1.Increment(PageSz));
                if (progressBar1.InvokeRequired)
                {
                    progressBar1.Invoke(mi);
                }
                else
                {
                    mi.Invoke();
                }
                //System.GC.Collect();
                toolStripStatusLabelEventCt.Text = totalCaptureEventsForDisplay.ToString();
                Refresh();
                Application.DoEvents();
                Thread.Sleep(0);
            };

            progressBar1.Value = 0;
            log.Info("Exiting LoadLatestStreamMomentsFromDB()");

        }

        /// <summary>
        /// Uploads a stream-moment directly via WCF. This function is called by a WCF thread.
        /// </summary>
        public void AddDataStreamMomentUsingWCF(byte[] streamMoment)
        {
            //System.Threading.Thread.CurrentThread.Priority = System.Threading.ThreadPriority.Highest; // does help with performance
            
            if (suspendWCF)
            {
                logWCF.Info("Skipping AddDataStreamMomentUsingWCF() because suspendWCF=true (load/unload is in progress)");
                return;
            }

            if (streamMoment.Length <= 8) 
            {
                logWCF.Error("Skipping AddDataStreamMomentUsingWCF() because br.BaseStream.Length({0}) <= 8 ", streamMoment.Length);  
                return;
            }

            logWCF.Debug("Entering AddDataStreamMomentUsingWCF()");

            long[] streamTimeTicks2 = new long[1]; //array of one item
            Buffer.BlockCopy(streamMoment, 0, streamTimeTicks2, 0, sizeof(long));
            long streamTimeInTicks = (streamTimeTicks2[0] / TimeSpan.TicksPerSecond) * TimeSpan.TicksPerSecond; //round down to second

            BinaryReader reader = new BinaryReader(new MemoryStream(streamMoment)); //TODO: Delete this because I added "Blockcopy" a few lines above but need to check it
            streamTimeInTicks = (reader.ReadInt64() / TimeSpan.TicksPerSecond) * TimeSpan.TicksPerSecond; //round down to second


            //Make sure we are at the next expected second
            long nextExpectedTicks = (mostRecentTickSubmitted + TimeSpan.TicksPerSecond); 
            if (streamTimeInTicks != nextExpectedTicks)
            {
                logWCF.Debug("Expected {0}({1}) but got {2}({3})", 
                    (new DateTime(nextExpectedTicks)).ToString("HH:mm:ss.ffff"), 
                    (mostRecentTickSubmitted + TimeSpan.TicksPerSecond), 
                    (new DateTime(streamTimeInTicks)).ToString("HH:mm:ss.ffff"), 
                    streamTimeInTicks);
                
                if (0 == mostRecentTickSubmitted)
                { // brand new update
                    mostRecentTickSubmitted = streamTimeInTicks;
                    logWCF.Info("Skipping brief received by WCF because there are no DB entries yet");
                    return;
                }
                else if (streamTimeInTicks < mostRecentTickSubmitted)
                {
                    logWCF.Warn("Warning: StreamMoment update from WCF should never appear in the past. This can be normal at startup.");
                    return; // if its not the next expected second then exit
                }
                else if (streamTimeInTicks == mostRecentTickSubmitted)
                {
                    logWCF.Debug("Skipping duplicate StreamMoment from WCF.");
                    return; // if its not the next expected second then exit
                }
                else if (streamTimeInTicks > nextExpectedTicks)
                {
                    logWCF.Info("Gap in StreamMoments from WCF starting from {0} to {1}", mostRecentTickSubmitted, streamTimeInTicks - TimeSpan.TicksPerSecond);
                    
                    // check database to make sure its not there first, else fail because it should have been there.
                    LoadLatestStreamMomentsFromDB();
    
                    if (streamTimeInTicks == (mostRecentTickSubmitted + TimeSpan.TicksPerSecond))
                        logWCF.Debug("Gap has been filled. Continuing with original AddDataStreamMomentUsingWCF()...");
                    else
                    {
                        logWCF.Error("Unable to fill Gap in StreamMoments. Exiting AddDataStreamMomentUsingWCF()");
                        return;
                    }
                }
                else
                {
                    logWCF.Error("Unknown streamTimeInTicks from WCF update.");
                    return; // if its not the next expected second then exit
                }
            }

            AddStreamMoment(streamMoment, streamTimeInTicks, false);
        }

        // Caution: This function can be called by either a UI or a WCF threads.
        public void AddStreamMoment(byte[] streamMoment, long streamTimeInTicks,  bool bulkMode)
        {
            DateTime streamDataTime = new DateTime(streamTimeInTicks);
            log.Debug("Entering AddStreamMoment(streamTimeInTicks={0}, time={1}, bulkMode={2})", 
                streamTimeInTicks, streamDataTime.ToString("ddd M/d/yy h:mm:ss.f tt"), bulkMode);

            BinaryReader br = new BinaryReader(new MemoryStream(streamMoment));
            br.BaseStream.Position = 8; // skip past datetimeTicks - this will be filled in later.

            // Error checking - check stream length
            if (((br.BaseStream.Length - 8) % 7) != 0)
                log.Error("streamMoment.bytes.length should be (8 + x * 7) at {0}", streamDataTime.ToString("ddd M/d/yy h:mm:ss.f tt"));

            // Now Read in all data
            //StringBuilder sb = new StringBuilder(); // uncomment to aid in debugging
            //string[] TickTypes =  { "bidSz","bid","ask","askSz","last","lastSz","??6","??7","vol","??","??10","??11"}; // uncomment to aid in debugging
            while (br.BaseStream.Position < br.BaseStream.Length)
            {
                byte timeFraction = br.ReadByte(); //read time fraction - not really used yet
                IB_TickType tickType = (IB_TickType)br.ReadByte();
                byte id = br.ReadByte();
                float val = br.ReadSingle();

                // Make sure we exclude higher id values (added 10/2/2013)
                if (id >= StockCount + IndexCount)
                    continue;

                //sb.AppendLine(streamDataTime.ToString("ddd M/d/yy h:mm:ss.f tt") + "." + timeFraction + ", " + symbols[id] + ", " + TickTypes[tickType] + ", " + val.ToString());

                if (id < StockCount) //hack
                    ContributeToBrf(tickType, id, val);
                else
                    indexes[id - StockCount].ContributeItem(tickType, val);
            }

            //DumpTextToDesktopFile(sb); // uncomment to aid in debugging

            int second = streamDataTime.Second % 6;
            if (second == 1)
                SolidifyBuySellPrices();
            if (second == 5)
                FinalizeAndUploadBrief(streamDataTime, bulkMode);

            // Update mostRecentTickSubmitted to new time;
            mostRecentTickSubmitted = streamTimeInTicks;

            if (!bulkMode)
                Invoke((MethodInvoker)delegate
                {
                    toolStripStatusLabelEventCt.Text = (totalCaptureEventsForDisplay++).ToString();
                });  // runs on UI thread 
            log.Debug("Exiting AddStreamMoment(streamTimeInTicks={0}, bulkMode={1})", streamTimeInTicks, bulkMode);
            br.BaseStream.Dispose();  //todo: can we remove this
            br.Dispose();
        }

        private void FinalizeAndUploadBrief(DateTime time, bool inBulkMode)
        {
            log.Debug("Entering FinalizeAndUploadBrief() time={0}, bulkMode={1}", time.ToString("ddd M/d/yy h:mm:ss.f tt"), inBulkMode);

            //save any values from the EndRecordTime of the previous brief that we might want to use later
            float[] last_buyy_price = new float[StockCount];
            float[] last_sell_price = new float[StockCount];
            for (int s = 0; s < StockCount; s++)
            {
                last_buyy_price[s] = briefCoin.processingBrief.symbols[s].buyy_price;
                last_sell_price[s] = briefCoin.processingBrief.symbols[s].sell_price;
            }
               
            //////////////// Flip the coin /////////////////////////
            GetANewBrfReadyRightBeforeFlip();
            briefCoin.FlipOver();
                       

            ///////////// Process Extended indexes ///////////////////
            float[] high = new float[StockCount];
            float[] low = new float[StockCount];
            float[] close = new float[StockCount];
            float[] vol = new float[StockCount];
            for (int s = 0; s < StockCount; s++)
            {
                high[s] = briefCoin.processingBrief.symbols[s].price_high;
                low[s] = briefCoin.processingBrief.symbols[s].price_loww;
                close[s] = briefCoin.processingBrief.symbols[s].price_last;
                vol[s] = briefCoin.processingBrief.symbols[s].volume_ths;
            }
            extendedIndicators.AddBrfRow(high, low, close, vol);
            float[/*symbol*/][/*ind*/] extIndexes = extendedIndicators.CalcIndicators();


            //////////// Finalize - Add Statistics info /////////////////////
            for (int s = 0; s < StockCount; s++)
            {
                PartialBriefWithPrices b = briefCoin.processingBrief.symbols[s];
                Stat.SimpleStatisticsFloatResults r = Stat.SimpleStatistics(b.prices, b.price_last);
                b.price_medn = r.median;
                b.price_mean = r.mean;
                b.price_mode = r.mode;
                b.prcModeCnt = r.modeCount;
                b.extIndex00 = extIndexes[s][0];
                b.extIndex01 = extIndexes[s][1];
                b.extIndex02 = extIndexes[s][2];
                b.extIndex03 = extIndexes[s][3];
                b.extIndex04 = extIndexes[s][4];
                b.extIndex05 = extIndexes[s][5];
                b.extIndex06 = extIndexes[s][6];
                b.extIndex07 = extIndexes[s][7];
                b.extIndex08 = extIndexes[s][8];
                //if (b.buyy_price < 0) // >= last_buyy_price[s]
                //    b.buyy_price = 0;
                //if (b.sell_price < 0) // <= last_sell_price[s]
                //    b.sell_price = 0;
            }


            /////////// Make sure we are not still waiting for any initial data ////////////////  
            if (MaxWaitingLoops > 0)
            {
                for (int i = 0; i < StockCount; i++)
                {
                    PartialBriefWithPrices bs = briefCoin.processingBrief.symbols[i];

                    if (waitingForData[i, 0] && !(bs.volume_day > 1.00)) log.Debug("Waiting for: {0}({1})-volume_day > 1     val:{2}", stockTickers[i], i, bs.volume_day); else waitingForData[i, 0] = false;
                    if (waitingForData[i, 1] && !(bs.volume_ths > 1.00)) log.Debug("Waiting for: {0}({1})-volume_ths > 1     val:{2}", stockTickers[i], i, bs.volume_ths); else waitingForData[i, 1] = false;
                    if (waitingForData[i, 2] && !(bs.price_askk > 0.01)) log.Debug("Waiting for: {0}({1})-price_askk > 0.01  val:{2}", stockTickers[i], i, bs.price_askk); else waitingForData[i, 2] = false;
                    if (waitingForData[i, 3] && !(bs.price_bidd > 0.01)) log.Debug("Waiting for: {0}({1})-price_bidd > 0.01  val:{2}", stockTickers[i], i, bs.price_bidd); else waitingForData[i, 3] = false;
                    if (waitingForData[i, 4] && !(bs.volume_bid > 1.00)) log.Debug("Waiting for: {0}({1})-volume_bid > 1     val:{2}", stockTickers[i], i, bs.volume_bid); else waitingForData[i, 4] = false;
                    if (waitingForData[i, 5] && !(bs.volume_ask > 1.00)) log.Debug("Waiting for: {0}({1})-volume_ask > 1     val:{2}", stockTickers[i], i, bs.volume_ask); else waitingForData[i, 5] = false;
                    waitingForData[i, 6] = false; //if (waitingForData[i, 6] && !(bs.buyy_price > 0.01)) logger.Debug("Waiting for: {0}({1})-buyy_price > 0.01  val:{2}", symbols[i], i, bs.buyy_price); else waitingForData[i, 6] = false;
                    waitingForData[i, 7] = false; //if (waitingForData[i, 7] && !(bs.sell_price > 0.01)) logger.Debug("Waiting for: {0}({1})-sell_price > 0.01  val:{2}", symbols[i], i, bs.sell_price); else waitingForData[i, 7] = false;
                    if (waitingForData[i, 8] && !(bs.price_last > 0.01)) log.Debug("Waiting for: {0}({1})-price_last > 0.01  val:{2}", stockTickers[i], i, bs.price_last); else waitingForData[i, 8] = false;
                    waitingForData[i, 9] = false; //if (waitingForData[i, 9 ] && !(bs.largTrdVol > 1.00)) logger.Debug("Waiting for: {0}({1})-largTrdVol > 1    val:{2}", symbols[i], i, bs.largTrdVol); else waitingForData[i, 9] = false;
                    waitingForData[i, 10] = false;//if (waitingForData[i, 10] && !(bs.vol_at_ask > 1.00)) logger.Debug("Waiting for: {0}({1})-vol_at_ask > 1    val:{2}", symbols[i], i, bs.vol_at_ask); else waitingForData[i, 10] = false;
                    waitingForData[i, 11] = false;//if (waitingForData[i, 11] && !(bs.vol_no_chg > 1.00))logger.Debug("Waiting for: {0}({1})-vol_no_chg > 1     val:{2}", symbols[i], i, bs.vol_no_chg); else waitingForData[i, 11] = false;
                    waitingForData[i, 12] = false;//if (waitingForData[i, 12] && !(bs.vol_at_bid > 1.00))logger.Debug("Waiting for: {0}({1})-vol_at_bid > 1     val:{2}", symbols[i], i, bs.vol_at_bid); else waitingForData[i, 12] = false;
                    // Set the remaining to false since we don't need to wait for data on other tick-types.
                    for (int s = 13; s < StockCount; s++)
                        waitingForData[i, s] = false;
                }

                //lets check all the symbols and their items to see if data has been received.
                bool waitingForDataStill = false;
                for (int i = 0; i < StockCount; i++)
                    for (int a = 0; a < StockAttribCount; a++)
                    if (waitingForData[i, a]) 
                        waitingForDataStill = true;
                
                //3 choices here: (1) still waiting for data  (2) all items now have data  (3) this is the last loop and all data has not been filed, so lets fill in the data as best as we could

                if (waitingForDataStill && (MaxWaitingLoops > 1))  // still waiting for data
                {
                    log.Info("Waiting for all items to have data. ({0} retries left before overriding)", MaxWaitingLoops);
                    MaxWaitingLoops--;
                    return;
                }
                else if (waitingForDataStill && (MaxWaitingLoops == 1)) // this is the last loop and all data has not been filed, so lets fill in the data as best as we could
                {
                    log.Info("Waiting for all items to have data but max loop count hit. Filling data with similar data as best as possible.");
                    MaxWaitingLoops = 0;
                    waitingForData = null;

                    //Attempt to Fill In Missing Information with similar information
                    for (int i = 0; i < StockCount; i++)
                    {
                        PartialBriefWithPrices bs = briefCoin.processingBrief.symbols[i];
                        if (bs.price_last < 0.01) bs.price_last = bs.price_bidd;
                        if (bs.price_askk < 0.01) bs.price_askk = bs.price_last;
                        if (bs.price_bidd < 0.01) bs.price_bidd = bs.price_last;
                        if (bs.volume_bid < 1.00) bs.volume_bid = bs.price_last;
                        if (bs.volume_ask < 1.00) bs.volume_ask = bs.price_last;
                        if (bs.buyy_price < 0.01) bs.buyy_price = bs.price_last;
                        if (bs.sell_price < 0.01) bs.sell_price = bs.price_last;
                    }
                }
                else if (waitingForDataStill == false)// All data found, lets free waitingForData
                {
                    log.Info("Required values have been filled in. countdown={0}", MaxWaitingLoops);
                    waitingForData = null; // waitingForData is no longer needed.
                    MaxWaitingLoops = 0;
                }
            }

            //////////////// Lets make sure everything is in range ////////////////
            for (int symbID = 0; symbID < StockCount; symbID++)
            {
                PartialBriefWithPrices bs = briefCoin.processingBrief.symbols[symbID];

                //////////////// Fill in any missing last, buy, or sell prices with a synthetic value ////////////////
                float last; // either the last price or if last is not valid than (ask-.005) or (bid+.005)
                if (bs.price_last > 0.03f && bs.price_last < 2000.0f)
                    last = bs.price_last;
                else if (bs.price_askk > 0.03f && bs.price_askk < 2000.0f)
                    last = bs.price_askk - 0.005F;
                else if (bs.price_bidd > 0.03f && bs.price_bidd < 2000.0f)
                    last = bs.price_bidd + 0.005F;
                else
                {
                    last = 0.0F;
                    if (ErrorCounters[7]-- > 0) 
                        log.Warn("Unable to create synthetic last price({0}) for symbID:{1}. Could not substitute using Bid({2}) or Ask({3}).", bs.price_last, symbID, bs.price_askk, bs.price_bidd);
                    //return;
                }

                /////////// Warn for data that is out of range  ////////////////  
                if (cbLogErrorChecking.Checked)
                {
                    StringBuilder sb = new StringBuilder();
                    LogIfOutOfRange(sb, bs.price_last, 0.02f, 2000.0f, symbID, time, "price_last");
                    LogIfOutOfRange(sb, bs.buyy_price, -2000.0f, 2000.0f, symbID, time, "buyy_price");
                    LogIfOutOfRange(sb, bs.sell_price, -2000.0f, 2000.0f, symbID, time, "sell_price");
                    LogIfOutOfRange(sb, bs.prcModeCnt, 0.00F, 100.0f, symbID, time, "prcModeCnt");
                    LogIfOutOfRange(sb, bs.vol_at_ask, 0.00F, 100.0f, symbID, time, "vol_at_ask");
                    LogIfOutOfRange(sb, bs.vol_no_chg, 0.00F, 100.0f, symbID, time, "vol_no_chg");
                    LogIfOutOfRange(sb, bs.vol_at_bid, 0.00F, 100.0f, symbID, time, "vol_at_bid");
                    LogIfOutOfRange(sb, bs.volume_day, 1.00F, 9999999, symbID, time, "volume_day");
                    LogIfOutOfRange(sb, bs.volume_ths, 0.00F, 999999, symbID, time, "volume_ths");
                    LogIfOutOfRange(sb, bs.largTrdPrc, 0.02f, 2000.0f, symbID, time, "largTrdPrc");
                    LogIfOutOfRange(sb, bs.price_high, 0.02f, 2000.0f, symbID, time, "price_high");
                    LogIfOutOfRange(sb, bs.price_loww, 0.02f, 2000.0f, symbID, time, "price_loww");
                    LogIfOutOfRange(sb, bs.price_bidd, 0.02f, 2000.0f, symbID, time, "price_bidd");
                    LogIfOutOfRange(sb, bs.price_askk, 0.02f, 2000.0f, symbID, time, "price_askk");
                    LogIfOutOfRange(sb, bs.volume_bid, 0.00F, 500000, symbID, time, "volume_bid");
                    LogIfOutOfRange(sb, bs.volume_ask, 0.00F, 500000, symbID, time, "volume_ask");
                    LogIfOutOfRange(sb, bs.price_medn, 0.02f, 2000.0f, symbID, time, "price_medn");
                    LogIfOutOfRange(sb, bs.price_mean, 0.02f, 2000.0f, symbID, time, "price_mean");
                    LogIfOutOfRange(sb, bs.price_mode, 0.02f, 2000.0f, symbID, time, "price_mode");
                    LogIfOutOfRange(sb, bs.largTrdVol, 0.00F, 999999, symbID, time, "largTrdVol");
                    if (sb.Length > 0)
                        log.Warn(sb.ToString());
                }

                // setup buy/sell price for "Relative orders" 
                float buyyPrc = Math.Abs(bs.buyy_price);
                float sellPrc = Math.Abs(bs.sell_price);
                float buyy; // either the buy price or if buy is not valid then (sell_price+.01) or (last + .005)
                if (buyyPrc > 0.03f && buyyPrc < 2000.0f)
                    buyy = buyyPrc;
                else if (sellPrc > 0.03f && sellPrc < 2000.0f)
                    buyy = sellPrc + 0.01F;
                else
                    buyy = last + 0.005F;

                float sell; // either the buy price or if buy is not valid then (buyy_price-.01) or (last - .005)
                if (sellPrc > 0.03f && sellPrc < 2000.0f)
                    sell = bs.sell_price;
                else
                    sell = buyy - 0.01F;

                // removed on 4/21/2014 - this would slightly randomize the buy and sell price
                ///////////// Slightly randomize the buy/sell prices  ////////////////
                //if (buyy > 0.03f)
                //    buyy += (float)((Ryan.utils.rand.NextDouble() - .5) / 3000.0);
                //if (sell > 0.03f)
                //    sell += (float)((Ryan.utils.rand.NextDouble() - .5) / 3000.0);


                // added back in on 4/21/2014
                //if the prices are really close together or sell>max then lets create a .005 gap. 
                if (buyy - sell  < 0.005) //if the prices are really close together or sell>max then lets create a .005 gap. 
                {
                    float midPoint = (buyy + sell) / 2.0f;
                    buyy = midPoint + 0.0025F;  // after randomizing the buy
                    sell = midPoint - 0.0025F;
                }

                bs.price_last = last;
                bs.buyy_price = buyy;// removed 4/21/2014 Math.Max(0, buyy); 
                bs.sell_price = sell;// removed 4/21/2014 Math.Max(0, sell);

                //////////////// Repair values as much as possible ////////////////////////////
                bs.price_high = Math.Max(last, bs.price_high);
                bs.price_loww = (bs.price_loww > 0.03f) ? Math.Min(last, bs.price_loww) : last;

                // As a precaution, in case the bid is not filled in, lets fake bid instead of using 0.
                // A .3% spread is usually a typical amount for high volume stocks during trading hours.
                if (bs.price_bidd < 0.03f)
                    bs.price_bidd = ((bs.price_askk >= 0.03f)? bs.price_askk : bs.price_last) * 0.997f; 
                if (bs.price_askk < 0.03f)
                    bs.price_askk = bs.price_bidd * 0.997f; // A .3% spread is usually a typical amount for high volume stocks during trading hours.

                // if bid > ask then its probably because we might have missed something as
                // this is rare or maybe not even possible because a trade will happen. Lets
                // just average them and set them equal if this is the case. 
                if (bs.price_bidd > bs.price_askk)
                    bs.price_bidd = bs.price_askk = (bs.price_bidd + bs.price_askk) / 2;

                // bid/ask usually should not be more the 1% + 0.01 over/under last (for high volume stocks during trading hours)
                if (bs.price_askk < last * 0.99f - 0.01f)
                    bs.price_askk = last * 0.99f - 0.01f;
                if (bs.price_bidd <= 0.03f)
                    bs.price_bidd = last;
                else if (bs.price_bidd > last * 1.01f + 0.01f)
                    bs.price_bidd = last * 1.01f + 0.01f;

                if (bs.largTrdPrc < 0.03f || bs.largTrdPrc > 2000.0f)
                    bs.largTrdPrc = last;
                if (bs.price_medn < 0.03f || bs.price_medn > 2000.0f)
                    bs.price_medn = last;
                if (bs.price_mean < 0.03f || bs.price_mean > 2000.0f)
                    bs.price_mean = last;
                if (bs.price_mode < 0.03f || bs.price_mode > 2000.0f)
                    bs.price_mode = last;
            }


            /////////// Save results only if the market is open ///////////
            if (!IsNormalBusinessHoursForBrf(time))
            {
                log.Debug("The market is closed.  Hours: {0}-{1}  Time: {2}" + TinyTime6Sec.START_HOUR, TinyTime6Sec.END_HOUR, time);
                if (!inBulkMode)
                    Invoke((MethodInvoker)delegate { toolStripStatusLabelMarketOpen.Text = "Closed"; });  // delegate not really needed anymore because this runs on the UI thread
            }
            else  // Upload Briefs
            {
                log.Debug("Processing the 6th streamMoment {0} for Brief {1}", time, (TinyTime6Sec)time);
                TinyTime6Sec tinyTime = time;

                // lets check to see if this is the next expected TT
                if (nextExpectedTinyTime != tinyTime)
                {
                    log.Warn("briefID will not be continuous. The next expected briefID was {0} but processing {1}", (TinyTime6Sec)nextExpectedTinyTime, tinyTime);
                    nextExpectedTinyTime = tinyTime;
                }
                nextExpectedTinyTime ++;

                //Save it to the database    
                Brief briefToSave = new Brief()
                    {
                        BriefID = tinyTime,
                        BriefBytes = briefCoin.processingBrief.GetAsBinary(tinyTime)
                    };
                
                try
                {
                    if (inBulkMode)
                    {
                        if (briefToSave.BriefID > onlySaveIfAfter) //when replaying, we don't want to save brfs that are already there
                        {
                            log.Debug("Adding brief {0} for later DB update.", briefToSave.BriefID);
                            //int count = dc.GetChangeSet().Inserts.Count();

                            bulkBriefsToSubmit.Add(briefToSave);

                            //if (bulkBriefsToSubmit.Count > 1000)
                            //    SubmitPendingBriefs();
                        }
                        else
                            log.Debug("Skipping save Brief_{0} to DB because <= {1}(onlySaveIfAfter).", briefToSave.BriefID, onlySaveIfAfter);

                    }
                    else
                        using (var dc = new DataClassesBrfsDataContext())
                        {
                            log.Debug("Opening DB, submitting single brief({0}), then closing.", briefToSave.BriefID);
                            dc.Briefs.InsertOnSubmit(briefToSave);
                            dc.SubmitChanges(ConflictMode.ContinueOnConflict);
                        }
                }
                catch (Exception ex)
                {
                    log.Error("Error submitting new brief({0}) to DB. Msg:{1}", (int)tinyTime, ex.Message);
                }
                
                if (!inBulkMode)
                    Invoke((MethodInvoker)delegate { 
                        toolStripStatusLabelLastBrfID.Text = ((int)tinyTime).ToString();
                        toolStripStatusLabelMarketOpen.Text = "Open";
                    });  // runs on UI thread 
            }

            // Reset commonIndexes (note: commonIndexes are not flipped like a coin) 
            //todo:this is not perfect. Since we allow updates to commonIndexes a few milliseconds 
            //     after we flip the coin(and before we upload it) that is not consistent with the 
            //     normal stock values. I think its okay though for now.
            indexes[0].tickTypeAttrib[(int)IB_TickType.lastPc_4].StartNexPeriod(); 
            indexes[1].tickTypeAttrib[(int)IB_TickType.bidSz__0].StartNexPeriod(); 
            indexes[1].tickTypeAttrib[(int)IB_TickType.bidPc__1].StartNexPeriod(); 
            indexes[1].tickTypeAttrib[(int)IB_TickType.askPc__2].StartNexPeriod(); 
            indexes[2].tickTypeAttrib[(int)IB_TickType.bidPc__1].StartNexPeriod(); 
            indexes[2].tickTypeAttrib[(int)IB_TickType.askPc__2].StartNexPeriod(); 
            indexes[3].tickTypeAttrib[(int)IB_TickType.lastPc_4].StartNexPeriod(); 
            indexes[4].tickTypeAttrib[(int)IB_TickType.bidSz__0].StartNexPeriod(); 
            indexes[4].tickTypeAttrib[(int)IB_TickType.bidPc__1].StartNexPeriod(); 
            indexes[4].tickTypeAttrib[(int)IB_TickType.askPc__2].StartNexPeriod(); 
            indexes[5].tickTypeAttrib[(int)IB_TickType.bidSz__0].StartNexPeriod(); 
            indexes[5].tickTypeAttrib[(int)IB_TickType.bidPc__1].StartNexPeriod(); 
            indexes[5].tickTypeAttrib[(int)IB_TickType.askPc__2].StartNexPeriod(); 
            indexes[6].tickTypeAttrib[(int)IB_TickType.bidPc__1].StartNexPeriod(); 
            indexes[6].tickTypeAttrib[(int)IB_TickType.askPc__2].StartNexPeriod(); 
            indexes[6].tickTypeAttrib[(int)IB_TickType.lastPc_4].StartNexPeriod(); 

            log.Debug("Exiting Fi4nalizeAndUploadBrief() time={0}, bulkMode={1}", time.ToString("ddd M/d/yy h:mm:ss.f tt"), inBulkMode);
            high = null;
            low = null;
            close = null;
            vol = null;
        }

        private void SubmitPendingBriefs()
        {
            if (bulkBriefsToSubmit.Count > 0)
            {
                try
                {
                    using (var dc = new DataClassesBrfsDataContext())
                    {
                        foreach (var brief in bulkBriefsToSubmit)
                            dc.Briefs.InsertOnSubmit(brief);

                        // We don't want to get to many records before we submit! (note: dataContext is already open)
                        log.Info("Submitting {0} Briefs (last BriefID: {1})", bulkBriefsToSubmit.Count(), bulkBriefsToSubmit.Last().BriefID);
                        bulkBriefsToSubmit.Clear();

                        dc.SubmitChanges(ConflictMode.ContinueOnConflict);
                    }
                }
                catch (Exception ex)
                {
                    log.Error("Error submitting new streamMoments: {0}", ex.Message);
                }
            }
        }

        private bool IsNormalBusinessHoursForBrf(DateTime time)
        {
            bool isWeekend = (time.DayOfWeek == DayOfWeek.Saturday) || (time.DayOfWeek == DayOfWeek.Sunday);
            bool isMarketClosed = (time.TimeOfDay < BeginRecordTime) || (time.TimeOfDay > EndRecordTime); //Updated 3/19/2013 to ONLY market hours
            return !(isMarketClosed || isWeekend);
        }

        private bool IsNormalBusinessHoursForStreamProcessing(DateTime time)
        {
            TinyTime6Sec.CheckDateRange(time);
            bool isWeekend = (time.DayOfWeek == DayOfWeek.Saturday) || (time.DayOfWeek == DayOfWeek.Sunday);
            bool isMarketClosed = (time.TimeOfDay < PreBeginBufferTime) || (time.TimeOfDay > EndRecordTime); //Updated 3/19/2013 to ONLY market hours
            return !(isMarketClosed || isWeekend);
        }

        private void LogIfOutOfRange(StringBuilder sb, float value, float low, float high, int symbolID, DateTime time, string attribDesc)
        {
            if (value < low)
                sb.AppendFormat("{0} {4} is below the min value of {5} for {1}({2})-{3}.", time.ToString("G"), stockTickers[symbolID], symbolID, attribDesc, value, low);
            else if (value > high)
                sb.AppendFormat("{0} {4} is above the max value of {5} for {1}({2})-{3}.", time.ToString("G"), stockTickers[symbolID], symbolID, attribDesc, value, high);

            // todo: maybe use "ErrorCounters" here to prevent a flood of messages from happening. (it clogs up the system)
        }

        /// <summary>
        /// First thing called by each 6SecTimer tick. It prepares an empty brief using the
        /// brief that is just finishing.
        /// </summary>
        private void GetANewBrfReadyRightBeforeFlip()
        {
            //now get the brief ready for immediate use.
            for (int i = 0; i < StockCount; i++)
            {
                PartialBriefWithPrices processingBrf = briefCoin.processingBrief.symbols[i];
                PartialBriefWithPrices capBrf = briefCoin.capturingBrief.symbols[i];

                // Clear the price history
                processingBrf.prices.Clear();                    

                processingBrf.LastVolTickType = capBrf.LastVolTickType;
                processingBrf.lastTickType = capBrf.lastTickType;
                processingBrf.volume_day = capBrf.volume_day; //volume_day(carried)
                processingBrf.volume_ths = 0;                 //volume_ths(reset+running) 
                processingBrf.largTrdPrc = capBrf.price_last; //largTrdPrc(reset+running)
                processingBrf.price_high = capBrf.price_last; //price_high(reset+running) 
                processingBrf.price_loww = capBrf.price_last; //price_loww(reset+running) 
                processingBrf.price_last = capBrf.price_last; //price_last(carried)
                processingBrf.price_bidd = capBrf.price_bidd; //price_bidd(carried) 
                processingBrf.price_askk = capBrf.price_askk; //price_askk(carried)                           
                processingBrf.volume_bid = capBrf.volume_bid; //volume_bid(carried) 
                processingBrf.volume_ask = capBrf.volume_ask; //volume_ask(carried)  
                processingBrf.buyy_price = capBrf.buyy_price; //buyy_price(carried) 
                processingBrf.sell_price = capBrf.sell_price; //sell_price(carried)  
                //price_medn                                  //price_medn(overwritten)
                //price_mean                                  //price_mean(overwritten)
                //price_mode                                  //price_mode(overwritten) 
                //prcModeCnt                                  //prcModeCnt(overwritten)
                processingBrf.vol_at_ask = 0;                 //vol_at_ask(reset+running) 
                processingBrf.vol_no_chg = 0;                 //vol_no_chg(reset+running)
                processingBrf.vol_at_bid = 0;                 //vol_at_bid(reset+running)
                processingBrf.largTrdVol = 0;                 //largTrdVol(reset+running)
                processingBrf.BidUpTicks = 0;                 //BidUpTicks(reset+running)
                processingBrf.BidDnTicks = 0;                 //BidDnTicks(reset+running)
                processingBrf.sale_count = 0;                 //sale_count(reset+running)
            }
        }

        float[] volumeLast = new float[StockCount];
        private void SolidifyBuySellPrices()
        {
            // Example for buy
            // On each Volume Update
            // float buyPoints = bid/VolChange
            // float askPoints = (ask/VolChange) * 0.25
            // if buyPrice is <=0 then
            //   if ((buyPoints-askPoints) * Random() > 10/*threshHold*/)
            //     buyPrice = Bid
            
            // On each Bid/Ask Update
            // if buyPrice is <=0 then
            //   if (did the Bid Cost Move Up)
            //     buyPrice = Bid

            // on SolidifyBuySellPrices() we just reset the values to 0

            //StringBuilder sb = new StringBuilder();
            //string[] TickTypes = { "bidSz", "bid", "ask", "askSz", "last", "lastSz", "??6", "??7", "vol", "??", "??10", "??11" };
            for (int i = 0; i < StockCount; i++)
            {
                PartialBriefWithPrices b = briefCoin.capturingBrief.symbols[i]; //updated 3/17/2013 - changed from processingBrfSet to capturingBrief
                //No locks needed. Reads/Writes to aligned memory locations no larger than the native; word size are atomic when all the write accesses to a location are the same size

                //sb.AppendLine(symbols[i] + ", " + b.buyy_price + ", " + b.sell_price.ToString() + ", " + b.volume_ask.ToString() + ", " + b.volume_bid.ToString() + ", " + (volumeLast[i]  -  b.volume_day).ToString());

                b.sell_price = -b.price_bidd;
                b.buyy_price = -b.price_askk;

                volumeLast[i] = b.volume_day; //todo: why is this here and not in GetANewBrfReadyRightBeforeFlip()?
            }

            //DumpTextToDesktopFile(sb); //for debug

            log.Debug("Mid Snapshot completed.");  
        }

        private void ContributeToBrf(IB_TickType tickType, byte symbol, float val)
        {
            PartialBriefWithPrices b = briefCoin.capturingBrief.symbols[symbol];
            briefCoin.CheckoutCapturing();
            switch ((byte)tickType)
            {
                case 0:/*bidSz*/
                    if (val > b.volume_bid)  
                        b.BidUpTicks++;

                    if (val < b.volume_bid)
                        b.BidDnTicks++;

                    b.volume_bid = val;  
                    break; 
                case 1:/*bid  */
                    // if bid goes up we set b.buyy_price to positive to indicate a guaranteed buy
                    if (val < b.price_bidd)
                        b.buyy_price = Math.Abs(b.buyy_price);

                    b.price_bidd = val; break;

                case 2:/*ask  */
                    // if ask goes up we set b.sell_price to positive to indicate a guaranteed sell
                    if (val > b.price_askk)
                        b.sell_price = Math.Abs(b.sell_price);

                    b.price_askk = val; break;

                case 3:/*askSz*/ b.volume_ask = val; break; 
                case 4:/*last */
                    if (b.price_loww > val)
                        b.price_loww = val;
                    if (b.price_high < val)
                        b.price_high = val;
                    b.price_last = val;
                    break;
                case 5: /*last size */
                    if (b.LastVolTickType == 5/*last size */) //prevent duplicate tick types; there should be a volume update for each last update
                        break;

                    if (b.price_askk <= b.price_last)       // Up Tick    
                        b.vol_at_ask += val;

                    else if (b.price_bidd >= b.price_last)  // Down Tick       
                        b.vol_at_bid += val;

                    if ((int)b.lastTickType != 4) 
                        b.vol_no_chg += val;
   
                    //b.volume_day += val;
                    if (b.largTrdVol < val)
                    {
                        b.largTrdVol = val;
                        // sometimes prices will be empty (if there are no trades)
                        if (b.prices.Count > 0) 
                            b.largTrdPrc = b.prices.Last(); // get the most recent price
                    }
                    
                    b.sale_count++;
                    b.volume_ths += val;
                    b.prices.Add(b.price_last);
                    b.LastVolTickType = 5/*last size */;
                    break;
                case 8: /*vol  */ 
                // On each Volume Update we randomly fill b.buyy_price
                    //if (b.buyy_price == 0.0 && b.volume_ask != 0 && b.volume_bid != 0 && b.volume_day != 0) 
                    //{
                    //    float changeInVolume = val - b.volume_day;
                    //    float bidPoints = changeInVolume / b.volume_bid;
                    //    float askPoints = (changeInVolume / b.volume_ask) * 0.25f;
                    //    int points = rand.Next(-400/*threshHold*/, (int)(Math.Max(10/*threshHold*/, 1000 * (bidPoints - askPoints))));//(bidPoints - askPoints) * ((float)rand.Next() / 2147483648);
                    //    if (points >= 0)
                    //        b.buyy_price = b.price_bidd;   
                    //}
                    //if (b.sell_price == 0.0 && b.volume_ask != 0 && b.volume_bid != 0 && b.volume_day != 0) 
                    //{
                    //    float changeInVolume = val - b.volume_day;
                    //    float askPoints = changeInVolume / b.volume_ask ;
                    //    float bidPoints = (changeInVolume / b.volume_bid ) * 0.25f;
                    //    //float randVal = ((float)rand.Next() / 2147483648.0f);
                    //    float points = rand.Next(-400/*threshHold*/, (int)(Math.Max(10/*threshHold*/, 1000 * (askPoints - bidPoints))));//(askPoints - bidPoints) * randVal;
                    //    if (points >= 0)
                    //        b.sell_price = b.price_askk;
                    //}     

                    b.volume_day = val;
                    b.LastVolTickType = 8/*last size */;
                    break; //(uint)(val / 100)
                // never gets hit case 6: /*high */ b.price_high = (ushort)(val * 100); break;
                // never gets hit case 7: /*low  */ b.price_loww = (ushort)(val * 100); break;
                // never gets hit case 9: /*close*/ break;
                default:
                    log.Debug("Unknown TickType. Type:{0} SymbID:{1} val:{2}", tickType, symbol, val);  

                    break;
            } //Switch
            b.lastTickType = tickType;
            briefCoin.CheckInCapturing();
        }

        private void btnRebuildDBfromStreamMoments_Click(object sender, EventArgs e)
        {
            if (DialogResult.Yes != MessageBox.Show("Delete all data from the Briefs and Predictions tables?", "Warning", MessageBoxButtons.YesNo))
                return;
            
            suspendWCF = true;
            MaxWaitingLoops = 250;
            totalCaptureEventsForDisplay = 0; 
            log.Debug("Deleting all briefs and Predictions in DB...");
            using (var dc = new DataClassesBrfsDataContext())
            {
                dc.CommandTimeout = 500;
                dc.ExecuteCommand("Delete from Preds");
                dc.ExecuteCommand("Delete from Briefs");
            }

            UnLoadLoadedBriefs();
            LoadSystem();
        }

        private void BriefMaker_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Close the ServiceHostBase to shutdown the service.
            // wcfReceiverHost.Close(); //todo: need to add this line (not tested however)
            UnLoadLoadedBriefs(); 
        }

        private void UnLoadLoadedBriefs()
        {
            log.Info("Unloading loaded Briefs...");
            mostRecentTickSubmitted = 0;
        }

        /// <summary>
        /// Tool to aid in debugging. It dumps a StringBuilder to the desktop.
        /// </summary>
        private static void DumpTextToDesktopFile(StringBuilder sb)
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            using (StreamWriter outfile = new StreamWriter(desktop + @"\dumpDebugFile.txt", true))
                outfile.Write(sb.ToString());
        }
    }
}