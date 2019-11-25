using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Dexmo.Unity;
using System;
using System.IO;
using System.Text;

namespace Dexmo.Unity
{
	/// <summary>
    /// Handles reading, saving and learning of regression coefficients for predicting grasp from sensors
    /// </summary>
    public class GraspPredictManager : MonoBehaviour
    {
        /// <summary>
        /// File to read regression coefficients from
        /// </summary>
        private static string toflearndataFilePath = @"C:\Users\dexta\test_sdk\Assets\TofSensorUnity\toflearndata.dat";

        /// <summary>
        /// Stores MAX regression coefficients as array of 5 fingers, 2 segmented regressions, polynomialorder+1 coeffs
        /// </summary>
        public static double[][][] GraspTofCoeffsMax = new double[5][][];

        /// <summary>
        /// Stores MIN regression coefficients as array of 5 fingers, 2 segmented regressions, polynomialorder+1 coeffs
        /// </summary>
        public static double[][][] GraspTofCoeffsMin = new double[5][][];

        /// <summary>
        /// Array of grasp learners
        /// </summary>        
        private static GraspLearner[] myGraspLearners = new GraspLearner[5];

        /// <summary>
        /// Convert generic array to string array
        /// </summary>
        public static string[] ArrToStr<T> (T[] _arr)
        {
            return Array.ConvertAll(_arr, item => item.ToString());
        }

        /// <summary>
        /// Returns root of 1st order polynomial, coefficients in increasing order
        /// </summary>
        private static double _linearRoot(double[] _coeffs)
        {
            return -1d * (_coeffs[0]/_coeffs[1]);
        }

        /// <summary>
        /// Returns closest root of 2nd order polynomial to _toWhat, , coefficients in increasing order
        /// </summary>
        private static double _closestQuadraticRoot(double[] _coeffs, double _toWhat)
        {
            double _discriminant = Math.Sqrt(_coeffs[1]*_coeffs[1] - 4*_coeffs[2]*_coeffs[0]);
            double _root0 = (-0.5d * (_coeffs[1] + _discriminant) / _coeffs[2]); // -+ = - = left root
            double _root1 = (-0.5d * (_coeffs[1] - _discriminant) / _coeffs[2]); // -- = + = right root

            return (Math.Abs(_root0 - _toWhat) < Math.Abs(_root1 - _toWhat)) ? _root0 : _root1;
        }

        /// <summary>
        /// Returns difference of two vectors
        /// </summary>
        private static double[] _deltaVector(double[] _vec1, double[] _vec2)
        {
            double[] _deltas = new double[_vec1.Length];
            for (int i=0; i < _vec1.Length; i++) 
            {
                _deltas[i] = _vec1[i]-_vec2[i];
            }
            return _deltas;
        }

        /// <summary>
        /// Scales vector in place by _scale
        /// </summary>
        private static void _scaleVectorInPlace(ref double[] _vec, double _scale)
        {
            for (int i=0; i<_vec.Length; i++)
            {
                _vec[i] *= _scale;
            }
        }

        /// <summary>
        /// Scales 2 curves given by _coeffs1 and _coeffs2 so they intersect at x=_thresh
        /// </summary>
        private static void _scaleCurvesIntersectAtThreshold(ref double[] _coeffs1, ref double[] _coeffs2, double _thresh)
        {
            // find (y at segment point) to y-scale about 
            double _yMaxPivot = TofRegression.RegressionPrediction(_coeffs1, GraspLearner.Max_SegmentPoint);
            double _yMinPivot = TofRegression.RegressionPrediction(_coeffs2, GraspLearner.Min_SegmentPoint);

            // find each y at the actual threshold
            double _yMaxThresh = TofRegression.RegressionPrediction(_coeffs1, _thresh);
            double _yMinThresh = TofRegression.RegressionPrediction(_coeffs2, _thresh);

            // find y at threshold so power and plate intersect at their average
            double _yAvgAtThreshold = 0.5d * (_yMaxThresh + _yMinThresh);

            // find y scale factor so intersects at (x,y) = (threshold, avg of power and plate), where avg = yPivot + k_i*(f_i - yPivot)
            double _kMax = (_yAvgAtThreshold -_yMaxPivot) / (_yMaxThresh -_yMaxPivot);
            double _kMin = (_yAvgAtThreshold -_yMinPivot) / (_yMinThresh -_yMinPivot);

            // scale coeffs //a,b -> ka,kb // c-> kc + ym (1-k)
            _scaleVectorInPlace(ref _coeffs1, _kMax);
            _scaleVectorInPlace(ref _coeffs2, _kMin);
            // offset constant term
            _coeffs1[0] += (_yMaxPivot * (1d - _kMax));
            _coeffs2[0] += (_yMinPivot * (1d - _kMin));
        }

        /// <summary>
        /// Return false if any set of coefficients is 0,0,0,etc 
        /// </summary>
        private static bool _coeffsComplete(int _f)
        {
            double _productOfSums = 1;
            for (int i=0; i<GraspTofCoeffsMax[_f].Length; i++)
            {
                double _sumMaxCoeffs = 0;
                double _sumMinCoeffs = 0;
                for (int j=0; j<GraspTofCoeffsMax[_f][i].Length; j++)
                {
                    _sumMaxCoeffs += GraspTofCoeffsMax[_f][i][j];
                    _sumMinCoeffs += GraspTofCoeffsMin[_f][i][j];
                }
                _productOfSums *= (_sumMaxCoeffs*_sumMinCoeffs);
            }
            return (!Mathf.Approximately((float)_productOfSums, 0f));
        }

        private static void _clearGraspTofCoeffs(ref double[][][] _graspTofCoeffs)
        {
            for (int i=0; i<_graspTofCoeffs.Length; i++)
            {
                for (int j=0; j<_graspTofCoeffs[i].Length; j++)
                {
                    for (int k=0; k<_graspTofCoeffs[i][j].Length; k++)
                    {
                        _graspTofCoeffs[i][j][k] = 0d;
                    }
                }
            }
        }

        /// <summary>
        /// Read from coefficients data file and output to arrays
        /// </summary>
        private static void _readCoeffsFile() 
        {
            for (int f=0; f<5; f++)
            {
                // Read coefficients for each finger from file
                StreamReader _sr = File.OpenText(toflearndataFilePath);
                string _s;
                while ((_s = _sr.ReadLine()) != null)
                {
                    if (_s.Equals(((FingerType)f).ToString()))
                    {
                        GraspTofCoeffsMax[f][0] = Array.ConvertAll(_sr.ReadLine().Split(','), double.Parse);
                        GraspTofCoeffsMax[f][1] = Array.ConvertAll(_sr.ReadLine().Split(','), double.Parse);
                        GraspTofCoeffsMin[f][0] = Array.ConvertAll(_sr.ReadLine().Split(','), double.Parse);
                        GraspTofCoeffsMin[f][1] = Array.ConvertAll(_sr.ReadLine().Split(','), double.Parse);
                    }
                }
                if (_coeffsComplete(f)) Debug.Log(((FingerType)f).ToString() + " coeffs read");
            }
        }

        // Start is called before the first frame update
        void Start()
        {
            // Initialise arrays
            for (int i=0; i<5; i++)
            {
                GraspTofCoeffsMax[i] = new double[2][];
                GraspTofCoeffsMin[i] = new double[2][];
                for (int j=0; j<2; j++) // Each finger has two segments
                {
                    // Each segment has a set of coeffs
                    GraspTofCoeffsMax[i][j] = new double[] {0};
                    GraspTofCoeffsMin[i][j] = new double[] {0};
                }
            }
        }

        // Update is called once per frame
        void Update()
        {
            // Handle key press events
            if (Input.GetKeyDown("s")) 
            {
                // Store learnt regression coefficients in file
                StringBuilder _acsb = new StringBuilder();
                for (int i=0; i<5; i++)
                { 
                    _acsb.AppendLine(((FingerType)i).ToString());
                    _acsb.AppendLine(String.Join(",", ArrToStr(GraspTofCoeffsMax[i][0])));
                    _acsb.AppendLine(String.Join(",", ArrToStr(GraspTofCoeffsMax[i][1])));
                    _acsb.AppendLine(String.Join(",", ArrToStr(GraspTofCoeffsMin[i][0])));
                    _acsb.AppendLine(String.Join(",", ArrToStr(GraspTofCoeffsMin[i][1])));
                }

                File.AppendAllText(toflearndataFilePath, _acsb.ToString());
                Debug.Log("Coeffs saved");
            }

            if (Input.GetKeyDown("a")) _readCoeffsFile();

            if (Input.GetKeyDown("c")) // Clear first before learning
            {
                _clearGraspTofCoeffs(ref GraspTofCoeffsMax);
                _clearGraspTofCoeffs(ref GraspTofCoeffsMin);
            }

			if (Input.GetKeyDown("j")) // Start finger learning data collection for POWER (MAX)
			{
                for (int f=1; f<3; f++) { myGraspLearners[f] = new GraspLearner((FingerType)f, GraspLearner.LearningGrasp.Power); }
			}
			if (Input.GetKeyDown("k")) // Start finger learning data collection for PLATE (MIN)
			{
				for (int f=1; f<3; f++) { myGraspLearners[f] = new GraspLearner((FingerType)f, GraspLearner.LearningGrasp.Plate); }
			}
			if (Input.GetKeyDown("n")) // Start thumb learning data collection for POWER (MAX)
			{
				for (int f=0; f<1; f++) { myGraspLearners[f] = new GraspLearner((FingerType)f, GraspLearner.LearningGrasp.Power); }
			}
			if (Input.GetKeyDown("m")) // Start thumb learning data collection for PLATE (MIN)
			{
				for (int f=0; f<1; f++) { myGraspLearners[f] = new GraspLearner((FingerType)f, GraspLearner.LearningGrasp.Plate); }
			}

            for (int f=0; f<5; f++)
            {
                if (myGraspLearners[f] != null && myGraspLearners[f].LearningActive == true)
                {
                    // If learning, append to datacollector if data is ready
                    if (TofManager.TofChanged[f] == true)
                        myGraspLearners[f].MyDataCollector.Append((double)TofManager.Graspness[f], (double)TofManager.TofReading[f]);

                    // If finished learning, start learning algorithm
                    if (myGraspLearners[f].MyDataCollector.CollectorFull)
                    {
                        // Print and save collected learning data for debug
                        DebugTools.CSVtoFile(myGraspLearners[f].MyDataCollector.createCSVTable(String.Format("{0} {1}", (FingerType)f, myGraspLearners[f].CurLearningGrasp)), DebugTools.CsvFilePath, "append");
                        Debug.Log(((FingerType)f).ToString() + " data collection finished, learning...");

                        // Get regression coefficients
                        double[][] _betas = ((FingerType)f != FingerType.THUMB) ? myGraspLearners[f].LearnGrasp() : myGraspLearners[f].LearnGraspThumb();
                        _betas.CopyTo(((myGraspLearners[f].CurLearningGrasp==GraspLearner.LearningGrasp.Power) ? GraspTofCoeffsMax[f] : GraspTofCoeffsMin[f]), 0);

                        Debug.Log(String.Format("{0} {1} grasp learned, coeffs1 {2}, coeffs2 {3}.", (FingerType)f, myGraspLearners[f].CurLearningGrasp, String.Join(",", ArrToStr(_betas[0])), String.Join(",", ArrToStr(_betas[1]))));

                        // Validation: If both max and min coeffs exist, adjust their coeffs (not for thumb)
                        if (_coeffsComplete(f) && f!=0)
                        {
                            Debug.Log("Adjusting coeffs" + f.ToString());
                            //Make sure power and plate only intersect outside threshold limits so prediction space is always valid, by scaling the coeffs
                            double _xIntersectionThresholdLeft = -2d;
                            double _xIntersectionThresholdRight = 102d;
                            double _xPlatePowerIntersectionLeft;
                            double _xPlatePowerIntersectionRight;

                            if (GraspTofCoeffsMax[f][0].Length == 2)
                            {
                                _xPlatePowerIntersectionLeft = _linearRoot(_deltaVector(GraspTofCoeffsMax[f][0], GraspTofCoeffsMin[f][0]));
                                _xPlatePowerIntersectionRight = _linearRoot(_deltaVector(GraspTofCoeffsMax[f][1], GraspTofCoeffsMin[f][1]));
                            }
                            else
                            {
                                _xPlatePowerIntersectionLeft = _closestQuadraticRoot(_deltaVector(GraspTofCoeffsMax[f][0], GraspTofCoeffsMin[f][0]), _xIntersectionThresholdLeft);
                                _xPlatePowerIntersectionRight = _closestQuadraticRoot(_deltaVector(GraspTofCoeffsMax[f][1], GraspTofCoeffsMin[f][1]), _xIntersectionThresholdRight);
                            }

                            if (_xPlatePowerIntersectionLeft > _xIntersectionThresholdLeft) //left intersection not low enough
                            {
                                _scaleCurvesIntersectAtThreshold(ref GraspTofCoeffsMax[f][0], ref GraspTofCoeffsMin[f][0], _xIntersectionThresholdLeft);
                            }

                            if (_xPlatePowerIntersectionRight <  _xIntersectionThresholdRight) //right intersection not high enough
                            {
                                _scaleCurvesIntersectAtThreshold(ref GraspTofCoeffsMax[f][1], ref GraspTofCoeffsMin[f][1], _xIntersectionThresholdRight);
                            }
                            Debug.Log(String.Format("{0} coeffs adjusted", (FingerType)f));
                        }

                        myGraspLearners[f].LearningActive = false;
                    }
                }
			}
        }
    }
}
