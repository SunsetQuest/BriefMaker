// BriefMaker - converts market stream data to time-interval snapshots
// This projected is licensed under the terms of the MIT license.
// NO WARRANTY. THE SOFTWARE IS PROVIDED TO YOU “AS IS” AND “WITH ALL FAULTS.”
// ANY USE OF THE SOFTWARE IS ENTIRELY AT YOUR OWN RISK.
// Created by Ryan S. White in 2013; Last updated in 2016.

using System;

namespace BM
{
    public struct TinyTimeSeperated
    {
        //accuracy: 1 second
        //life: 178 years
        //time - ranges: 18.4 hours
        //1st 16-bits is days since 2000
        //2nd 16-bits is seconds since midnight


        //599266080000000000 ticks since 1900)
        //630822816000000000 ticks since 2000)
        //730119             days from 0 to 2000
        //10000000           SYSTEM_TICKS(DateTime.Ticks) in 1 second 
        //315360000000000    normal year
        //864000000000       ticks in 24hours

        uint val;

        public TinyTimeSeperated(int val)
        {
            this.val = (uint)val;
        }

        public TinyTimeSeperated(uint val)
        {
            this.val = val;
        }

        public TinyTimeSeperated(DateTime _datetime)
        {
            val = 0;
            SetUsingDataTime(_datetime);
        }
        public override string ToString()
        {
            return ((DateTime)this).ToString();
        }

        public void SetUsingDataTime(DateTime t)
        {   //Datetime -> IntTime
            int seconds;
            long ticksSince2000 = t.Ticks - 630822816000000000;
            int secondsSince2000 = (int)(ticksSince2000 / 10000000);
            uint days = (uint)System.Math.DivRem(secondsSince2000, 86400, out seconds);
            if (seconds > 65535)
                throw new Exception("DateTime conversion error: Hours go past 18:10pm.");

            val = (days << 16) + (uint)seconds;
        }

        public static implicit operator int(TinyTimeSeperated tt)
        {
            return ((int)tt.val);
        }

        public static implicit operator TinyTimeSeperated(int val)
        {
            return new TinyTimeSeperated(val);
        }

        public static implicit operator uint(TinyTimeSeperated tt)
        {
            return (uint)(tt.val);
        }

        public static implicit operator TinyTimeSeperated(uint val)
        {
            return new TinyTimeSeperated(val);
        }

        public static implicit operator DateTime(TinyTimeSeperated tt)
        {
            uint days = tt.val >> 16;
            uint seconds = tt.val & 0x0000FFFF;
            uint secondsSince2000 = days * 86400 + seconds;
            long ticksSince2000 = ((long)secondsSince2000 * 10000000);
            long ticks = ticksSince2000 + 630822816000000000;
            return new DateTime(ticks);
        }

        public static implicit operator TinyTimeSeperated(DateTime val)
        {
            return new TinyTimeSeperated(val);
        }
    }

    public struct TinyTime256th
    {
        // * weekends and nights are removed
        // * about 2 years and 3 months life
        // * 8 working hours

        const int START_HOUR = 6;  //6am (600)
        const int POST_HOURS = 10;//2pm (1400)
        const int year = 2008;
        const int dayOffset = 1; //offset from Monday (Tuesday = 1)
        const long TicksSinceStartingYear = 633347424000000000;

        const double RATIO = 39062.500;  //breaks DateTime.Ticks in 1/256 sec slices 
        const int ticksInMorningAndNight = (START_HOUR + POST_HOURS) * 921600;
        const int TicksInRecordingHours = (24 - START_HOUR - POST_HOURS) * 921600;
        const int TicksInPreHours = START_HOUR * 921600;

        uint val;

        //second  = 256                 10000000    
        //minute  = 15360               600000000
        //hour    = 921600              36000000000
        //day     = 7372800 
        //day(24h)= 22118400            864000000000
        //2 days  = 44236800
        //1 yr =1916928000-1924300800   315360000000000 

        //599266080000000000 ticks since 1900)
        //630822816000000000 ticks since 2000)
        //730119             days from 0 to 2000
        //10000000           SYSTEM_TICKS(DateTime.Ticks) in 1 second 
        //315360000000000    SYSTEM_TICKS in normal year
        //864000000000       SYSTEM_TICKS in 24hours

        public TinyTime256th(int val)
        {
            this.val = (uint)val;
        }

        public TinyTime256th(uint val)
        {
            this.val = val;
        }

        public TinyTime256th(DateTime _datetime)
        {
            val = 0;
            SetUsingDataTime(_datetime);
        }
        public override string ToString()
        {
            return ((DateTime)this).ToString();
        }

        public void SetUsingDataTime(DateTime t)
        {   //Datetime -> IntTime
            long val = (long)(((double)(t.Ticks - TicksSinceStartingYear)) / RATIO);

            //check for errors
            int hours = t.TimeOfDay.Hours;
            if ((hours < (START_HOUR)) || (hours >= (24 - POST_HOURS)))
                throw new Exception("DateTime conversion: Hours are out-of-range.");
            DayOfWeek dayOfWeek = t.DayOfWeek;
            if (dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday)
                throw new Exception("DateTime conversion: Weekend Detected.");
            if (val < 0)
                throw new Exception("DateTime conversion: Low date range.");

            //remove weekends
            long days = val / 22118400;
            long weekends = (days + (long)dayOffset) / 7;   // (n/7) ==> n*9363>>16
            val -= 44236800 * weekends;

            //remove evenings and mornings
            long days2 = days - (weekends << 1);
            days = val / 22118400;
            val -= ticksInMorningAndNight * days; //each night + morning
            val -= TicksInPreHours;//and finally the very first morning

            //check for more errors
            if (val > UInt32.MaxValue)
                throw new Exception("DateTime conversion: Date range overflow.");


            this.val = (uint)val;

        }

        public static implicit operator int(TinyTime256th tt)
        {
            return ((int)tt.val);
        }

        public static implicit operator TinyTime256th(int val)
        {
            return new TinyTime256th(val);
        }

        public static implicit operator uint(TinyTime256th tt)
        {
            return (uint)(tt.val);
        }

        public static implicit operator TinyTime256th(uint val)
        {
            return new TinyTime256th(val);
        }

        public static implicit operator DateTime(TinyTime256th tt)
        {
            long ticks = tt.val;

            //add evenings and mornings
            int days = (int)(tt.val / TicksInRecordingHours);
            ticks += ticksInMorningAndNight * (long)days; //each night + morning
            ticks += TicksInPreHours;//and finally the very first morning

            //add weekends
            int weekends = (days + dayOffset) / 5;   // n/5 ==> n*205>>10
            ticks += 44236800 * (long)weekends; //each weekend

            //long temp = (new DateTime(2008, 1, 1, 6, 0, 0, 4)).Ticks - TicksSinceStartingYear;
            return new DateTime(((long)(ticks * RATIO + 10000)) + TicksSinceStartingYear);
        }

        public static implicit operator TinyTime256th(DateTime val)
        {
            return new TinyTime256th(val);
        }
    }

    /// <summary>TinyTime6Sec holds data M-F 6am-2pm with a 6 second resolution.</summary>
    public struct TinyTime6Sec
    {
        //accuracy: 6 seconds    
        //6 second ticks; good for ~450 years
        /// <summary>(6); 6am or 600 ticks</summary>
        public const int START_HOUR = 6;
        /// <summary>(14); (2pm) (1400 ticks)</summary>
        public const int END_HOUR = 14;
        /// <summary>(6); The number of seconds in one tick</summary>
        public const int RESOLUTION = 6;
        /// <summary>(1); offset from Monday (Tue=1)(depends on StartingYear)</summary>
        public const int DAY_OF_WEEK_OFFSET = 4;
        /// <summary>(633979008000000000); ticks since 2010(aka DateTime(2010,1,1).Ticks)</summary>
        public const long SYS_TICKS_SINCE_START_YEAR = 633979008000000000; 
        /// <summary>(10000000)</summary>
        public const int SYS_TICKS_PER_SEC = 10000000;
        /// <summary>(10); 60/RESOLUTION = 60/6 = 10</summary>
        public const int TICKS_IN_MINUTE = 60 / RESOLUTION;
        /// <summary>(10); 24-14 = 10</summary>
        public const int POST_HOURS = 24 - END_HOUR;
        /// <summary>14 - 6 = 8</summary>
        public const int ACTIVE_HOURS = (END_HOUR - START_HOUR);
        /// <summary>(600); TICKS_IN_MINUTE*60 = 10*60 = 600</summary>
        public const int TICKS_IN_HOUR = TICKS_IN_MINUTE * 60;
        /// <summary>(14400); TICKS_IN_HOUR*24 = 600*24 = 14400</summary>
        public const int TICKS_IN_FULL_24HOURS = TICKS_IN_HOUR * 24;
        /// <summary>The number of SYSTEM ticks(datetime.ticks) in 6 seconds.</summary>
        public const int SYS_TICKS_PER_UNIT = SYS_TICKS_PER_SEC * RESOLUTION;
        /// <summary>The maximum SYSTEM Tick value that TinyTime6Sec would fail. 
        /// (SYS_TICKS_SINCE_START_YEAR + (UInt32.MaxValue * (long)SYS_TICKS_PER_UNIT))</summary>
        public const long SYS_TICKS_OVERFLOW_AMOUNT = SYS_TICKS_SINCE_START_YEAR + (UInt32.MaxValue * (long)SYS_TICKS_PER_UNIT);

        /// <summary>(9600); (START_HOUR + POST_HOURS) * TICKS_IN_HOUR; (6+10)*600=9600 </summary>
        public const int TICKS_IN_MORNING_AND_EVENING = (START_HOUR + POST_HOURS) * TICKS_IN_HOUR;
        /// <summary>600*(8)=4800</summary>
        public const int TICKS_IN_ACTIVE_HOURS = ACTIVE_HOURS * TICKS_IN_HOUR; // 8 * 600
                                                                               /// <summary>START_HOUR(6) * TICKS_IN_HOUR(600)</summary>
        public const int TICKS_IN_PRE_HOURS = START_HOUR * TICKS_IN_HOUR;

        /// <summary>
        ///  This is the date/time. Each increment is 6 seconds.
        /// </summary>
        uint val;

        //599266080000000000(ticksSince1900) //630822816000000000 ticks since 2000)
        //730119(days from 0 to 2000)        //10000000 (SYSTEM_TICKS(DateTime.Ticks)/sec)
        //315360000000000(normal year)       //864000000000(SYSTEM_TICKS ticks in 24hours)

        public TinyTime6Sec(int val)
        {
            this.val = (uint)val;
        }

        public TinyTime6Sec(uint val)
        {
            this.val = val;
        }

        public TinyTime6Sec(long ticks)
        {
            val = 0;

            //if out of range then round down
            DateTime newTime = new DateTime(ticks);
            if ((newTime.TimeOfDay > new TimeSpan(END_HOUR, 0, 0, 6, 999)))
                newTime = newTime.Date.AddHours(END_HOUR - 1).AddMinutes(59).AddSeconds(54);
            else if (newTime.Hour < START_HOUR)
                newTime = newTime.Date.AddDays(-1).AddHours(END_HOUR - 1).AddMinutes(59).AddSeconds(54);

            if (newTime.DayOfWeek == DayOfWeek.Saturday)
                newTime = newTime.Date.AddDays(-1).AddHours(END_HOUR - 1).AddMinutes(59).AddSeconds(54);
            else if (newTime.DayOfWeek == DayOfWeek.Sunday)
                newTime = newTime.Date.AddDays(-2).AddHours(END_HOUR - 1).AddMinutes(59).AddSeconds(54);


            SetUsingDataTime(newTime);
        }

        public TinyTime6Sec(DateTime _datetime)
        {
            val = 0;
            SetUsingDataTime(_datetime);
        }

        public override string ToString()
        {
            return ((DateTime)this).ToString();
        }

        public void SetUsingDataTime(DateTime t)
        {
            IsValidTime(t, true);

            //Datetime -> IntTime
            long val = ((t.Ticks - SYS_TICKS_SINCE_START_YEAR) / SYS_TICKS_PER_UNIT);

            //remove weekends
            long days = val / TICKS_IN_FULL_24HOURS;
            long weekends = (days + (long)DAY_OF_WEEK_OFFSET) / 7;   // (n/7) ==> n*9363>>16
            val -= (TICKS_IN_FULL_24HOURS * 2) * weekends;

            //remove evenings and mornings
            days = days - (weekends << 1);
            //days = val / (TICKS_IN_FULL_24HOURS * 2); //needed because weekends are now removed 
            val -= TICKS_IN_MORNING_AND_EVENING * days; //each night + morning
            val -= TICKS_IN_PRE_HOURS;//and finally the very first morning

            this.val = (uint)val;
        }

        public void SetUsingTicks(long ticks)
        {
            SetUsingDataTime(new DateTime(ticks));
        }

        public long Ticks()
        {
            DateTime dt = new TinyTime6Sec(val);
            return dt.Ticks;
        }

        public static implicit operator int(TinyTime6Sec tt)
        {
            return ((int)tt.val);
        }

        public static implicit operator TinyTime6Sec(int val)
        {
            return new TinyTime6Sec(val);
        }

        public static implicit operator TinyTime6Sec(long ticks)
        {
            return new TinyTime6Sec(ticks);
        }

        public static implicit operator uint(TinyTime6Sec tt)
        {
            return (uint)(tt.val);
        }

        public static implicit operator TinyTime6Sec(uint val)
        {
            return new TinyTime6Sec(val);
        }

        public static implicit operator DateTime(TinyTime6Sec tt)
        {
            //add evenings and mornings
            long ticks = tt.val;
            int days = (int)(ticks / TICKS_IN_ACTIVE_HOURS);
            ticks += TICKS_IN_MORNING_AND_EVENING * (long)days; //each night + morning
            ticks += TICKS_IN_PRE_HOURS;//and finally the very first morning

            //add weekends
            int weekends = (days + DAY_OF_WEEK_OFFSET) / 5;   // n/5 ==> n*205>>10
            ticks += 28800 * (long)weekends;         //each weekend

            //long temp = (new DateTime(2008, 1, 1, 6, 0, 0, 4)).Ticks - TicksSinceStartingYear;
            return new DateTime(((long)(ticks * SYS_TICKS_PER_UNIT)) + SYS_TICKS_SINCE_START_YEAR); //removed  "+ 10000"
        }


        public static implicit operator TinyTime6Sec(DateTime val)
        {
            return new TinyTime6Sec(val);
        }

        //public static DateTime AsDateTime(long ticks)
        //{
        //    //add evenings and mornings
        //    int days = (int)(ticks / TICKS_IN_ACTIVE_HOURS);
        //    ticks += TICKS_IN_MORNING_AND_EVENING * (long)days; //each night + morning
        //    ticks += TICKS_IN_PRE_HOURS;//and finally the very first morning


        //    //add weekends
        //    int weekends = (days + DAY_OF_WEEK_OFFSET) / 5;   // n/5 ==> n*205>>10
        //    ticks += 28800 * (long)weekends;         //each weekend

        //    return new DateTime(((long)(ticks * SYS_TICKS_PER_UNIT + 10000)) + SYS_TICKS_SINCE_START_YEAR);
        //}

        public static bool IsValidTime(DateTime t, bool throwError)
        {
            bool valid = true;
            int hours = t.TimeOfDay.Hours;
            if (hours < START_HOUR)
            {
                valid = false;
                if (throwError)
                    throw new Exception("TinyTime: Time cannot be before 6:00 - " + t.ToShortTimeString());
            }
            if ((hours >= END_HOUR) && (t.TimeOfDay > new TimeSpan(END_HOUR, 0, 0, 6, 999)))
            {
                valid = false;
                if (throwError)
                    throw new Exception("TinyTime: Time cannot be after 14:00:06 - " + t.ToShortTimeString());
            }
            DayOfWeek dayOfWeek = t.DayOfWeek;
            if (dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday)
            {
                valid = false;
                if (throwError)
                    throw new Exception("TinyTime: Weekends not allowed - " + t.ToLongDateString());
            }
            long ticks = t.Ticks;
            if (ticks < TinyTime6Sec.SYS_TICKS_SINCE_START_YEAR)
            {
                valid = false;
                if (throwError)
                    throw new Exception("TinyTime: Low date range - " + t.ToLongDateString());
            }
            if (ticks >= TinyTime6Sec.SYS_TICKS_OVERFLOW_AMOUNT)
            {
                valid = false;
                if (throwError)
                    throw new Exception("TinyTime: Date overflow - " + t.ToLongDateString());
            }

            return valid;
        }

        /// <summary>Returns true if the datetime can be stored in the current TinyTime6Sec.</summary>
        public static bool IsValid(DateTime t)
        {
            int hours = t.TimeOfDay.Hours;
            DayOfWeek dayOfWeek = t.DayOfWeek;
            long ticks = t.Ticks;

            return !((hours < START_HOUR)
                || ((hours >= END_HOUR) && (t.TimeOfDay > new TimeSpan(END_HOUR, 0, 0, 6, 999)))
                || (dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday)
                || (ticks < TinyTime6Sec.SYS_TICKS_SINCE_START_YEAR)
                || (ticks >= TinyTime6Sec.SYS_TICKS_OVERFLOW_AMOUNT));
        }

        /// <summary>Throws an exception if time is below or above what TinyTime6Sec can handle.</summary>
        public static void CheckDateRange(DateTime t)
        {
            long ticks = t.Ticks;
            if (ticks < TinyTime6Sec.SYS_TICKS_SINCE_START_YEAR)
                throw new Exception("TinyTime: Low date range - " + t.ToLongDateString());
            if (ticks >= TinyTime6Sec.SYS_TICKS_OVERFLOW_AMOUNT)
                throw new Exception("TinyTime: Date overflow - " + t.ToLongDateString());
        }

        public static TinyTime6Sec GetNextValidDateTime(TinyTime6Sec tt)
        {
            tt++;
            return tt;
        }

        //public TinyTime6Sec GetNextValidDateTime()
        //{
        //    val.
        //    return tt;
        //}

        public static DateTime GetNextValidDateTime(DateTime t)
        {
            TinyTime6Sec tt = new TinyTime6Sec(t.Ticks);
            tt++;
            return tt;
        }

        /// <summary>Gets the number of 6 second ticks since the PreBeginBufferTime of the day.</summary>
        public uint TicksToday { get { return val % TICKS_IN_ACTIVE_HOURS; } }
    }

    public struct TinyTime1Sec
    {
        //accuracy: 1 seconds 
        //life: 136.1 years
        //time coverage: 24 hours (no breaks)
        //date coverage: all 365 days (no breaks)

        //599266080000000000 ticks since 1900)
        //630822816000000000 ticks since 2000)
        //730119             days from 0 to 2000
        //10000000           SYSTEM_TICKS(DateTime.Ticks) in 1 second 
        //315360000000000    normal year
        //864000000000       ticks in 24hours

        uint val; //secondsSince2000

        public TinyTime1Sec(int val)
        {
            this.val = (uint)val;
        }

        public TinyTime1Sec(uint val)
        {
            this.val = val;
        }

        public TinyTime1Sec(DateTime _datetime)
        {
            val = 0;
            SetUsingDataTime(_datetime);
        }
        public override string ToString()
        {
            return ((DateTime)this).ToString();
        }

        public void SetUsingDataTime(DateTime t)
        {   //Datetime -> IntTime
            long ticksSince2000 = t.Ticks - 630822816000000000;
            val = (uint)(ticksSince2000 / 10000000);
        }

        public static implicit operator int(TinyTime1Sec tt)
        {
            return ((int)tt.val);
        }

        public static implicit operator TinyTime1Sec(int val)
        {
            return new TinyTime1Sec(val);
        }

        public static implicit operator uint(TinyTime1Sec tt)
        {
            return (uint)(tt.val);
        }

        public static implicit operator TinyTime1Sec(uint val)
        {
            return new TinyTime1Sec(val);
        }

        public static implicit operator DateTime(TinyTime1Sec tt)
        {
            long ticksSince2000 = ((long)tt.val * 10000000);
            long ticks = ticksSince2000 + 630822816000000000;
            return new DateTime(ticks);
        }

        public static implicit operator TinyTime1Sec(DateTime val)
        {
            return new TinyTime1Sec(val);
        }

    }
}
