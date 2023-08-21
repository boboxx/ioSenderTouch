using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;
using CNC.Core;
using CNC.Core.Comands;
using CNC.GCode;


namespace ioSenderTouch.ViewModels
{
    public class SurfacingViewModel : INotifyPropertyChanged
    {
        private readonly GrblViewModel _model;
        private bool _usingInches;
        private string _unit;
        private string _unitPerMin;

        private const double Inches_To_MM = 25.4;
        public ICommand SurfacingCommand { get; set; }
        public ICommand UnitCommand { get; }
        public double ToolDiameter { get; set; }
        public double StockLength { get; set; }
        public double StockWidth { get; set; }
        public double DepthOfCut { get; set; }
        public int Passes { get; set; }
        public double FeedRate { get; set; }
        public double SpindleRpm { get; set; }

        public double OverLap { get; set; }

        public string Unit
        {
            get { return _unit; }
            set
            {
                if (_unit != value)
                {
                    _unit = value;
                    UnitPerMin = value;
                    OnPropertyChanged("Unit");
                }
            }
        }

        public string UnitPerMin
        {
            get { return _unitPerMin; }
            set
            {
                _unitPerMin = $"{value}/min";
                OnPropertyChanged("UnitPerMin");
            }
        }
        public bool UsingInches
        {
            get => _usingInches;
            set
            {
                if (_usingInches != value)
                {
                    _usingInches = value;
                    UpdateMeasurementUnit();
                }
            }
        }
        public SurfacingViewModel(GrblViewModel model)
        {
            _model = model;
            SurfacingCommand = new Command(ExecuteMethod);
            UnitCommand = new Command(SetUnits);
            Unit = "mm";
            _model.UtilityMacros.Add(new Macro
            {
                Name = "Quick Z Probe",
                Code = "G91F100\nG38.3F100Z-10\nG0Z0.5\nG38.3F25Z-2\nG0Z0\nG0Y0\nG0X0\nG10L2P1Z0",
                Id = 1,
                ConfirmOnExecute = true,
            });
        }

        public SurfacingViewModel()
        {
            
        }

        private void SetUnits(object obj)
        {
            var unit = obj.ToString();
            if (unit.Equals("Inch"))
            {
               _usingInches = true;
               Unit = "inch";
            }
            else
            {
                _usingInches = false;
                Unit = "mm";
            }
        }

        private void UpdateMeasurementUnit()
        {
            Unit = _usingInches ? "mm" : "inch";
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
            if (_usingInches)
            {
                width = width * Inches_To_MM;
                length = length * Inches_To_MM;
                feedRate = feedRate * Inches_To_MM;
                dia = dia * Inches_To_MM;
            }

            var gcode = new GcodeSurfacingBuilder(width, length, feedRate, dia, numberOfPasses, depth, overlap);
        }

        private void ExecuteMethod(object parameter)
        {
            if (!(parameter is string content)) return;
            switch (parameter)
            {
                case "Generate":
                    GenerateGcode();
                    break;
                default:
                    SetupUnits(content);
                    break;
            }
        }

        private void SetupUnits(string unit)
        {
            UsingInches = string.Equals("Inches", unit, StringComparison.CurrentCultureIgnoreCase);
            Unit = UsingInches ? "in" : "mm";
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
    private const string TravelFeedRate = "G1";
    private double _feedRate;
    private double _toolDiamete;
    private double overLap;

    public List<Movement> Movements { get; set; }
    private double X;
    private double Y;
    private double width;
    private double length;
    private double feedRate;
    private double dia;
    private int numberOfPasses;
    private double depth;

    public GcodeSurfacingBuilder(double width)
    {
        GenerateGcode();
    }

    public GcodeSurfacingBuilder(double width, double length, double feedRate, double dia, int numberOfPasses, double depth, double overlap)
    {
        this.width = width;
        this.length = length;
        _feedRate = feedRate;
        _toolDiamete = dia;
        this.numberOfPasses = numberOfPasses;
        this.depth = depth;
        this.overLap = overlap /100;

        var lines = width / (dia * overLap);
    }

    private void GenerateGcode()
    {
        
    }

}
public class Movement
{
    private const string LinearTravel = "G01";
    private const string FeedRate = "F";
    private const string X = "X";
    private const string Y = "Y";
    public string LinearMovement { get; internal set; }

    public Movement(string x, string y, string feedRate)
    {
        LinearMovement = $"{LinearTravel} {x} {y} {FeedRate}{feedRate}";
    }
}
