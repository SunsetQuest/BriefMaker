// BriefMaker - converts market stream data to time-interval snapshots
// This projected is licensed under the terms of the MIT license.
// NO WARRANTY. THE SOFTWARE IS PROVIDED TO YOU “AS IS” AND “WITH ALL FAULTS.”
// ANY USE OF THE SOFTWARE IS ENTIRELY AT YOUR OWN RISK.
// Copyright (c) 2013, 2014, 2015, 2016 Ryan S. White

using System;
using TicTacTec.TA.Library;
using System.Threading.Tasks;

// Information source: http://seekingalpha.com/author/paul-zimbardo
// Information source (6-17-2013) https://github.com/dennislwm/MLEA/blob/master/MLEA/TaLibTest.cs

namespace BM
{
    class ExtendedIndicator
    {
        // Extended related - these the use a high/low/close/vol history table with the TicTacTec library to build a image type field.
        const int HIST_BUF_SZ = 1024;       // a guess; The maximum size we will allow an indexes to process.
        public readonly int hist_min_sz;    // aids when reorganizing buffer; also aids in when to skip calcIndex; The minimum size we needed to establish the most needy indexes.
        private static NLog.Logger logger;  //note: must be initialized in Load so NLog.config is read
         
        int histCt = 0;
        int symbolCt;
        float[/*StockCount*/][/*hist_max_sz*/] hist_hi, hist_lo, hist_cl, hist_vo;  // [symbolCt][hist_max_sz]
        float[/*StockCount*/][/*indCt*/] outputs; // [symbol][indCt]


        //////////////////////////////////////////
        // Source for some of this info: (http://search.cpan.org/~kmx/Finance-TA-v0.4.0/TA.pod)
        const int optInTimePeriod = 30;   
        const int optInTimePeriodSma = 10; 
        const int optInTimePeriodBBands = 20;
        const int optInTimePeriod14 = 14;
        const int optInTimePeriodT3 = 5;
        const int optInTimePeriodMom = 10;
        const int optInTimePeriodRoc = 10;
        const int optInTimePeriodBeta = 5;
        const int optInTimePeriodStdDev = 5;
        const int optInTimePeriodVar = 5;
        const int optInTimePeriodKama = 9;

        const int optInFastPeriod = 12;
        const int optInSlowPeriod = 26;
        const int optInFastPeriod_AdOsc = 3;
        const int optInSlowPeriod_AdOsc = 10;
        const int optInSignalPeriod = 9;
        const int optInSlowK_Period = 3;
        const int optInSlowD_Period = 3;
        const int optInFastK_Period = 3;
        const int optInFastD_Period = 3;
        const int optInTimePeriod1 = 7;
        const int optInTimePeriod2 = 14;
        const int optInTimePeriod3 = 28;

        const double optInNbDevUp = 2.0;
        const double optInNbDevDn = 2.0;
        const double optInPenetration = 0.3;
        const double optInFastLimit = 0.5;
        const double optInSlowLimit = 0.5;
        const double optInAcceleration = 0.02;
        const double optInMaximum = 0.2;
        const double optInNbDev = 1;
        const double optInStartValue = 0;
        const double optInOffsetOnReverse = 0;
        const double optInAccelerationInitLong = 0.02;
        const double optInAccelerationLong = 0.02;
        const double optInAccelerationMaxLong = 0.2;
        const double optInAccelerationInitShort = 0.02;
        const double optInAccelerationShort = 0.02;
        const double optInAccelerationMaxShort = 0.2;
        const double optInVFactor = 0.7;

        const Core.MAType optInMAType = Core.MAType.Sma;
        const Core.MAType optInSlowK_MAType = Core.MAType.Sma;
        const Core.MAType optInSlowD_MAType = Core.MAType.Sma;
        const Core.MAType optInFastD_MAType = Core.MAType.Sma;
        const Core.MAType optInFastMAType = Core.MAType.Sma;
        const Core.MAType optInSlowMAType = Core.MAType.Sma;
        const Core.MAType optInSignalMAType = Core.MAType.Sma;


        public ExtendedIndicator(int symbolCt)
        {
            logger = NLog.LogManager.GetCurrentClassLogger();

            this.symbolCt = symbolCt;

            // Initialize extended items
            hist_hi = new float[symbolCt][]; 
            hist_lo = new float[symbolCt][];
            hist_cl = new float[symbolCt][];
            hist_vo = new float[symbolCt][];
            outputs = new float[symbolCt][];
            for (int i = 0; i < symbolCt; i++)  //hack: do we need this?
            {
                hist_hi[i] = new float[HIST_BUF_SZ];
                hist_lo[i] = new float[HIST_BUF_SZ];
                hist_cl[i] = new float[HIST_BUF_SZ];
                hist_vo[i] = new float[HIST_BUF_SZ];
                outputs[i] = new float[128];  // hack: 128 is a guess; will be able to support 128 indexes
            }

            // Set the minimum size; this is needed to re-align the array with a minimal copy
            // and to make sure we have enough initial data to generate the indexes.
            int min_Size = Core.BbandsLookback(optInTimePeriodBBands, optInNbDevUp, optInNbDevDn, optInMAType);
            min_Size = Math.Max(min_Size, Core.MacdExtLookback(optInFastPeriod, optInFastMAType, optInSlowPeriod, optInSlowMAType, optInSignalPeriod, optInSignalMAType));
            hist_min_sz = min_Size + 2; //lets just do a couple extra rows
        }

        /// <summary>
        /// Calculates and returns the latest indexes row. This function is typically called when finishing up a new brief.
        /// </summary>
        public float[/*symbol*/][/*ind*/] CalcIndicators()
        {
            if (histCt < hist_min_sz+2)
                return outputs; // returns all zeros

            int startIdx = histCt -1; // "-1" because we want the last element   
            int endIdx = histCt -1; //END_REC_TIME is the same as startIdx because we only want to get this one moment.

            Parallel.For(0, symbolCt, symbId =>
            {
                int id=0, outBegIdx, outNBElement;

                float[] inClose = hist_cl[symbId];
                float[] inLow = hist_lo[symbId];
                float[] inHigh = hist_hi[symbId];
                float[] inVolume = hist_vo[symbId];
                float[] inOpen = inClose;  
                float[] inReal = inClose;
                float[] inReal0 = inClose; //todo: this should usually be an indicator or something like else
                float[] inReal1 = inClose; //todo: this should usually be an indicator or something like else

                int[] tmpInt0 = new int[1];
                int[] tmpInt1 = new int[1];
                double[] tmpDub0 = new double[1];
                double[] tmpDub1 = new double[1];
                double[] tmpDub2 = new double[1];

                float[] curOutput = outputs[symbId];

            //ErrChk(Core.Cdl2Crows(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.Cdl3BlackCrows(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.Cdl3Inside(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.Cdl3LineStrike(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.Cdl3Outside(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.Cdl3StarsInSouth(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.Cdl3WhiteSoldiers(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlAbandonedBaby(startIdx, endIdx, inOpen, inHigh, inLow, inClose, optInPenetration, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlAdvanceBlock(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlBeltHold(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlBreakaway(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlClosingMarubozu(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlConcealBabysWall(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlCounterAttack(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlDarkCloudCover(startIdx, endIdx, inOpen, inHigh, inLow, inClose, optInPenetration, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlDoji(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlDojiStar(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlEngulfing(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlEveningDojiStar(startIdx, endIdx, inOpen, inHigh, inLow, inClose, optInPenetration, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlEveningStar(startIdx, endIdx, inOpen, inHigh, inLow, inClose, optInPenetration, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlHammer(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlHangingMan(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlHarami(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlHaramiCross(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlHignWave(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlHikkake(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlHikkakeMod(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlHomingPigeon(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlIdentical3Crows(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlInNeck(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlInvertedHammer(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlKicking(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlKickingByLength(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlLadderBottom(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlLongLine(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlMarubozu(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlMatchingLow(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlMatHold(startIdx, endIdx, inOpen, inHigh, inLow, inClose, optInPenetration, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlMorningDojiStar(startIdx, endIdx, inOpen, inHigh, inLow, inClose, optInPenetration, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlMorningStar(startIdx, endIdx, inOpen, inHigh, inLow, inClose, optInPenetration, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlOnNeck(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlPiercing(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlRiseFall3Methods(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlSeperatingLines(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlShootingStar(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlShortLine(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlSpinningTop(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlStalledPattern(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlStickSandwhich(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlTasukiGap(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlThrusting(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlUnique3River(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlUpsideGap2Crows(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlXSideGap3Methods(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt)); output[id++]=(float)tmpInt[0]; //todo: removed because no changes; also need to add inOpen
            //ErrChk(Core.CdlDragonflyDoji(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt0)); curOutput[id++] = (float)tmpInt0[0]; //todo: works some but need to add inOpen
            //ErrChk(Core.CdlGapSideSideWhite(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt0)); curOutput[id++] = (float)tmpInt0[0]; //todo: works some but need to add inOpen
            //ErrChk(Core.CdlGravestoneDoji(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt0)); curOutput[id++] = (float)tmpInt0[0]; //todo: works some but need to add inOpen
            //ErrChk(Core.CdlLongLeggedDoji(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt0)); curOutput[id++] = (float)tmpInt0[0]; //todo: works some but need to add inOpen
            //ErrChk(Core.CdlRickshawMan(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt0)); curOutput[id++] = (float)tmpInt0[0]; //todo: works some but need to add inOpen
            //ErrChk(Core.CdlTakuri(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt0)); curOutput[id++] = (float)tmpInt0[0]; //todo: works some but need to add inOpen
            //ErrChk(Core.CdlTristar(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpInt0)); curOutput[id++] = (float)tmpInt0[0]; //todo: works some but need to add inOpen
            //ErrChk(Core.HtTrendMode(startIdx, endIdx, inReal, out outBegIdx, out outNBElement, tmpInt0)); curOutput[id++]=(float)tmpInt0[0]; //works, but I can only pick five so commenting out
            //ErrChk(Core.MaxIndex(startIdx, endIdx, inReal, optInTimePeriod, out outBegIdx, out outNBElement, tmpInt0)); curOutput[id++] = (float)tmpInt0[0]; //works, but I can only pick five so commenting out
            //ErrChk(Core.MinIndex(startIdx, endIdx, inReal, optInTimePeriod, out outBegIdx, out outNBElement, tmpInt0)); curOutput[id++] = (float)tmpInt0[0]; //works, but I can only pick five so commenting out
            //ErrChk(Core.MinMaxIndex(startIdx, endIdx, inReal, optInTimePeriod, out outBegIdx, out outNBElement, /*MinIdx*/ tmpInt0, /*MaxIdx*/ tmpInt1)); curOutput[id++] = (float)tmpInt0[0]; curOutput[id++] = (float)tmpInt1[0]; //works, but I can only pick five so commenting out

              
            //ErrChk(Core.Acos(startIdx, endIdx, inReal, out outBegIdx, out outNBElement, tmpDub)); output[id++]=(float)tmpDub[0]; //todo: removed because no changes
            //ErrChk(Core.Asin(startIdx, endIdx, inReal, out outBegIdx, out outNBElement, tmpDub)); output[id++]=(float)tmpDub[0]; //todo: removed because no changes
            //ErrChk(Core.Bop(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpDub)); output[id++]=(float)tmpDub[0]; //todo: removed because no changes
            //ErrChk(Core.Ceil(startIdx, endIdx, inReal, out outBegIdx, out outNBElement, tmpDub)); output[id++]=(float)tmpDub[0]; //todo: removed because no changes
            //ErrChk(Core.Correl(startIdx, endIdx, inReal0, inReal1, optInTimePeriod, out outBegIdx, out outNBElement, tmpDub)); output[id++]=(float)tmpDub[0]; //todo: removed because no changes
            //ErrChk(Core.Div(startIdx, endIdx, inReal0, inReal1, out outBegIdx, out outNBElement, tmpDub)); output[id++]=(float)tmpDub[0]; //todo: removed because no changes
            //ErrChk(Core.Floor(startIdx, endIdx, inReal, out outBegIdx, out outNBElement, tmpDub)); output[id++]=(float)tmpDub[0]; //todo: removed because no changes
            //ErrChk(Core.Sub(startIdx, endIdx, inReal0, inReal1, out outBegIdx, out outNBElement, tmpDub)); output[id++]=(float)tmpDub[0]; //todo: removed because no changes
            //ErrChk(Core.MovingAverageVariablePeriod(startIdx,endIdx,inReal,inPeriods,optInMinPeriod,optInMaxPeriod,optInMAType, out outBegIdx, out outNBElement, tmpDub)); output[id++]=(float)tmpDub[0]; momentTime.Stop(); totalTicks += momentTime.ElapsedTicks; Console.WriteLine(tmpDubDesc[w-1] + "Output Begin:" + outBegIdx + "  Output length:" + outNBElement + " Time: " + momentTime.ElapsedMilliseconds + "ms" +"("+ momentTime.ElapsedTicks + " ticks)");},
            //ErrChk(Core.Ad(startIdx, endIdx, inHigh, inLow, inClose, inVolume, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Add(startIdx, endIdx, inReal0, inReal1, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.AdOsc(startIdx, endIdx, inHigh, inLow, inClose, inVolume, optInFastPeriod_AdOsc, optInSlowPeriod_AdOsc, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Adx(startIdx, endIdx, inHigh, inLow, inClose, optInTimePeriod14, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Adxr(startIdx, endIdx, inHigh, inLow, inClose, optInTimePeriod14, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Apo(startIdx, endIdx, inReal, optInFastPeriod, optInSlowPeriod, optInMAType, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.AroonOsc(startIdx, endIdx, inHigh, inLow, optInTimePeriod14, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Atan(startIdx, endIdx, inReal, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            ErrChk(Core.Atr(startIdx, endIdx, inHigh, inLow, inClose, optInTimePeriod14, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.AvgPrice(startIdx, endIdx, inOpen, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Beta(startIdx, endIdx, inReal0, inReal1, optInTimePeriodBeta, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            ErrChk(Core.Cci(startIdx, endIdx, inHigh, inLow, inClose, optInTimePeriod14, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Cmo(startIdx, endIdx, inReal, optInTimePeriod14, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Cos(startIdx, endIdx, inReal, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Cosh(startIdx, endIdx, inReal, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Dema(startIdx, endIdx, inReal, optInTimePeriod, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Dx(startIdx, endIdx, inHigh, inLow, inClose, optInTimePeriod14, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
              ErrChk(Core.Ema(startIdx, endIdx, inReal, optInTimePeriod, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Exp(startIdx, endIdx, inReal, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.HtDcPeriod(startIdx, endIdx, inReal, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.HtDcPhase(startIdx, endIdx, inReal, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.HtTrendline(startIdx, endIdx, inReal, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            ErrChk(Core.Kama(startIdx, endIdx, inReal, optInTimePeriodKama, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.LinearReg(startIdx, endIdx, inReal, optInTimePeriod14, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.LinearRegAngle(startIdx, endIdx, inReal, optInTimePeriod14, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.LinearRegIntercept(startIdx, endIdx, inReal, optInTimePeriod14, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.LinearRegSlope(startIdx, endIdx, inReal, optInTimePeriod14, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Ln(startIdx, endIdx, inReal, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Log10(startIdx, endIdx, inReal, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Max(startIdx, endIdx, inReal, optInTimePeriod, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.MedPrice(startIdx, endIdx, inHigh, inLow, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Mfi(startIdx, endIdx, inHigh, inLow, inClose, inVolume, optInTimePeriod14, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.MidPoint(startIdx, endIdx, inReal, optInTimePeriod14, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.MidPrice(startIdx, endIdx, inHigh, inLow, optInTimePeriod14, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Min(startIdx, endIdx, inReal, optInTimePeriod, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.MinusDI(startIdx, endIdx, inHigh, inLow, inClose, optInTimePeriod14, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.MinusDM(startIdx, endIdx, inHigh, inLow, optInTimePeriod14, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Mom(startIdx, endIdx, inReal, optInTimePeriodMom, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.MovingAverage(startIdx, endIdx, inReal, optInTimePeriod, optInMAType, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Mult(startIdx, endIdx, inReal0, inReal1, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Natr(startIdx, endIdx, inHigh, inLow, inClose, optInTimePeriod14, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Obv(startIdx, endIdx, inReal, inVolume, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.PlusDI(startIdx, endIdx, inHigh, inLow, inClose, optInTimePeriod14, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.PlusDM(startIdx, endIdx, inHigh, inLow, optInTimePeriod14, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Ppo(startIdx, endIdx, inReal, optInFastPeriod, optInSlowPeriod, optInMAType, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Roc(startIdx, endIdx, inReal, optInTimePeriodRoc, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.RocP(startIdx, endIdx, inReal, optInTimePeriodRoc, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.RocR(startIdx, endIdx, inReal, optInTimePeriodRoc, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.RocR100(startIdx, endIdx, inReal, optInTimePeriodRoc, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            ErrChk(Core.Rsi(startIdx, endIdx, inReal, optInTimePeriod14, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Sar(startIdx, endIdx, inHigh, inLow, optInAcceleration, optInMaximum, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Sin(startIdx, endIdx, inReal, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Sinh(startIdx, endIdx, inReal, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            ErrChk(Core.Sma(startIdx, endIdx, inReal, optInTimePeriodSma, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Sqrt(startIdx, endIdx, inReal, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.StdDev(startIdx, endIdx, inReal, optInTimePeriodStdDev, optInNbDev, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Sum(startIdx, endIdx, inReal, optInTimePeriod, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.T3(startIdx, endIdx, inReal, optInTimePeriodT3, optInVFactor, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Tan(startIdx, endIdx, inReal, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Tanh(startIdx, endIdx, inReal, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Tema(startIdx, endIdx, inReal, optInTimePeriod, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Trima(startIdx, endIdx, inReal, optInTimePeriod, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Trix(startIdx, endIdx, inReal, optInTimePeriod, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.TrueRange(startIdx, endIdx, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Tsf(startIdx, endIdx, inReal, optInTimePeriod14, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.TypPrice(startIdx, endIdx, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.UltOsc(startIdx, endIdx, inHigh, inLow, inClose, optInTimePeriod1, optInTimePeriod2, optInTimePeriod3, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Variance(startIdx, endIdx, inReal, optInTimePeriodVar, optInNbDev, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.WclPrice(startIdx, endIdx, inHigh, inLow, inClose, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.WillR(startIdx, endIdx, inHigh, inLow, inClose, optInTimePeriod14, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Wma(startIdx, endIdx, inReal, optInTimePeriod, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            ErrChk(Core.SarExt(startIdx, endIdx, inHigh, inLow, optInStartValue, optInOffsetOnReverse, optInAccelerationInitLong, optInAccelerationLong, optInAccelerationMaxLong, optInAccelerationInitShort, optInAccelerationShort, optInAccelerationMaxShort, out outBegIdx, out outNBElement, tmpDub0)); curOutput[id++]=(float)tmpDub0[0];
            //ErrChk(Core.Aroon(startIdx, endIdx, inHigh, inLow, optInTimePeriod14, out outBegIdx, out outNBElement,/*outAroonDn*/tmpDub0, /*outAroonUp*/tmpDub1)); curOutput[id++]=(float)tmpDub0[0]; curOutput[id++]=(float)tmpDub1[0];
            //ErrChk(Core.Mama(startIdx, endIdx, inReal, optInFastLimit, optInSlowLimit, out outBegIdx, out outNBElement, /*MAMA*/ tmpDub0, /*FAMA*/ tmpDub1)); curOutput[id++]=(float)tmpDub0[0]; curOutput[id++]=(float)tmpDub1[0];
            //ErrChk(Core.HtPhasor(startIdx, endIdx, inReal, out outBegIdx, out outNBElement, /*InPhase*/tmpDub0, /*outQuadrature*/tmpDub1)); curOutput[id++]=(float)tmpDub0[0]; curOutput[id++]=(float)tmpDub1[0];
            //ErrChk(Core.HtSine(startIdx, endIdx, inReal, out outBegIdx, out outNBElement, /*Sine*/ tmpDub0, /*LeadSine*/ tmpDub1)); curOutput[id++]=(float)tmpDub0[0]; curOutput[id++]=(float)tmpDub1[0];
            //ErrChk(Core.StochF(startIdx, endIdx, inHigh, inLow, inClose, optInFastK_Period, optInFastD_Period, optInFastD_MAType, out outBegIdx, out outNBElement,/*FastK*/tmpDub0,/*FastD*/tmpDub1)); curOutput[id++]=(float)tmpDub0[0]; curOutput[id++]=(float)tmpDub1[0];
            //ErrChk(Core.StochRsi(startIdx, endIdx, inReal, optInTimePeriod14, optInFastK_Period, optInFastD_Period, optInFastD_MAType, out outBegIdx, out outNBElement,/*FastK*/tmpDub0,/*FastD*/tmpDub1)); curOutput[id++]=(float)tmpDub0[0]; curOutput[id++]=(float)tmpDub1[0];
            //ErrChk(Core.MinMax(startIdx, endIdx, inReal, optInTimePeriod, out outBegIdx, out outNBElement, /*Min*/ tmpDub0, /*Max*/ tmpDub1)); curOutput[id++]=(float)tmpDub0[0]; curOutput[id++]=(float)tmpDub1[0];
            //ErrChk(Core.Stoch(startIdx, endIdx, inHigh, inLow, inClose, optInFastK_Period, optInSlowK_Period, optInSlowK_MAType, optInSlowD_Period, optInSlowD_MAType, out outBegIdx, out outNBElement,/*FastK*/tmpDub0,/*FastD*/tmpDub1)); curOutput[id++]=(float)tmpDub0[0]; curOutput[id++]=(float)tmpDub1[0];
            ErrChk(Core.Macd(startIdx, endIdx, inReal, optInFastPeriod, optInSlowPeriod, optInSignalPeriod, out outBegIdx, out outNBElement,/*MACD*/tmpDub0,/*MACDSig*/tmpDub1,/*MACDHist*/tmpDub2)); curOutput[id++]=(float)tmpDub0[0]; curOutput[id++]=(float)tmpDub1[0];  curOutput[id++]=(float)tmpDub2[0];
            //ErrChk(Core.MacdFix(startIdx, endIdx, inReal, optInSignalPeriod, out outBegIdx, out outNBElement, /*MACD*/ tmpDub0, /*MACDSignal*/ tmpDub1, /*MACDHist*/ tmpDub2)); curOutput[id++]=(float)tmpDub0[0]; curOutput[id++]=(float)tmpDub1[0]; curOutput[id++]=(float)tmpDub2[0];
            ErrChk(Core.Bbands(startIdx, endIdx, inReal, optInTimePeriodBBands, optInNbDevUp, optInNbDevDn, optInMAType, out outBegIdx, out outNBElement, tmpDub0, tmpDub1, tmpDub2)); curOutput[id++]=(float)tmpDub0[0]; curOutput[id++]=(float)tmpDub1[0]; curOutput[id++]=(float)tmpDub2[0];
            //ErrChk(Core.MacdExt(startIdx, endIdx, inReal, optInFastPeriod, optInFastMAType, optInSlowPeriod, optInSlowMAType, optInSignalPeriod, optInSignalMAType, out outBegIdx, out outNBElement,/*MACD*/tmpDub0,/*MACDSignal*/tmpDub1,/*MACDHist*/tmpDub2)); curOutput[id++]=(float)tmpDub0[0]; curOutput[id++]=(float)tmpDub1[0]; curOutput[id++]=(float)tmpDub2[0];

            }); // Parallel.For

            return outputs;
        }

        /// <summary>
        /// Adds the a brief Row and re-aligns the histories if we are getting close to hist_max_sz.
        /// </summary>
        public void AddBrfRow(float[] high, float[] low, float[] close, float[] vol)
        {
            // future: we copy the data twice (once building the arguments and another copying it into the hist_hi,hist_lo....)
            // lets check to see if our array is getting full. If we get close to hist_max_sz then we should re-align the data.
            if (histCt > (HIST_BUF_SZ - 100))
            {
                int srcIdx = histCt - hist_min_sz;
                for (int symbol = 0; symbol < symbolCt; symbol++)
                {
                    Array.Copy(hist_hi[symbol], srcIdx, hist_hi[symbol], 0, hist_min_sz);
                    Array.Copy(hist_lo[symbol], srcIdx, hist_lo[symbol], 0, hist_min_sz);
                    Array.Copy(hist_cl[symbol], srcIdx, hist_cl[symbol], 0, hist_min_sz);
                    Array.Copy(hist_vo[symbol], srcIdx, hist_vo[symbol], 0, hist_min_sz);
                }

                //since we moved everything left our history size is now smaller.
                histCt = hist_min_sz;
            }

            // now lets add the new brief; 
            for (int symbol = 0; symbol < symbolCt; symbol++)
            {
                hist_hi[symbol][histCt] = high[symbol];
                hist_lo[symbol][histCt] = low[symbol];
                hist_cl[symbol][histCt] = close[symbol];
                hist_vo[symbol][histCt] = vol[symbol];
            }
            histCt++;
        }


        private static void ErrChk(Core.RetCode retCode)
        {
            if (retCode != Core.RetCode.Success)
                Console.WriteLine("Error:" + retCode);
        }
    }
}
