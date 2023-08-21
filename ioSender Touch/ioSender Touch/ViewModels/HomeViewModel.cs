using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CNC.Controls;
using CNC.Controls.Probing;
using CNC.Controls.Viewer;
using CNC.Core;
using CNC.Core.Comands;
using ioSenderTouch.Views;

namespace ioSenderTouch.ViewModels
{
    public class HomeViewModel : INotifyPropertyChanged
    {

        private bool? initOK = null;
        private bool isBooted = false;
        private GrblViewModel _grblViewModel;
        private Controller _controller = null;
        private ToolView _toolView;
        private RenderControl _renderView;
        private ProbingView _probeView;
        private SDCardView _sdView;
        private UserControl _view;
        private GrblConfigView _grblSettingView;
        private AppConfigView _grblAppSettings;
        private OffsetView _offsetView;
        private UtilityView _utilityView;

        public GrblViewModel GrblViewModel { get; set; }
        public ICommand ChangeView { get; }
        public UserControl View
        {
            get => _view;
            set
            {
                if (Equals(value, _view)) return;
                _view = value;
                OnPropertyChanged();
            }
        }

        public HomeViewModel(GrblViewModel grblViewModel)
        {
            _grblViewModel = grblViewModel;
            Grbl.GrblViewModel = _grblViewModel;
            _renderView = new RenderControl(_grblViewModel);
            _grblSettingView = new GrblConfigView(_grblViewModel);
            _grblAppSettings = new AppConfigView(_grblViewModel);
            _offsetView = new OffsetView(_grblViewModel);
            _utilityView = new UtilityView(_grblViewModel);
            AppConfig.Settings.SetupAndOpen(_grblViewModel, Application.Current.Dispatcher);
            InitSystem();
            ChangeView = new Command(SetNewView);
            _grblViewModel.PropertyChanged += OnDataContextPropertyChanged;

        }

        private void OnDataContextPropertyChanged(object sender, PropertyChangedEventArgs e)
        {

            switch (e.PropertyName)
            {
                case nameof(GrblViewModel.GrblState):
                    if (_controller != null && !_controller.ResetPending)
                    {
                        if (isBooted && initOK == false && _grblViewModel.GrblState.State != GrblStates.Alarm)
                            InitSystem();
                    }
                    break;

                case nameof(GrblViewModel.IsGCLock):

                    break;

                case nameof(GrblViewModel.IsSleepMode):

                    break;

                case nameof(GrblViewModel.IsJobRunning):

                    if (GrblInfo.ManualToolChange)
                        GrblCommand.ToolChange = _grblViewModel.IsJobRunning ? "T{0}M6" : "M61Q{0}";
                    break;

                case nameof(GrblViewModel.IsToolChanging):

                    break;

                case nameof(GrblViewModel.Tool):
                    if (GrblInfo.ManualToolChange && _grblViewModel.Tool != GrblConstants.NO_TOOL)
                        GrblWorkParameters.RemoveNoTool();
                    break;

                case nameof(GrblViewModel.GrblReset):
                    if (_grblViewModel.IsReady)
                    {
                        if (!_controller.ResetPending && _grblViewModel.GrblReset)
                        {
                            initOK = null;
                            Activate(true, ViewType.GRBL);
                        }
                    }
                    break;
                case nameof(GrblViewModel.ParserState):
                    if (!_controller.ResetPending && _grblViewModel.GrblReset)
                    {

                        _grblViewModel.GrblReset = false;
                    }
                    break;

                case nameof(GrblViewModel.FileName):
                    string filename = _grblViewModel.FileName;
                    MainWindow.ui.WindowTitle = filename;

                    if (string.IsNullOrEmpty(filename))
                        MainWindow.CloseFile();
                    else if (_grblViewModel.IsSDCardJob)
                    {
                        //TODO set up sd card view 

                        //MainWindow.EnableView(false, ViewType.GCodeViewer);
                    }
                    else if (AppConfig.Settings.GCodeViewer.IsEnabled)
                    {
                        if (filename.StartsWith("Wizard:"))
                        {

                            _renderView.Open(GCode.File.Tokens);
                        }
                        else if (!string.IsNullOrEmpty(filename))
                        {
                            _grblViewModel.Poller.SetState(0);
                            _renderView.Open(GCode.File.Tokens);
                            _grblViewModel.Poller.SetState(AppConfig.Settings.Base.PollInterval);
                            _renderView.Open(GCode.File.Tokens);
                        }
                    }

                    break;

            }
        }

        public void Activate(bool activate, ViewType chgMode)
        {
            if (activate)
            {
                //GCodeSender.RewindFile();
                //GCodeSender.CallHandler(GCode.File.IsLoaded ? StreamingState.Idle : (_grblViewModel.IsSDCardJob ? StreamingState.Start : StreamingState.NoFile), false);

                _grblViewModel.ResponseLogFilterOk = AppConfig.Settings.Base.FilterOkResponse;

                if (_controller == null)
                    _controller = new Controller(_grblViewModel);

                if (initOK != true)
                {
                    switch (_controller.Restart())
                    {
                        case Controller.RestartResult.Ok:
                            if (!isBooted)
                                OnBooted();
                            initOK = InitSystem();
                            break;

                        case Controller.RestartResult.Close:
                            MainWindow.ui.Close();
                            break;

                        case Controller.RestartResult.Exit:
                            Environment.Exit(-1);
                            break;
                    }

                    _grblViewModel.Message = _controller.Message;
                }

                if (initOK == null)
                    initOK = false;


                if (GCode.File.IsLoaded)
                    MainWindow.ui.WindowTitle = _grblViewModel.FileName;

                _grblViewModel.Keyboard.JogStepDistance = AppConfig.Settings.Jog.LinkStepJogToUI ? AppConfig.Settings.JogUiMetric.Distance0 : AppConfig.Settings.Jog.StepDistance;
                _grblViewModel.Keyboard.JogDistances[(int)KeypressHandler.JogMode.Slow] = AppConfig.Settings.Jog.SlowDistance;
                _grblViewModel.Keyboard.JogDistances[(int)KeypressHandler.JogMode.Fast] = AppConfig.Settings.Jog.FastDistance;
                _grblViewModel.Keyboard.JogFeedrates[(int)KeypressHandler.JogMode.Step] = AppConfig.Settings.Jog.StepFeedrate;
                _grblViewModel.Keyboard.JogFeedrates[(int)KeypressHandler.JogMode.Slow] = AppConfig.Settings.Jog.SlowFeedrate;
                _grblViewModel.Keyboard.JogFeedrates[(int)KeypressHandler.JogMode.Fast] = AppConfig.Settings.Jog.FastFeedrate;
                _grblViewModel.Keyboard.IsJoggingEnabled = AppConfig.Settings.Jog.Mode != JogConfig.JogMode.UI;

                if (!GrblInfo.IsGrblHAL)
                    _grblViewModel.Keyboard.IsContinuousJoggingEnabled = AppConfig.Settings.Jog.KeyboardEnable;
            }

        }

        public void CloseFile()
        {
            _renderView.Close();
        }

        private void OnBooted()
        {
            isBooted = true;
            string filename = Resources.Path + $"KeyMap{(int)AppConfig.Settings.Jog.Mode}.xml";

            if (System.IO.File.Exists(filename))
                _grblViewModel.Keyboard.LoadMappings(filename);

            if (GrblInfo.NumAxes > 3)
                GCode.File.AddTransformer(typeof(GCodeWrapViewModel), "Wrap to rotary (WIP)", MainWindow.UIViewModel.TransformMenuItems);
        }

        private bool InitSystem()
        {
            initOK = true;
            int timeout = 5;
            _grblViewModel.Poller.SetState(0);
            using (new UIUtils.WaitCursor())
            {
                while (!GrblInfo.Get())
                {
                    if (--timeout == 0)
                    {
                        _grblViewModel.Message = ("MsgNoResponse");
                        return false;
                    }
                    Thread.Sleep(500);
                }
                GrblAlarms.Get();
                GrblErrors.Get();
                GrblSettings.Load();
                if (GrblInfo.IsGrblHAL)
                {
                    GrblParserState.Get();
                    GrblWorkParameters.Get();
                }
                else
                    GrblParserState.Get(true);
                _grblViewModel.Poller.SetState(AppConfig.Settings.Base.PollInterval);

            }

            GrblCommand.ToolChange = GrblInfo.ManualToolChange ? "M61Q{0}" : "T{0}";


            if (_grblViewModel.HasSDCard)
            {
                _sdView = new SDCardView(_grblViewModel);
            }

            if (_grblViewModel.HasATC)
            {
                _toolView = new ToolView(_grblViewModel);
            }

            if (GrblInfo.HasProbe && GrblSettings.ReportProbeCoordinates)
            {
                _grblViewModel.HasProbing = true;
                _probeView = new ProbingView(_grblViewModel);
                _probeView.Activate(true, ViewType.Probing);

            }
            return true;
        }
        private void Button_ClickSDView(object sender, RoutedEventArgs e)
        {
            //  FillBorder.Child = _sdView;
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //FillBorder.Child = _grblSettingView;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            // FillBorder.Child = _probeView;

        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            //FillBorder.Child = _renderView;
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            // FillBorder.Child = _grblAppSettings;

        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            // FillBorder.Child = _offsetView;
        }

        private void Button_Click_Utility(object sender, RoutedEventArgs e)
        {

        }
        private void Button_Click_Tools(object sender, RoutedEventArgs e)
        {
            // FillBorder.Child = _toolView;
        }

        public void ConfiguationLoaded(UIViewModel uiViewModel, AppConfig settings)
        {
            _grblAppSettings.Setup(uiViewModel, settings);
        }

        public void SetNewView(object x)
        {

            switch (x.ToString())
            {

            }
        }
        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            //if (CodeListControl.Visibility == Visibility.Visible)
            //{
            //    CodeListControl.Visibility = Visibility.Hidden;
            //    ConsoleControl.Visibility = Visibility.Visible;
            //}
            //else
            //{
            //    CodeListControl.Visibility = Visibility.Visible;
            //    ConsoleControl.Visibility = Visibility.Hidden;
            //}


            //btnShowConsole.Content = ConsoleControl.Visibility == Visibility.Hidden ? "Console" : "GCode Viewer";
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

