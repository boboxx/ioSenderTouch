using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using CNC.Controls;
using CNC.Core;
using CNC.Core.Comands;
using CNC.GCode;
using Microsoft.Win32;


namespace ioSenderTouch.ViewModels
{
    public class SurfacingViewModel : INotifyPropertyChanged
    {
        private const string Metric = "Metric";
        private const string Inches = "Inches";

        private const double Inches_To_MM = 25.4;
        private const double Safe_Height = .12;
        private readonly GrblViewModel _model;
        private bool _usingInches;
        private string _unit;
        private string _unitPerMin;
        private bool _metric;
        private bool _inches;
        private string _measurement;

        public ICommand SurfacingCommand { get; set; }
        public double ToolDiameter { get; set; }
        public double StockLength { get; set; }
        public double StockWidth { get; set; }
        public double DepthOfCut { get; set; }
        public int Passes { get; set; }
        public double FeedRate { get; set; }
        public double SpindleRpm { get; set; }

        public double OverLap { get; set; }

        public string FilePath { get; set; }

        public string Measurement
        {
            get => _measurement;
            set
            {
                if (_measurement != value)
                {
                    _measurement = value;
                    SetUnits(value);
                    OnPropertyChanged(nameof(Measurement));
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
                    
                    OnPropertyChanged("Unit");
                }
            }
        }

        public string UnitPerMin
        {
            get => _unitPerMin;
            set
            {
                _unitPerMin = $"{value}/min";
                OnPropertyChanged("UnitPerMin");
            }
        }

        public SurfacingViewModel(GrblViewModel model)
        {
            _model = model;
            SurfacingCommand = new Command(ExecuteMethod); 
            Passes = 1;
            OverLap = 50;
            _model.UtilityMacros.Add(new Macro
            {
                Name = "Quick Z Probe",
                Code = "G91F100\nG38.3F100Z-10\nG0Z0.5\nG38.3F25Z-2\nG0Z0\nG0Y0\nG0X0\nG10L2P1Z0",
                Id = 1,
                ConfirmOnExecute = true,
            });
            AppConfig.Settings.OnConfigFileLoaded += Settings_OnConfigFileLoaded;

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

        public void BuildJobMacro(SurfaceConfig config)
        {
            _model.UtilityMacros.Add(new Macro
            {
                Name = "Quick Surfacing",
                Code = config.FilePath,
                Id = 2,
                ConfirmOnExecute = true,
                isJob = true,
                Path = config.FilePath
            });
        }

        private void SetUnits(string unit)
        {
            _usingInches = unit.Equals("Inches");
            UpdateMeasurementUnit();
        }
        private void UpdateMeasurementUnit()
        {
          Unit =UnitPerMin = _usingInches ? "inch" : "mm";
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
            if (_usingInches)
            {
                width = width * Inches_To_MM;
                length = length * Inches_To_MM;
                feedRate = feedRate * Inches_To_MM;
                dia = dia * Inches_To_MM;
                safeHeight = Safe_Height * Inches_To_MM;
                depth = depth * Inches_To_MM;
            }

            var surfaceGcode = new GcodeSurfacingBuilder(width, length, feedRate, dia, numberOfPasses, depth, overlap, rpm, safeHeight);
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

            var hasMacro = _model.UtilityMacros.Any(x => x.Name.Equals(macro.Name));
            if (hasMacro)
            {
                var foundMacro = _model.UtilityMacros.First(x => x.Name.Equals(macro.Name));
                _model.UtilityMacros.Remove(foundMacro);
            }
            _model.UtilityMacros.Add(macro);
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
                TooDiameter = ToolDiameter
            };
            AppConfig.Settings.Save();
        }

        private void ExecuteMethod(object parameter)
        {
            GenerateGcode();
        }

        
        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyname)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyname));
            }
        }

    }
}


public class GcodeSurfacingBuilder
{

    private const string End = "M30";
    private const string TurnOffFlood = "M9";
    private const string Flood = "M8";
    private const string Mist = "M7";
    private const string TravelFeedRate = "G1";
    private const string RapidFeedRate = "G0";
    private const string Metric = "G21";
    private const string Standard = "G20";
    private const string AbsolutePos = "G91";
    private const string IncrementalPos = "G90";
    private const string X0 = "X0";
    private const string Y0 = "Y0";
    private const string SpindleOn = "M3";
    private const string SpindleOff = "M5";
    private const string Pause = "G4";

    private double _feedRate;
    private double _toolDiameter;
    private double _width;
    private double _length;
    private int _numberOfPasses;
    private double _depth;
    private double _rpm;
    private double _safeHeight;
    private double _overLapPercent;

    public string FilePath { get; set; }
    public GcodeSurfacingBuilder(double width, double length, double feedRate, double dia, int numberOfPasses, double depth, double overlap, double rpm, double safeHeight)
    {
        _width = width;
        _length = length;
        _feedRate = feedRate;
        _toolDiameter = dia;
        _numberOfPasses = numberOfPasses;
        _depth = depth;
        _rpm = rpm;
        _safeHeight = safeHeight;
        _overLapPercent = overlap / 100;
        GenerateGcode();
    }

    private void GenerateGcode()
    {
        var gcodeList = new List<string>();
        gcodeList.AddRange(BuildHeader());
        gcodeList.AddRange(BuildGcodeRamp());
        gcodeList.AddRange(BuildSurfaceLayer());
        gcodeList.AddRange(BuildShutDown());
        WriteFile(gcodeList);
    }
    private List<string> BuildHeader()
    {
        var header = new List<string>
        {
            ";Header",
            Metric,
            IncrementalPos,
            $"{RapidFeedRate}{X0}{Y0}Z{_safeHeight:f4}",
            $"{TravelFeedRate}F{_feedRate}",
            $"{SpindleOn}S{_rpm}",
            $"{Pause}P4",
        };
        return header;
    }
    private List<string> BuildGcodeRamp()
    {
        var ramp1 = _toolDiameter;
        var ramp2 = _toolDiameter / 4;
        var rampReturn = ramp1 + ramp2;
        var depth = _safeHeight + _depth;
        var formattedDepth = $"{depth:f4}";
        var ramping = new List<string>
        {
            $";Ramping into Stock",
            $"{AbsolutePos}",
            $"{TravelFeedRate}X{ramp1}Z-{formattedDepth}",
            $"{TravelFeedRate}X{ramp2}",
            $"{TravelFeedRate}X-{rampReturn}",
            $"{IncrementalPos}",
        };
        return ramping;
    }

    public IEnumerable<string> BuildSurfaceLayer()
    {
        var overlapMeasurement = _toolDiameter * _overLapPercent;
        _width += overlapMeasurement;
        _length += overlapMeasurement;
        var lines = _width / _toolDiameter;
        double y = 0;
        var layer = 1;
        var gcodeList = new List<string> { $";Layer{layer}" };
        for (int i = 0; i <= lines; i++)
        {
            gcodeList.Add($"G1 Y{y:f4}");
            gcodeList.Add($"G1 X{_length}");
            y += overlapMeasurement;
            gcodeList.Add($"G1 Y{y:f4}");
            gcodeList.Add("G1 X0");
            y += overlapMeasurement;
        }
        return gcodeList;
    }

    private void WriteFile(List<string> gcodeList)
    {
        var docPath = AppDomain.CurrentDomain.BaseDirectory;
        FilePath = Path.Combine(docPath, "QuickSurface.nc");
        using (var outputFile = new StreamWriter(FilePath))
        {
            foreach (string line in gcodeList)
                outputFile.WriteLine(line);
        }
    }

    private IList<string> BuildShutDown()
    {
        var endList = new List<string>
       {
           ";ShutDown",
           $"{RapidFeedRate}Z{_safeHeight}",
           SpindleOff,
           TurnOffFlood
       };
        return endList;
    }
}
