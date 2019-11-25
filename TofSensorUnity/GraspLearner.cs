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
    /// Encapsulating class to store collected data for learning
    /// </summary>
    class DataCollector
    {
        /// <summary>
        /// Array of two lists of doubles containing collected sensor data
        /// </summary>
        private List<double>[] dataCollector;
        public List<double> this[int _ind] { get { return dataCollector[_ind]; }}
        public bool CollectorFull { get { return (this[0].Capacity == this[0].Count); }}

        /// <summary>
        /// Constructor from collector capacity
        /// </summary>
        public DataCollector(int _dataCollectorCapacity)
        {
            dataCollector = new List<double>[] {new List<double>(_dataCollectorCapacity), new List<double>(_dataCollectorCapacity)};
        }

        /// <summary>
        /// Constructor from existing array of lists
        /// </summary>
        public DataCollector(List<double>[] _lists)
        {
            dataCollector = new List<double>[] {new List<double>(_lists[0]), new List<double>(_lists[1])};
        }

        /// <summary>
        /// Add next line of data to data collector
        /// </summary>
        public void Append(double x, double y)
        {
            this[0].Add(x);
            this[1].Add(y);
        }

        /// <summary>
        /// Create CSV table from collector to be outputted for debug
        /// </summary>
        /// <param name="_header">First line of CSV</param>
        public StringBuilder createCSVTable (string _header)
        {
            StringBuilder _sb = new StringBuilder(Environment.NewLine + _header + Environment.NewLine);

            for (int i=0; i < this[0].Count; i++)
            {
               _sb.AppendLine(this[0][i].ToString() + "," + this[1][i].ToString());
            }
            return _sb;
        }
        private List<double>[] _toLists()
        {
            return new List<double>[] {new List<double>(this[0]), new List<double>(this[1])};
        }

        /// <summary>
        /// Remove all values in collector when the corresponding _refVal > _refThresh, returns reduced datacollector
        /// </summary>
        /// <param name="_refThresh">Reference threshold</param>
        /// <param name="_refVals">Values to compare against threshold, same length as collector lists</param>
        public DataCollector RemoveAboveThresh(double _refThresh, List<double> _refVals)
        {
            List<double>[] _outLists = this._toLists();
            for (int i=_refVals.Count-1; i>=0; i--)
            {
                if (_refVals[i] > _refThresh)
                {
                    for (int l=0; l<_outLists.Length; l++)
                    {
                        _outLists[l].RemoveAt(i);
                    }
                }
            }
            return new DataCollector(_outLists);
        }

        /// <summary>
        /// Segment this collector into two sections, based on comparing values of column 1 of this collector with _segmentPoint
        /// </summary>
        /// <param name="_outLists1">Reference of first output collector</param>
        /// <param name="_outLists2">Reference of second output collector</param>
        /// <param name="_segmentPoint">Value to compare</param>
        public bool SegmentLists(ref List<double>[] _outLists1, ref List<double>[] _outLists2, int _segmentPoint) 
        {
            List<double>[] _inLists = this._toLists();
            int _nColumns = _inLists.Length;
            int _nRows = _inLists[0].Count;
            bool _segmentSuccess = false;

            float[] _ins0floats = Array.ConvertAll(_inLists[0].ToArray(), item => (float)item); //Mathf only accepts array of floats
            int _xMax = (int)Mathf.Max(_ins0floats);
            int _xMin = (int)Mathf.Min(_ins0floats);
            int _xRange = _xMax - _xMin;

            for (int c=0; c<_nColumns; c++)
            {
                _outLists1[c] = new List<double>();
                _outLists2[c] = new List<double>();
            }

            for (int i=0; i<_nRows; i++)
            {
                for (int c=0; c<_nColumns; c++)
                {
                    ((_inLists[0][i] < _segmentPoint) ? _outLists1[c] : _outLists2[c]).Add( _inLists[c][i] );
                }
            }

            // Make sure significant amount of data on either side of divide           
            _segmentSuccess = ((_xMax - _segmentPoint >= (int)(_xRange/4f)) && //array max is at least third of total range away
                               (_segmentPoint - _xMin >= (int)(_xRange/4f)) && //array min is at least third of total range away
                               (_outLists1[0].Count >= (int)(_nRows/4f))   && //at least quarter of sample size in either list
                               (_outLists2[0].Count >= (int)(_nRows/4f)));

            if (!_segmentSuccess) //segmentation not good, copy collector to outs1 without segment
            {
                for (int c=0; c<_nColumns; c++)
                {
                    _outLists1[c] = new List<double>(_inLists[c]);
                    _outLists2[c] = new List<double>();
                }
            }
            return _segmentSuccess;
        } 
    }


	/// <summary>
    /// Handles data collection, regression and output of coefficients for each finger
    /// </summary>    
    class GraspLearner
    {
        /// <summary>
        /// Enum containing whether this Learner is for Power(max) or Plate(min)
        /// </summary>
        public enum LearningGrasp {Power, Plate};

        /// <summary>
        /// Current learning grasp
        /// </summary>
        public LearningGrasp CurLearningGrasp {get; private set;}

        /// <summary>
        /// Store for collecting Sensor and Graspness data for regression
        /// </summary>
		private DataCollector dataCollector = new DataCollector(200);
        public DataCollector MyDataCollector { get {return dataCollector;} }

        /// <summary>
        /// Type of finger
        /// </summary>
        private FingerType fingerType;

        /// <summary>
        /// Point to split data collector to perform two separate regressions, for Power(max) grasp trajectory
        /// </summary>
        public static int Max_SegmentPoint = 40;

        /// <summary>
        /// Point to split data collector to perform two separate regressions, for Plate(min) grasp trajectory
        /// </summary>
        public static int Min_SegmentPoint = 40;

        /// <summary>
        /// If learning is still being used, otherwise, dispose
        /// </summary>
        public bool LearningActive {get; set;}


        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="_fingerType">Type of finger</param>
        /// <param name="_learningGrasp">Type of grasp to learn</param>
        public GraspLearner(FingerType _fingerType, LearningGrasp _learningGrasp)
        {
            fingerType = _fingerType;
            CurLearningGrasp = _learningGrasp;
            LearningActive = true;

            Debug.Log(String.Format("{0} <color=blue> {1} </color> learning started...", fingerType, _learningGrasp));
        }

        /// <summary>
        /// Run regression algorithm on collected data, returns two arrays of fitted coefficients, one for each segment
        /// </summary>
  		public double[][] LearnGrasp ()
		{
            // Remove outliers from main parabolic grasp trajectories
            // 1. Perform preliminary regression, remove entries where abs residuals > 2*SD
            double[] _preliminaryRegressionCoeffs = TofRegression.QuadraticFit(dataCollector[0], dataCollector[1]);
            List<double> _preliminaryAbsResiduals = TofRegression.RegressionAbsDeviations(dataCollector[0], dataCollector[1], _preliminaryRegressionCoeffs);
            double _stdDev = TofRegression.SStDev(_preliminaryAbsResiduals, _meanIsZero: true);
            DataCollector _reducedDataCollector = dataCollector.RemoveAboveThresh(2d * _stdDev, _preliminaryAbsResiduals);

            // 2. New regression with reduced sample, remove entries of original sample where abs deviations according to new regression > 1.5*SD
            double[] _newRegressionCoeffs = TofRegression.QuadraticFit(_reducedDataCollector[0], _reducedDataCollector[1]);
            List<double> _newAbsDeviations = TofRegression.RegressionAbsDeviations(dataCollector[0], dataCollector[1], _newRegressionCoeffs);
            double _stdDevNew = TofRegression.SStDev(_newAbsDeviations, _meanIsZero: false);
            DataCollector _finalDataCollector = dataCollector.RemoveAboveThresh(1.5d * _stdDevNew, _newAbsDeviations);

            // Segment lists
            List<double>[] _dc1 = new List<double>[2];
            List<double>[] _dc2 = new List<double>[2];

            bool _segmentSuccess = _finalDataCollector.SegmentLists(ref _dc1, ref _dc2, (CurLearningGrasp==LearningGrasp.Power) ? Max_SegmentPoint : Min_SegmentPoint);

			// Perform linear regression on cleaner dataset
            double[][] _betas = new double[2][];
			_betas[0]  = TofRegression.LinearFit(_dc1[0], _dc1[1]);
            _betas[1] = (_segmentSuccess) ? TofRegression.LinearFit(_dc2[0], _dc2[1]) : _betas[0]; //if not segmented, just use same coeffs

            return _betas;
		}

        /// <summary>
        /// Run thumb regression algorithm on collected data, returns fitted coefficients (two sets both the same)
        /// </summary>
        public double[][] LearnGraspThumb ()
        {
            // Skip removing outliers and segment, and directly regress
            // Perform quadratic regression on dataset
            double[][] _betas = new double[2][];
			_betas[0]  = TofRegression.QuadraticFit(dataCollector[0], dataCollector[1]);
            _betas[1] =  _betas[0]; //just use same coeffs
            return _betas;
        }
    }
}