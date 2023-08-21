
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CNC.Controls;
using CNC.Controls.Probing;
using CNC.Controls.Viewer;
using CNC.Core;
using ioSenderTouch.Views;

namespace ioSenderTouch
{
    
    public partial class HomeView : UserControl
    {
        private bool? initOK = null;
        private bool isBooted = false;
        private IInputElement focusedControl = null;
        private Controller Controller = null;
        private readonly GrblViewModel _model;
        private readonly GrblConfigView _grblSettingView;
        private readonly AppConfigView _grblAppSettings;
        private ProbingView _probeView;
        private readonly RenderControl _renderView;
        private readonly OffsetView _offsetView;
        private SDCardView _sdView;
        private ToolView _toolView;
        private readonly UtilityView _utilityView;


        public HomeView(GrblViewModel model)
        {
            _model = model;
            DataContext = _model;
            InitializeComponent();
            Grbl.GrblViewModel = _model;
            _renderView = new RenderControl(_model);
            _grblSettingView = new GrblConfigView(_model);
            _grblAppSettings = new AppConfigView(_model);
            _offsetView = new OffsetView(_model);
            _utilityView = new UtilityView(_model);
            FillBorder.Child = _renderView;
            AppConfig.Settings.SetupAndOpen(_model, Application.Current.Dispatcher);
            DRO.DROEnabledChanged += DRO_DROEnabledChanged;
            DataContextChanged += View_DataContextChanged;
            InitSystem();
        }

        private void View_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is GrblViewModel)
            {
                var model = (GrblViewModel)e.NewValue;
                model.PropertyChanged += OnDataContextPropertyChanged;
                DataContextChanged -= View_DataContextChanged;
            }
        }

        private void OnDataContextPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is GrblViewModel viewModel)
            {
                switch (e.PropertyName)
                {
                    case nameof(GrblViewModel.GrblState):
                        if (Controller != null && !Controller.ResetPending)
                        {
                            if (isBooted && initOK == false && viewModel.GrblState.State != GrblStates.Alarm)
                                Dispatcher.BeginInvoke(new System.Action(() => InitSystem()), DispatcherPriority.ApplicationIdle);
                        }
                        break;

                    case nameof(GrblViewModel.IsGCLock):

                        break;

                    case nameof(GrblViewModel.IsSleepMode):

                        break;

                    case nameof(GrblViewModel.IsJobRunning):

                        if (GrblInfo.ManualToolChange)
                            GrblCommand.ToolChange = viewModel.IsJobRunning ? "T{0}M6" : "M61Q{0}";
                        break;

                    case nameof(GrblViewModel.IsToolChanging):

                        break;

                    case nameof(GrblViewModel.Tool):
                        if (GrblInfo.ManualToolChange && viewModel.Tool != GrblConstants.NO_TOOL)
                            GrblWorkParameters.RemoveNoTool();
                        break;

                    case nameof(GrblViewModel.GrblReset):
                        if (viewModel.IsReady)
                        {
                            if (!Controller.ResetPending && viewModel.GrblReset)
                            {
                                initOK = null;
                                Dispatcher.BeginInvoke(new System.Action(() => Activate(true, ViewType.GRBL)), DispatcherPriority.ApplicationIdle);
                            }
                        }
                        break;

                    case nameof(GrblViewModel.ParserState):
                        if (!Controller.ResetPending && viewModel.GrblReset)
                        {

                            viewModel.GrblReset = false;
                        }
                        break;

                    case nameof(GrblViewModel.FileName):
                        string filename = viewModel.FileName;
                        MainWindow.ui.WindowTitle = filename;

                        if (string.IsNullOrEmpty(filename))
                            MainWindow.CloseFile();
                        else if (viewModel.IsSDCardJob)
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

                                GCodeSender.EnablePolling(false);
                                _renderView.Open(GCode.File.Tokens);
                                GCodeSender.EnablePolling(true);
                            }
                        }

                        break;
                }
            }
        }


        public ViewType ViewType { get { return ViewType.GRBL; } }

        public void Activate(bool activate, ViewType chgMode)
        {
            if (activate)
            {
                GCodeSender.RewindFile();
                GCodeSender.CallHandler(GCode.File.IsLoaded ? StreamingState.Idle : (_model.IsSDCardJob ? StreamingState.Start : StreamingState.NoFile), false);

                _model.ResponseLogFilterOk = AppConfig.Settings.Base.FilterOkResponse;

                if (Controller == null)
                    Controller = new Controller(_model);

                if (initOK != true)
                {
                    focusedControl = this;

                    switch (Controller.Restart())
                    {
                        case Controller.RestartResult.Ok:
                            if (!isBooted)
                                Dispatcher.BeginInvoke(new System.Action(() => OnBooted()), DispatcherPriority.ApplicationIdle);
                            initOK = InitSystem();
                            break;

                        case Controller.RestartResult.Close:
                            MainWindow.ui.Close();
                            break;

                        case Controller.RestartResult.Exit:
                            Environment.Exit(-1);
                            break;
                    }

                    _model.Message = Controller.Message;
                }

                if (initOK == null)
                    initOK = false;


                if (GCode.File.IsLoaded)
                    MainWindow.ui.WindowTitle = ((GrblViewModel)DataContext).FileName;

                _model.Keyboard.JogStepDistance = AppConfig.Settings.Jog.LinkStepJogToUI ? AppConfig.Settings.JogUiMetric.Distance0 : AppConfig.Settings.Jog.StepDistance;
                _model.Keyboard.JogDistances[(int)KeypressHandler.JogMode.Slow] = AppConfig.Settings.Jog.SlowDistance;
                _model.Keyboard.JogDistances[(int)KeypressHandler.JogMode.Fast] = AppConfig.Settings.Jog.FastDistance;
                _model.Keyboard.JogFeedrates[(int)KeypressHandler.JogMode.Step] = AppConfig.Settings.Jog.StepFeedrate;
                _model.Keyboard.JogFeedrates[(int)KeypressHandler.JogMode.Slow] = AppConfig.Settings.Jog.SlowFeedrate;
                _model.Keyboard.JogFeedrates[(int)KeypressHandler.JogMode.Fast] = AppConfig.Settings.Jog.FastFeedrate;

                _model.Keyboard.IsJoggingEnabled = AppConfig.Settings.Jog.Mode != JogConfig.JogMode.UI;

                if (!GrblInfo.IsGrblHAL)
                    _model.Keyboard.IsContinuousJoggingEnabled = AppConfig.Settings.Jog.KeyboardEnable;
            }
            else if (ViewType != ViewType.Shutdown)
            {
                DRO.IsFocusable = false;

                focusedControl = focusedControl = AppConfig.Settings.Base.KeepMdiFocus &&
                                  Keyboard.FocusedElement is TextBox &&
                                   (Keyboard.FocusedElement as TextBox).Tag is string &&
                                    (string)(Keyboard.FocusedElement as TextBox).Tag == "MDI"
                                  ? Keyboard.FocusedElement
                                  : this;
            }

            if (GCodeSender.Activate(activate))
            {

                Task.Delay(500).ContinueWith(t => DRO.EnableFocus());
                Application.Current.Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    focusedControl.Focus();
                }), DispatcherPriority.Render);
            }
        }

        public void CloseFile()
        {
            _renderView.Close();
        }

        private void OnBooted()
        {
            isBooted = true;
            string filename = CNC.Core.Resources.Path + string.Format("KeyMap{0}.xml", (int)AppConfig.Settings.Jog.Mode);

            if (System.IO.File.Exists(filename))
                _model.Keyboard.LoadMappings(filename);

            if (GrblInfo.NumAxes > 3)
                GCode.File.AddTransformer(typeof(GCodeWrapViewModel), "Wrap to rotary (WIP)", MainWindow.UIViewModel.TransformMenuItems);
        }

        private bool InitSystem()
        {
            initOK = true;
            int timeout = 5;

            using (new UIUtils.WaitCursor())
            {
                GCodeSender.EnablePolling(false);
                while (!GrblInfo.Get())
                {
                    if (--timeout == 0)
                    {
                        _model.Message = (string)FindResource("MsgNoResponse");
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
                GCodeSender.EnablePolling(true);
            }

            GrblCommand.ToolChange = GrblInfo.ManualToolChange ? "M61Q{0}" : "T{0}";

            if (AppConfig.Settings.Jog.Mode == JogConfig.JogMode.Keypad)
            {
                jogControl.Visibility = Visibility.Hidden;
                //joggerRow.MaxHeight = 0;
            }



            if (_model.HasSDCard)
            {
                _sdView = new SDCardView(_model);
            }

            if (_model.HasATC)
            {
                _toolView = new ToolView(_model);
            }

            if (GrblInfo.HasProbe && GrblSettings.ReportProbeCoordinates)
            {
                _model.HasProbing = true;
                _probeView = new ProbingView(_model);
                _probeView.Activate(true, ViewType.Probing);

            }
            return true;
        }



        #region UIevents

        private void JobView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
                GCodeSender.Focus();
        }

        void DRO_DROEnabledChanged(bool enabled)
        {
            if (!enabled)
                Focus();
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (!(e.Handled = ProcessKeyPreview(e)))
            {
                if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
                    Focus();

                base.OnPreviewKeyDown(e);
            }
        }
        protected override void OnPreviewKeyUp(KeyEventArgs e)
        {
            if (!(e.Handled = ProcessKeyPreview(e)))
                base.OnPreviewKeyDown(e);
        }

        protected bool ProcessKeyPreview(KeyEventArgs e)
        {
            return _model.Keyboard.ProcessKeypress(e, !(MdiControl.IsFocused || DRO.IsFocused || spindleControl.IsFocused || workParametersControl.IsFocused));
        }

        #endregion
        private void Button_ClickSDView(object sender, RoutedEventArgs e)
        {
            FillBorder.Child = _sdView;
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            FillBorder.Child = _grblSettingView;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            FillBorder.Child = _probeView;

        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            FillBorder.Child = _renderView;
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            FillBorder.Child = _grblAppSettings;

        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            FillBorder.Child = _offsetView;
        }

        private void Button_Click_Utility(object sender, RoutedEventArgs e)
        {
            FillBorder.Child = _utilityView;
        }
        private void Button_Click_Tools(object sender, RoutedEventArgs e)
        {
            FillBorder.Child = _toolView;
        }

        public void ConfiguationLoaded(UIViewModel uiViewModel, AppConfig settings)
        {
            _grblAppSettings.Setup(uiViewModel, settings);
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            if (CodeListControl.Visibility == Visibility.Visible)
            {
                CodeListControl.Visibility = Visibility.Hidden;
                ConsoleControl.Visibility = Visibility.Visible;
            }
            else
            {
                CodeListControl.Visibility = Visibility.Visible;
                ConsoleControl.Visibility = Visibility.Hidden;
            }


            btnShowConsole.Content = ConsoleControl.Visibility == Visibility.Hidden ? "Console" : "GCode Viewer";
        }
    }
}
