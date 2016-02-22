// BriefMaker - converts market stream data to time-interval snapshots
// This projected is licensed under the terms of the MIT license.
// NO WARRANTY. THE SOFTWARE IS PROVIDED TO YOU “AS IS” AND “WITH ALL FAULTS.”
// ANY USE OF THE SOFTWARE IS ENTIRELY AT YOUR OWN RISK.
// Copyright (c) 2013, 2014, 2015, 2016 Ryan S. White

using System;
using System.Collections.Generic;

namespace BM
{
    /// <summary>Contains static methods for running common statistical functions over an array of data.</summary>
    public static class Stat
    {
        /// <summary>Finds the most frequent value in a array. Side Effect: array is sorted. </summary>
        /// <param name="values">A non-sorted integer Array</param>
        /// <param name="ocurances">Returns the number of occurrences</param>
        /// <returns>The most frequent value.</returns>
        public static T Mode<T>(T[] values, out int ocurances)
        {

            Array.Sort<T>(values);

            int length = values.Length;
            int max_ct = 1;
            T max_value = default(T);


            int current_ct = 1;
            T current_value = values[0];
            for (int i = 1; i < length; i++)
            {
                if (current_value.Equals(values[i]))
                    current_ct++;
                else
                {
                    if (current_ct > max_ct)
                    {
                        max_ct = current_ct;
                        max_value = current_value;
                    }
                    current_ct = 1;
                    current_value = values[i];
                }
            }

            if (current_ct > max_ct)
            {
                max_ct = current_ct;
                max_value = current_value;
            }

            ocurances = max_ct;
            return max_value;
        }

        /// <summary>
        /// Calculates the Mean of a sorted integer array. For odd numbered
        /// arrays this is the value of the center item. Or, for even numbered 
        /// arrays, the average of the middle two items. Mode of 1,2,3,7,8 is 3.
        /// CPUTime=12 ticks(for odd number) 15 ticks(for even number)
        /// </summary>
        /// <param name="values">A sorted integer Array</param>
        /// <returns>The Mean(or Average)</returns>
        public static int MedianOfSortedArray(int[] values)
        {
            int length = values.Length;
            bool odd = ((length & 0x01) == 1);
            int half = length >> 1; //div by 2
            if (odd) //if odd number
                return ((values[half]));
            else
                return ((values[half] + values[half - 1]) >> 1);
        }

        /// <summary>Calculates the Mean(or average) of an array of doubles. </summary>
        /// <param name="values">A double Array</param>
        /// <returns>The Mean(or Average)</returns>
        public static double Mean(double[] data)
        {
            int len = data.Length;

            if (len == 0)
                throw new Exception("No data");

            double sum = 0;

            for (int i = 0; i < len; i++)
                sum += data[i];

            return sum / len;
        }

        /// <summary>Calculates the Mean(or average) of an array. </summary>
        /// <param name="values">A integer Array</param>
        /// <returns>The Mean(or Average)</returns>
        public static int Mean(int[] data)
        {
            int len = data.Length;

            //if (len == 0) throw new Exception("No data");

            int sum = 0;

            for (int i = 0; i < data.Length; i++)
                sum += data[i];

            return sum / len;
        }

        /// <summary>Calculates the Mean(or average) of an array. </summary>
        /// <param name="values">A integer Array</param>
        /// <returns>The Mean(or Average)</returns>
        public static double Mean_Dbl(int[] data)
        {
            int len = data.Length;

            //if (len == 0) throw new Exception("No data");

            int sum = 0;

            for (int i = 0; i < data.Length; i++)
                sum += data[i];

            return (double)sum / len;
        }

        /// <summary>Get variance - doubles </summary>
        public static double Variance_Dbl(double[] data)
        {
            int len = data.Length;

            // Get average
            double mean = Mean(data);

            double sum = 0;

            for (int i = 0; i < data.Length; i++)
                sum += System.Math.Pow((data[i] - mean), 2);

            return sum / (len - 1);
        }

        /// <summary>Get variance - The average of the square of the distance of each data point from the mean  </summary>
        public static int Variance_Int(int[] data)
        {
            int len = data.Length;

            // Get average
            int mean = Mean(data);

            int sum = 0;

            for (int i = 0; i < data.Length; i++)
            {
                int temp = (data[i] - mean);
                sum += temp * temp;
            }

            return sum / len;
        }

        /// <summary>Get variance - The average of the square of the distance of each data point from the mean  </summary>
        public static double Variance_Dbl(int[] data)
        {
            int len = data.Length;

            // Get average
            double mean = Mean_Dbl(data);

            double sum = 0;

            for (int i = 0; i < data.Length; i++)
            {
                double temp = (data[i] - mean);
                sum += temp * temp;
            }

            return sum / len;
        }

        /// <summary>Get standard deviation </summary>
        public static double StdDev_Dbl(double[] data)
        {
            return System.Math.Sqrt(Variance_Dbl(data));
        }

        /// <summary>Get standard deviation </summary>
        public static int StdDev_Int(int[] data)
        {
            return (int)System.Math.Sqrt(Variance_Dbl(data));
        }

        /// <summary>Get standard deviation </summary>
        public static double StdDev_Dbl(int[] data)
        {
            return System.Math.Sqrt(Variance_Dbl(data));
        }

        /// <summary>
        /// Skewness_Int is a measure of symmetry, or more precisely, the lack 
        /// of symmetry. A distribution, or data set, is symmetric if it 
        /// looks the same to the left and right of the center point. 
        /// Skewness_Int of symmetric data is zero 
        /// skewness = (Sum((Xi-mean)^3))/((N-1)*SD^3) 
        /// </summary>
        public static int Skewness_Int(int[] array)
        {
            int length = array.Length;
            int std_dev = StdDev_Int(array);
            double mean = Mean_Dbl(array);
            double top_sum = 0;
            int bot_sum = 0;

            for (int i = 0; i < length; i++)
            {
                double temp = (array[i] - mean);
                top_sum += (temp * temp * temp);
            }

            bot_sum = (length - 1) * (std_dev * std_dev * std_dev);

            return (int)(top_sum / bot_sum);
        }

        /// <summary>
        /// Kurtosis_Dbl is a measure of whether the data are peaked or flat relative
        /// to a normal distribution. That is, data sets with high kurtosis tend 
        /// to have a distinct peak near the mean, decline rather rapidly, and 
        /// have heavy tails. Data sets with low kurtosis tend to have a flat top
        /// near the mean rather than a sharp peak. A uniform distribution would
        /// be the extreme case. 
        /// The standard normal distribution has a kurtosis of zero. Positive 
        /// kurtosis indicates a "peaked" distribution and negative kurtosis 
        /// indicates a "flat" distribution. 
        /// kurtosis = (Sum((Xi-mean)^4))/((N-1)*SD^4) -3  
        /// </summary>
        public static double Kurtosis_Dbl(int[] array)
        {
            int length = array.Length;
            double std_dev = StdDev_Dbl(array);//StdDev_Int(array);
            double var = Variance_Dbl(array);//StdDev_Int(array);
            double mean = Mean_Dbl(array);
            double top_sum = 0;
            double bot_sum = 0;

            for (int i = 0; i < length; i++)
            {
                double temp = (array[i] - mean);
                top_sum += (temp * temp * temp * temp);
            }

            bot_sum = ((length - 1) * (var * var));

            return (top_sum / bot_sum) - 3;
        }

        public static void AllStatistics(double[] array, out double median, out double mean, out double stdDev, out double skewness, out double kurtosis)
        {
            int n = array.Length;

            double sum = 0.0;
            stdDev = 0.0;
            double variance = 0.0;
            skewness = 0.0;
            kurtosis = 0.0;
            median = 0.0;
            double deviation = 0.0;
            int i, mid = 0;

            for (i = 0; i < n; i++)
                sum += array[i];
            mean = sum / n;
            for (i = 0; i < n; i++)
            {
                deviation = (double)array[i] - mean;
                //average_deviation += Math.Abs(deviation);
                variance += System.Math.Pow(deviation, 2);
                skewness += System.Math.Pow(deviation, 3);
                kurtosis += System.Math.Pow(deviation, 4);
            }
            //average_deviation /= n;
            variance /= (n - 1);
            stdDev = Math.Sqrt(variance);
            if (variance != 0.0)
            {
                skewness /= ((n - 1) * variance * stdDev);
                kurtosis = kurtosis / ((n - 1) * variance * variance) - 3.0;
            }

            mid = n / 2;
            median = (n % 2 != 0) ?
                (double)array[mid] :
                ((double)array[mid] + (double)array[mid - 1]) / 2;
        }

        public struct AllStatisticsShortResults
        {
            public ushort median;
            public ushort mean;
            public ushort stdDev;
            public short skewness;
            public ushort kurtosis;
        }
        /// <summary>Get the Median, Mean, Std Dev, skewness, kurtosis of an array of positive ints. </summary>
        public static AllStatisticsShortResults AllStatistics(ushort[] array)
        {
            int nInt = array.Length;
            double n = (double)nInt;
            double nMinus1 = n - 1;
            double nMinus2 = n - 2;
            double sumDbl = 0;
            double stdDevDbl;
            double varianceDbl = 0;
            double skewnessDbl = 0;
            double kurtosisDbl = 0; //= {[n(n+1) / (n -1)(n - 2)(n-3)] sum[(x_i - mean)^4] / std^4} - [3(n-1)^2 / (n-2)(n-3)] 

            for (int i = 0; i < nInt; i++)
                sumDbl += (double)array[i];
            double meanDbl = sumDbl / n;

            double deviationDbl = 0.0;
            for (int i = 0; i < nInt; i++)
            {
                deviationDbl = (double)array[i] - meanDbl;
                double tempDevPwr2 = deviationDbl * deviationDbl;
                double tempDevPwr3 = tempDevPwr2 * deviationDbl;
                double tempDevPwr4 = tempDevPwr3 * deviationDbl;
                varianceDbl += tempDevPwr2;
                skewnessDbl += tempDevPwr3;
                kurtosisDbl += tempDevPwr4;
            }
            double temp = varianceDbl;// / n;
            varianceDbl /= nMinus1;
            stdDevDbl = System.Math.Sqrt(varianceDbl);
            if (varianceDbl != 0)
            {
                skewnessDbl = (n * skewnessDbl) / (nMinus1 * nMinus2 * stdDevDbl * varianceDbl);
                kurtosisDbl = ((n * n - 1) / (nMinus2 * (n - 3))) *
                  (((kurtosisDbl * n) / (temp * temp)) - 3 + (6 / (n + 1)));
            }

            AllStatisticsShortResults results;
            int mid = nInt >> 1; //divide by 2
            results.median = ((nInt & 1) == 1) ? array[mid] : (ushort)((array[mid] + array[mid - 1]) / 2.0F);
            results.mean = (ushort)meanDbl;
            results.stdDev = (ushort)stdDevDbl;
            results.skewness = (short)skewnessDbl;
            results.kurtosis = (ushort)kurtosisDbl;
            return results;
        }

        public struct AllStatisticsFloatResults
        {
            public float median;
            public float mean;
            public float stdDev;
            public float skewness;
            public float kurtosis;
            public float mode;
            public float modeCount;
        }

        public struct SimpleStatisticsFloatResults
        {
            public float median;
            public float mean;
            public float mode;
            public float modeCount;
        }

        /// <summary>Get the Median, Mean, Std Dev, skewness, kurtosis of an array of positive floats. </summary>
        public static SimpleStatisticsFloatResults SimpleStatistics(List<float> prices, float defaultVal)
        {
            SimpleStatisticsFloatResults results;

            if (prices.Count == 0)
            {

                results.median = defaultVal;
                results.mean = defaultVal;
                results.mode = defaultVal;
                results.modeCount = 0;
            }
            else if (prices.Count == 1)
            {
                results.median = prices[0];
                results.mean = prices[0];
                results.mode = prices[0];
                results.modeCount = 1;
            }
            else if (prices.Count == 2)
            {
                float avg = (prices[0] + prices[1]) / (float)2.0;
                results.median = avg;
                results.mean = avg;
                if (prices[0] == prices[1])
                {
                    results.mode = prices[0];
                    results.modeCount = 2;
                }
                else
                {
                    results.mode = prices[0];
                    results.modeCount = 1;
                }
            }
            else //3 or more
            {
                prices.Sort();
                int max_ct = 1;
                float max_value = prices[0];

                int current_ct = 1;
                float current_value = prices[0];
                for (int i = 1; i < prices.Count; i++)
                {
                    if (current_value.Equals(prices[i]))
                        current_ct++;
                    else
                    {
                        if (current_ct > max_ct)
                        {
                            max_ct = current_ct;
                            max_value = current_value;
                        }
                        current_ct = 1;
                        current_value = prices[i];
                    }
                }
                if (current_ct > max_ct)
                {
                    max_ct = current_ct;
                    max_value = current_value;
                }
                results.modeCount = max_ct;
                results.mode = max_value;

                // Now lets calculate the reset
                float sumDbl = 0;
                for (int i = 0; i < prices.Count; i++)
                    sumDbl += prices[i];
                results.mean = sumDbl / (float)prices.Count;

                int mid = prices.Count / 2;
                results.median = ((prices.Count & 1) == 1) ? prices[mid] : (float)((prices[mid] + prices[mid - 1]) / 2.0F);

                //if modeCount is 0 or 1 then set mode to median instead of just first value.
                if (results.modeCount <= 1)
                    results.mode = results.median;
            }
            return results;
        }


        /// <summary>Get the Median, Mean, Std Dev, skewness, kurtosis of an array of positive floats. </summary>
        public static AllStatisticsFloatResults AllStatistics(float[] array, int nInt, float defaultVal)
        {
            AllStatisticsFloatResults results;
            double varianceDbl = 0;
            double skewnessDbl = 0;
            double meanDbl = 0;
            double deviationDbl = 0;

            switch (nInt)
            {
                case 0:
                    results.median = defaultVal;
                    results.mean = defaultVal;
                    results.stdDev = 0;
                    results.skewness = 0;
                    results.kurtosis = 0;
                    results.mode = defaultVal;
                    results.modeCount = 0;
                    return results;

                case 1:
                    results.median = array[0];
                    results.mean = array[0];
                    results.stdDev = 0;
                    results.skewness = 0;
                    results.kurtosis = 0;
                    results.mode = array[0];
                    results.modeCount = 1;
                    return results;

                case 2:
                    float avg = (array[0] + array[1]) / (float)2.0;
                    results.median = avg;
                    results.mean = avg;
                    results.stdDev = 0;
                    results.skewness = 0;
                    results.kurtosis = 0;
                    if (array[0] == array[1])
                    {
                        results.mode = array[0];
                        results.modeCount = 2;
                    }
                    else
                    {
                        results.mode = array[0];
                        results.modeCount = 1;
                    }
                    return results;
                case 3:
                    //calc mode and mode count
                    Array.Sort<float>(array);
                    results.modeCount = 2;
                    results.mode = array[0];
                    if (array[0] == array[1])
                        if (array[0] == array[2])
                            results.modeCount = 3;
                        else
                        {
                            results.mode = array[1];
                            if (array[1] != array[2])
                                results.modeCount = 1;
                        }

                    //Now lets calculate the reset
                    meanDbl = array[0] + array[1] + array[2] / 3.0;

                    for (int i = 0; i < 3; i++)
                    {
                        deviationDbl = (double)array[i] - meanDbl;
                        double tempDevPwr2 = deviationDbl * deviationDbl;
                        double tempDevPwr3 = tempDevPwr2 * deviationDbl;
                        varianceDbl += tempDevPwr2;
                        skewnessDbl += tempDevPwr3;
                    }
                    varianceDbl /= 2;
                    results.stdDev = (float)System.Math.Sqrt(varianceDbl);
                    if (varianceDbl != 0)
                        skewnessDbl = 1.5 * (skewnessDbl) / (results.stdDev * varianceDbl);

                    results.median = array[1];
                    results.mean = (float)meanDbl;
                    results.skewness = (float)skewnessDbl;
                    results.kurtosis = 0;
                    return results;
            }

            //else is more then 3
            //calc mode
            Array.Sort<float>(array);
            int max_ct = 1;
            float max_value = array[0];

            int current_ct = 1;
            float current_value = array[0];
            for (int i = 1; i < nInt; i++)
            {
                if (current_value.Equals(array[i]))
                    current_ct++;
                else
                {
                    if (current_ct > max_ct)
                    {
                        max_ct = current_ct;
                        max_value = current_value;
                    }
                    current_ct = 1;
                    current_value = array[i];
                }
            }
            if (current_ct > max_ct)
            {
                max_ct = current_ct;
                max_value = current_value;
            }
            results.modeCount = max_ct;
            results.mode = max_value;

            //Now lets calculate the reset
            double n = (double)nInt;
            double nMinus1 = n - 1;
            double nMinus2 = n - 2;
            double sumDbl = 0;
            double stdDevDbl;
            double kurtosisDbl = 0;//= {[n(n+1) / (n -1)(n - 2)(n-3)] sum[(x_i - mean)^4] / std^4} - [3(n-1)^2 / (n-2)(n-3)] 

            for (int i = 0; i < nInt; i++)
                sumDbl += (double)array[i];
            meanDbl = sumDbl / n;
            for (int i = 0; i < nInt; i++)
            {
                deviationDbl = (double)array[i] - meanDbl;
                double tempDevPwr2 = deviationDbl * deviationDbl;
                double tempDevPwr3 = tempDevPwr2 * deviationDbl;
                double tempDevPwr4 = tempDevPwr3 * deviationDbl;
                varianceDbl += tempDevPwr2;
                skewnessDbl += tempDevPwr3;
                kurtosisDbl += tempDevPwr4;
            }
            double temp = varianceDbl;// / n;
            varianceDbl /= nMinus1;
            stdDevDbl = System.Math.Sqrt(varianceDbl);
            if (varianceDbl != 0)
            {
                skewnessDbl = (n * skewnessDbl) / (nMinus1 * nMinus2 * stdDevDbl * varianceDbl);
                kurtosisDbl = ((n * n - 1) / (nMinus2 * (n - 3))) *
                  (((kurtosisDbl * n) / (temp * temp)) - 3 + (6 / (n + 1)));
            }

            int mid = nInt >> 1; //divide by 2
            results.median = ((nInt & 1) == 1) ? array[mid] : (float)((array[mid] + array[mid - 1]) / 2.0F);
            results.mean = (float)meanDbl;
            results.stdDev = (float)stdDevDbl;
            results.skewness = (float)skewnessDbl;
            results.kurtosis = (float)kurtosisDbl;
            return results;
        }

        /// <summary>Get the Median, Mean, Std Deviation, skewness, kurtosis of an array of floats. </summary>
        public static void AllStatistics(float[] array, out float median, out float mean, out float stdDev, out float skewness, out float kurtosis)
        {
            //what about 0,1,2,or 3 items?

            int nInt = array.Length;
            double n = (double)nInt;
            double nMinus1 = n - 1;
            double nMinus2 = n - 2;
            double sumDbl = 0;
            double stdDevDbl;
            double varianceDbl = 0;
            double skewnessDbl = 0;
            double kurtosisDbl = 0; //= {[n(n+1) / (n -1)(n - 2)(n-3)] sum[(x_i - mean)^4] / std^4} - [3(n-1)^2 / (n-2)(n-3)] 

            for (int i = 0; i < nInt; i++)
                sumDbl += (double)array[i];
            double meanDbl = sumDbl / n;

            double deviationDbl = 0.0;
            for (int i = 0; i < nInt; i++)
            {
                deviationDbl = (double)array[i] - meanDbl;
                double tempDevPwr2 = deviationDbl * deviationDbl;
                double tempDevPwr3 = tempDevPwr2 * deviationDbl;
                double tempDevPwr4 = tempDevPwr3 * deviationDbl;
                varianceDbl += tempDevPwr2;
                skewnessDbl += tempDevPwr3;
                kurtosisDbl += tempDevPwr4;
            }
            double temp = varianceDbl;// / n;
            varianceDbl /= nMinus1;
            stdDevDbl = System.Math.Sqrt(varianceDbl);
            if (varianceDbl != 0)
            {
                skewnessDbl = (n * skewnessDbl) / (nMinus1 * nMinus2 * stdDevDbl * varianceDbl);
                kurtosisDbl = ((n * n - 1) / (nMinus2 * (n - 3))) *
                  (((kurtosisDbl * n) / (temp * temp)) - 3 + (6 / (n + 1)));
            }

            int mid = nInt >> 1; //divide by 2
            median = ((nInt & 1) == 1) ? array[mid] : (array[mid] + array[mid - 1]) / 2.0F;

            mean = (float)meanDbl;
            stdDev = (float)stdDevDbl;
            skewness = (float)skewnessDbl;
            kurtosis = (float)kurtosisDbl;
        }

        /// <summary>Get correlation </summary>
        public static void GetCorrelation(double[] x, double[] y, ref double covXY, ref double pearson)
        {
            if (x.Length != y.Length)
                throw new Exception("Length of sources is different");

            double avgX = Mean(x);
            double stdevX = StdDev_Dbl(x);
            double avgY = Mean(y);
            double stdevY = StdDev_Dbl(y);

            int len = x.Length;

            for (int i = 0; i < len; i++)
                covXY += (x[i] - avgX) * (y[i] - avgY);

            covXY /= len;

            pearson = covXY / (stdevX * stdevY);
        }
    }
}
