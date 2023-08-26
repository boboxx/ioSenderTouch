/*
 * MainWindow.xaml.cs - part of Grbl Code Sender
 *
 * v0.41 / 2022-09-14 / Io Engineering (Terje Io)
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
using System.Windows.Threading;
using CNC.Controls;
using CNC.Converters;
using CNC.Core;
using ConfigControl = CNC.Controls.Probing.ConfigControl;
#if ADD_CAMERA

#endif

namespace ioSenderTouch
{

    public partial class MainWindow : Window
    {
        private const string Version = "1.0.0";
        private const string App_Name = "IO Sender Touch";
        public static MainWindow ui = null;
        public static UIViewModel UIViewModel { get; } = new UIViewModel();
        private bool saveWinSize = false;
        private readonly GrblViewModel _viewModel;
        private readonly HomeView _homeView;

        public string BaseWindowTitle { get; set; }
        public bool JobRunning => _viewModel.IsJobRunning;
        public MainWindow()
        {
            CNC.Core.Resources.Path = AppDomain.CurrentDomain.BaseDirectory;
            InitializeComponent();
            ui = this;
            Title = string.Format(Title, Version);
            int res;
            //if ((res = AppConfig.Settings.SetupAndOpen(Title, (GrblViewModel)DataContext, App.Current.Dispatcher)) != 0)
            //    Environment.Exit(res);
            // _viewModel  = new GrblViewModel();
            BaseWindowTitle = Title;
            _viewModel = DataContext as GrblViewModel;
            _homeView = new HomeView(_viewModel);
            DockPanel.SetDock(_homeView, Dock.Left);
            DockPanel.Children.Add(_homeView);
            new PipeServer(App.Current.Dispatcher);
            PipeServer.FileTransfer += Pipe_FileTransfer;
            VerisonLabel.Content = $"{App_Name} {Version}";
        }

        private void Window_Load(object sender, EventArgs e)
        {

            System.Threading.Thread.Sleep(50);
            Comms.com.PurgeQueue();
            if (!string.IsNullOrEmpty(AppConfig.Settings.FileName))
            {
                // Delay loading until app is ready
                Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new System.Action(() =>
                {
                    GCode.File.Load(AppConfig.Settings.FileName);
                }));
            }

            IGCodeConverter c = new Excellon2GCode();
            GCode.File.AddConverter(c.GetType(), c.FileType, c.FileExtensions);
            c = new HpglToGCode();
            GCode.File.AddConverter(c.GetType(), c.FileType, c.FileExtensions);
            GCode.File.AddTransformer(typeof(GCodeRotateViewModel), (string)FindResource("MenuRotate"), UIViewModel.TransformMenuItems);
            GCode.File.AddTransformer(typeof(ArcsToLines), (string)FindResource("MenuArcsToLines"), UIViewModel.TransformMenuItems);
            GCode.File.AddTransformer(typeof(GCodeCompress), (string)FindResource("MenuCompress"), UIViewModel.TransformMenuItems);
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Comms.com.DataReceived -= _viewModel.DataReceived;
            using (new UIUtils.WaitCursor())
            {
                Comms.com.Close(); // disconnecting from websocket may take some time...
                AppConfig.Settings.Shutdown();
            }
        }
        private void Pipe_FileTransfer(string filename)
        {
            if (!JobRunning)
                GCode.File.Load(filename);
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
