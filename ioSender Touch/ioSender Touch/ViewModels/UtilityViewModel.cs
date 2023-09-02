
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using CNC.Controls;
using CNC.Controls.Views;
using CNC.Core;
using CNC.Core.Comands;
using CNC.GCode;
using ioSenderTouch.Controls;
using ioSenderTouch.Utility;

namespace ioSenderTouch.ViewModels
{
    public class UtilityViewModel : INotifyPropertyChanged
    {
        private  string _instructions =
            "-Load spindle with center point bit\n such 90 degrees chamfer bit or pointed dial.\n\r" +
            "-Zero stock at lower left corner.\n\r" +
            "-Enter measurement of desired triangle.\r"+
            "The larger the triangle the increase in accuracy\r\n"+
            "-Note: Using low tac tape at point A, B and C\nmay increase visibility of imprint marks \n"+
            "smaller imprint marks using less Z depth will help increase accuracy\n" +
            "but may increase difficulty in measuring";

        private const string Metric = "Metric";
        private const string Inches = "Inches";

        private readonly GrblViewModel _grblViewModel;
        private UIElement _control;
        private readonly ErrorsAndAlarms _alarmAndError;
        private readonly MacroEditor _macroEditor;
        private readonly SurfacingControl _surfacingControl;
        private readonly CalibrationControl _xyCalibrationControl;


        private const double Inches_To_MM = 25.4;
        private const double Safe_Height = .12;
        private bool _usingInches;
        private string _unit;
        private string _unitPerMin;
        private bool _metric;
        private bool _inches;
        private string _measurement;
        private bool _mist;
        private bool _flood;
        private double _measureA;
        private double _lengthA;
        private double _lengthB;
        private double _depthCalibration;
        private double _measureB;
        private double _measureC;
        private double _feedRateCalibration;
        private string _measurementResults;
        private readonly CalibrationTriangle _calibrationTriangle
            ;


        public ICommand ShowView { get; }
        public ICommand SurfacingCommand { get; set; }
        public ICommand CalibrationRunCommand { get; set; }
        public ICommand CalibrationResultsCommand { get; set; }

        public double ToolDiameter { get; set; }
        public double StockLength { get; set; }
        public double StockWidth { get; set; }
        public double DepthOfCut { get; set; }
        public int Passes { get; set; }
        public double FeedRate { get; set; }
        public double SpindleRpm { get; set; }
        public double OverLap { get; set; }
        public string FilePath { get; set; }

      

        public bool Flood
        {
            get => _flood;
            set
            {
                if (value == _flood) return;
                _flood = value;
                OnPropertyChanged();
            }
        }

        public bool Mist
        {
            get => _mist;
            set
            {
                if (value == _mist) return;
                _mist = value;
                OnPropertyChanged();
            }
        }

        public string Measurement
        {
            get => _measurement;
            set
            {
                if (_measurement != value)
                {
                    _measurement = value;
                    SetUnits(value);
                    OnPropertyChanged();
                }
            }
        }
        public string Unit
        {
            get => _unit;
            set
            {
                if (_unit != value)
                {
                    _unit = value;
                    OnPropertyChanged();
                }
            }
        }

        public string UnitPerMin
        {
            get => _unitPerMin;
            set
            {
                _unitPerMin = $"{value}/min";
                OnPropertyChanged();
            }
        }

        public string Instructions
        {
            get => _instructions;
            set
            {
                if (value == _instructions) return;
                _instructions = value;
                OnPropertyChanged();
            }
        }
        public double LengthA
        {
            get => _lengthA;
            set
            {
                if (value.Equals(_lengthA)) return;
                _lengthA = value;
                OnPropertyChanged();
            }
        }

        public double LengthB
        {
            get => _lengthB;
            set
            {
                if (value.Equals(_lengthB)) return;
                _lengthB = value;
                OnPropertyChanged();
            }
        }

        public double DepthCalibration
        {
            get => _depthCalibration;
            set
            {
                if (value.Equals(_depthCalibration)) return;
                _depthCalibration = value;
                OnPropertyChanged();
            }
        }

        public double FeedRateCalibration
        {
            get => _feedRateCalibration;
            set
            {
                if (value.Equals(_feedRateCalibration)) return;
                _feedRateCalibration = value;
                OnPropertyChanged();
            }
        }

        public double MeasureA
        {
            get => _measureA;
            set
            {
                if (value.Equals(_measureA)) return;
                _measureA = value;
                OnPropertyChanged();
            }
        }

        public double MeasureB
        {
            get => _measureB;
            set
            {
                if (value.Equals(_measureB)) return;
                _measureB = value;
                OnPropertyChanged();
            }
        }

        public double MeasureC
        {
            get => _measureC;
            set
            {
                if (value.Equals(_measureC)) return;
                _measureC = value;
                OnPropertyChanged();
            }
        }

        public string MeasurementResults
        {
            get => _measurementResults;
            set
            {
                if (value == _measurementResults) return;
                _measurementResults = value;
                OnPropertyChanged();
            }
        }


     
        public UtilityViewModel(GrblViewModel grblViewModel)
        {
            _grblViewModel = grblViewModel;
            ShowView = new Command(SetView);
            CalibrationResultsCommand = new Command(CreateCalibrationResults);
            CalibrationRunCommand = new Command(CreateCalibrationJob);
            _alarmAndError = new ErrorsAndAlarms("");
            _surfacingControl = new SurfacingControl();
            _xyCalibrationControl = new CalibrationControl();
            _macroEditor = new MacroEditor();
            SurfacingCommand = new Command(ExecuteMethod);
            Passes = 1;
            OverLap = 50;
            BuildProbeMacro();
            AppConfig.Settings.OnConfigFileLoaded += Settings_OnConfigFileLoaded;
            _calibrationTriangle = new CalibrationTriangle();
            //CalculateAngle(400, 300, 500);
            //CalculateAngle(400, 300, 500.1714);
            //CalculateTriangle(400, 300);
        }

        private void BuildProbeMacro()
        {
            _grblViewModel.UtilityMacros.Add(new Macro
            {
                Name = "Quick Z Probe",
                Code = "G91F100\nG38.3F100Z-10\nG10L20P0Z0.000\nG0Z5.000",
                Id = 1,
                ConfirmOnExecute = true,
            });
        }

        private void Settings_OnConfigFileLoaded(object sender, EventArgs e)
        {

            var surface = AppConfig.Settings.Base.Surface;
            if (surface == null) return;
            PopulateUI(surface);
        }

        private void PopulateUI(SurfaceConfig config)
        {
            _usingInches = config.IsInches;
            Measurement = _usingInches ? Inches : Metric;
            Mist = config.Mist;
            Flood = config.Flood;
            SpindleRpm = config.SpindleRPM;
            Passes = config.Passes;
            FeedRate = config.FeedRate;
            OverLap = config.Overlap;
            DepthOfCut = config.Depth;
            StockLength = config.StockLength;
            StockWidth = config.StockWidth;
            ToolDiameter = config.TooDiameter;

            if (!File.Exists(config.FilePath)) return;
            FilePath = config.FilePath;
            BuildJobMacro(config);

        }
        private void SetView(object view)
        {
            switch (view.ToString())
            {
                case "Alarm":
                    Control = _alarmAndError;
                    break;
                case "Macro":
                    Control = _macroEditor;
                    break;
                case "Surfacing":
                    Control = _surfacingControl;
                    break;
                case "Calibration":
                    Control = _xyCalibrationControl;
                    break;
            }
        }
        public void BuildJobMacro(SurfaceConfig config)
        {
            _grblViewModel.UtilityMacros.Add(new Macro
            {
                Name = "Quick Surfacing",
                Code = config.FilePath,
                Id = 2,
                ConfirmOnExecute = true,
                isJob = true,
                Path = config.FilePath
            });
        }
        private void ExecuteMethod(object parameter)
        {
            GenerateGcode();
        }
        private void SetUnits(string unit)
        {
            _usingInches = unit.Equals("Inches");
            UpdateMeasurementUnit();
        }
        private void UpdateMeasurementUnit()
        {
            Unit = UnitPerMin = _usingInches ? "inch" : "mm";
        }
        private void GenerateGcode()
        {
            var width = StockWidth;
            var length = StockLength;
            var dia = ToolDiameter;
            var rpm = SpindleRpm;
            var feedRate = FeedRate;
            var numberOfPasses = Passes;
            var depth = DepthOfCut;
            var overlap = OverLap;
            var safeHeight = Safe_Height;
            var mist = Mist;
            var flood = Flood;
            if (_usingInches)
            {
                width = width * Inches_To_MM;
                length = length * Inches_To_MM;
                feedRate = feedRate * Inches_To_MM;
                dia = dia * Inches_To_MM;
                safeHeight = Safe_Height * Inches_To_MM;
                depth = depth * Inches_To_MM;
            }

            var surfaceGcode = new GcodeSurfacingBuilder(width, length, feedRate, dia, numberOfPasses, depth, overlap, rpm, safeHeight, mist, flood);
            var macro = new Macro
            {
                Name = "Quick Surfacing",
                Code = surfaceGcode.FilePath,
                Id = 2,
                ConfirmOnExecute = true,
                isJob = true,
                Path = surfaceGcode.FilePath
            };
            FilePath = surfaceGcode.FilePath;

            var hasMacro = _grblViewModel.UtilityMacros.Any(x => x.Name.Equals(macro.Name));
            if (hasMacro)
            {
                var foundMacro = _grblViewModel.UtilityMacros.First(x => x.Name.Equals(macro.Name));
                _grblViewModel.UtilityMacros.Remove(foundMacro);
            }
            _grblViewModel.UtilityMacros.Add(macro);
            SaveMacro();

        }

        private void SaveMacro()
        {
            AppConfig.Settings.Base.Surface = new SurfaceConfig
            {
                SpindleRPM = SpindleRpm,
                Passes = Passes,
                FeedRate = FeedRate,
                Depth = DepthOfCut,
                FilePath = FilePath,
                IsInches = _usingInches,
                Overlap = (int)OverLap,
                StockLength = StockLength,
                StockWidth = StockWidth,
                TooDiameter = ToolDiameter,
                Flood = Flood,
                Mist = Mist
            };
            AppConfig.Settings.Save();
        }

        public UIElement Control
        {
            get => _control;
            set
            {
                if (Equals(value, _control)) return;
                _control = value;
                OnPropertyChanged();
            }
        }

        private void CreateCalibrationJob(object x)
        {
            _calibrationTriangle.CalculateTriangle(LengthA, LengthB);
        }

        private void CreateCalibrationResults(object x)
        {
          var triangle =  _calibrationTriangle.CalculateResults(MeasureA, MeasureB, MeasureC);
        
         var delta = _calibrationTriangle.CalculateDelta(triangle);
         var leftDelta = delta * -1;
         MeasurementResults = $"Move Left Axis: { leftDelta}\n or Right Axis: { delta}";
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
