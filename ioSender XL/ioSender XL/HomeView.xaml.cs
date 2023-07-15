/*
 * JobView.xaml.cs - part of Grbl Code Sender
 *
 * v0.39 / 2022-06-24 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2019-2022, Io Engineering (Terje Io)
All rights reserved.

Redistribution and use in source and binary forms, with or without modification,
are permitted provided that the following conditions are met:

· Redistributions of source code must retain the above copyright notice, this
list of conditions and the following disclaimer.

· Redistributions in binary form must reproduce the above copyright notice, this
list of conditions and the following disclaimer in the documentation and/or
other materials provided with the distribution.

· Neither the name of the copyright holder nor the names of its contributors may
be used to endorse or promote products derived from this software without
specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON
ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

*/

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Threading;
using CNC.Core;
using CNC.Controls;
using CNC.Controls.Probing;
using CNC.Controls.Viewer;

namespace GCode_Sender
{
    /// <summary>
    /// Interaction logic for JobView.xaml
    /// </summary>
    public partial class HomeView : UserControl, ICNCView
    {
        private bool? initOK = null;
        private bool isBooted = false;
        private IInputElement focusedControl = null;
        private Controller Controller = null;
        private readonly GrblViewModel _model;
        private readonly GrblConfigView _grblSettingView;
        private readonly AppConfigView _grblAppSettings;
        private  ProbingView _probeView;
        private readonly RenderControl _renderView;
        private readonly OffsetView _offsetView;
        private bool _hasProbing;

        public HomeView(GrblViewModel model)
        {
            InitializeComponent();
            _model = model;
            _renderView = new RenderControl(_model);
            _grblSettingView = new GrblConfigView();
            _grblAppSettings = new AppConfigView(model);
            _probeView = new ProbingView(model);
            _offsetView = new OffsetView(model);
            FillBorder.Child = _renderView;
            AppConfig.Settings.SetupAndOpen(model, Application.Current.Dispatcher);
            DataContext = _model;
            Grbl.GrblViewModel = _model;
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
                //          model.OnGrblReset += Model_OnGrblReset;
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
                        MainWindow.ui.JobRunning = viewModel.IsJobRunning;
                        //             MainWindow.EnableView(!(sender as GrblViewModel).IsGCLock, ViewType.Probing);
                        break;

                    case nameof(GrblViewModel.IsSleepMode):
                        EnableUI(!viewModel.IsSleepMode);
                        break;

                    case nameof(GrblViewModel.IsJobRunning):
                        MainWindow.ui.JobRunning = viewModel.IsJobRunning;
                        if (GrblInfo.ManualToolChange)
                            GrblCommand.ToolChange = viewModel.IsJobRunning ? "T{0}M6" : "M61Q{0}";
                        break;

                    case nameof(GrblViewModel.IsToolChanging):
                        MainWindow.ui.JobRunning = viewModel.IsToolChanging || viewModel.IsJobRunning;
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
                            EnableUI(true);
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
                            MainWindow.EnableView(false, ViewType.GCodeViewer);
                        }
                        else if (filename.StartsWith("Wizard:"))
                        {
                            if (MainWindow.IsViewVisible(ViewType.GCodeViewer))
                            {
                                MainWindow.EnableView(true, ViewType.GCodeViewer);
                                _renderView.Open(GCode.File.Tokens);
                            }
                        }
                        else if (!string.IsNullOrEmpty(filename) && AppConfig.Settings.GCodeViewer.IsEnabled)
                        {
                            MainWindow.GCodeViewer?.Open(GCode.File.Tokens);
                            MainWindow.EnableView(true, ViewType.GCodeViewer);

                            GCodeSender.EnablePolling(false);
                            _renderView.Open(GCode.File.Tokens);
                            GCodeSender.EnablePolling(true);
                        }
                        break;
                }
            }
        }

        private void RenderGCode()
        {
            _renderView.Open(GCode.File.Tokens);
        }

        #region Methods and properties required by CNCView interface

        public ViewType ViewType { get { return ViewType.GRBL; } }
        public bool CanEnable { get { return true; } }

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

#if ADD_CAMERA
                if (MainWindow.UIViewModel.Camera != null)
                {
                    MainWindow.UIViewModel.Camera.MoveOffset += Camera_MoveOffset;
                    MainWindow.UIViewModel.Camera.IsVisibilityChanged += Camera_Opened;
                    MainWindow.UIViewModel.Camera.IsMoveEnabled = true;
                }
#endif
                //if (viewer == null)
                //    viewer = new Viewer();

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
#if ADD_CAMERA
                if (MainWindow.UIViewModel.Camera != null)
                {
                    MainWindow.UIViewModel.Camera.MoveOffset -= Camera_MoveOffset;
                    MainWindow.UIViewModel.Camera.IsMoveEnabled = false;
                }
#endif
                focusedControl = focusedControl = AppConfig.Settings.Base.KeepMdiFocus &&
                                  Keyboard.FocusedElement is TextBox &&
                                   (Keyboard.FocusedElement as TextBox).Tag is string &&
                                    (string)(Keyboard.FocusedElement as TextBox).Tag == "MDI"
                                  ? Keyboard.FocusedElement
                                  : this;
            }

            if (GCodeSender.Activate(activate))
            {
                showProgramLimits();
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

        public void Setup(UIViewModel model, AppConfig profile)
        {
        }

        #endregion

        // https://stackoverflow.com/questions/5707143/how-to-get-the-width-height-of-a-collapsed-control-in-wpf
        private void showProgramLimits()
        {
            double height;

            //if (limitsControl.Visibility == Visibility.Collapsed)
            //{
            //    limitsControl.Visibility = Visibility.Hidden;
            //    limitsControl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            //    height = limitsControl.DesiredSize.Height;
            //    limitsControl.Visibility = Visibility.Collapsed;
            //}
            //else
            //    height = limitsControl.ActualHeight;

            //limitsControl.Visibility = (dp.ActualHeight - t1.ActualHeight - t2.ActualHeight + limitsControl.ActualHeight) > height ? Visibility.Visible : Visibility.Collapsed;
            //coolantControl.Visibility = rhGrid.ActualHeight > 600 ? Visibility.Visible : Visibility.Collapsed;
            //gotoControl.Visibility = rhGrid.ActualHeight > 575 ? Visibility.Visible : Visibility.Collapsed;
        }

#if ADD_CAMERA
        void Camera_Opened()
        {
            _model.IsCameraVisible = MainWindow.UIViewModel.Camera.IsVisible;
            Focus();
        }

        void Camera_MoveOffset(CameraMoveMode Mode, double XOffset, double YOffset)
        {
            GrblParserState.Get();
            CNC.GCode.Units units = GrblParserState.Units;
            CNC.GCode.DistanceMode distanceMode = GrblParserState.DistanceMode;

            Comms.com.WriteString("G91G0\r"); // Enter relative metric G0 mode - set scale to 1.0?

            switch (Mode)
            {
                case CameraMoveMode.XAxisFirst:
                    Comms.com.WriteString(string.Format("X{0}\r", XOffset.ToInvariantString("F3")));
                    Comms.com.WriteString(string.Format("Y{0}\r", YOffset.ToInvariantString("F3")));
                    break;

                case CameraMoveMode.YAxisFirst:
                    Comms.com.WriteString(string.Format("Y{0}\r", YOffset.ToInvariantString("F3")));
                    Comms.com.WriteString(string.Format("X{0}\r", XOffset.ToInvariantString("F3")));
                    break;

                case CameraMoveMode.BothAxes:
                    ((GrblViewModel)DataContext).ExecuteCommand(string.Format("X{0}Y{1}", XOffset.ToInvariantString("F3"), YOffset.ToInvariantString("F3")));
                    break;
            }

            if (distanceMode != CNC.GCode.DistanceMode.Incremental)
                Comms.com.WriteString("G90\r");

            if (units != CNC.GCode.Units.Metric)
                Comms.com.WriteString("G20\r");
        }
#endif

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

            showProgramLimits();

            if (!AppConfig.Settings.GCodeViewer.IsEnabled)
                //tabGCode.Items.Remove(tab3D);

                if (GrblInfo.LatheModeEnabled)
                {
                    MainWindow.EnableView(true, ViewType.Turning);
                    MainWindow.EnableView(true, ViewType.G76Threading);
                }
                else
                {
                    MainWindow.ShowView(false, ViewType.Turning);
                    MainWindow.ShowView(false, ViewType.Parting);
                    MainWindow.ShowView(false, ViewType.Facing);
                    MainWindow.ShowView(false, ViewType.G76Threading);
                }
           
            if (GrblInfo.HasSDCard)
                MainWindow.EnableView(true, ViewType.SDCard);
            else
                MainWindow.ShowView(false, ViewType.SDCard);

            if (GrblInfo.HasPIDLog)
                MainWindow.EnableView(true, ViewType.PIDTuner);
            else
                MainWindow.ShowView(false, ViewType.PIDTuner);

            if (GrblInfo.NumTools > 0)
                MainWindow.EnableView(true, ViewType.Tools);
            else
                MainWindow.ShowView(false, ViewType.Tools);

            if (GrblInfo.HasProbe && GrblSettings.ReportProbeCoordinates)
            {
                _probeView = new ProbingView(_model);
                _probeView.Activate(true, ViewType.Probing);

            }

            _model.HasProbing = GrblInfo.HasProbe;

            if (!string.IsNullOrEmpty(GrblInfo.TrinamicDrivers))
                MainWindow.EnableView(true, ViewType.TrinamicTuner);
            else
                MainWindow.ShowView(false, ViewType.TrinamicTuner);

            return true;
        }

    

        void EnableUI(bool enable)
        {
            //foreach (UserControl control in UIUtils.FindFirstLogicalChildren<UserControl>(this))
            //{
            //    if (control.Name != nameof(statusControl))
            //        control.IsEnabled = enable;
            //}
            // disable ui components when in sleep mode
        }
        #region UIevents

        void JobView_Load(object sender, EventArgs e)
        {
            GCodeSender.CallHandler(StreamingState.Idle, true);
        }

        private void JobView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
                GCodeSender.Focus();
        }

        private void JobView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (GrblInfo.IsLoaded)
                showProgramLimits();
        }

        private void outside_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Focus();
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

        public void ConfiguationLoaded(UIViewModel uiViewModel, AppConfig settings)
        {
            _grblAppSettings.Setup(uiViewModel,settings);
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
