using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CNC.Controls;
using CNC.Core;
using CNC.GCode;
using GCode_Sender.Commands;

namespace ioSenderTouch.ViewModels
{
    public class UtilityViewModel : INotifyPropertyChanged
    {
        private readonly GrblViewModel _grblViewModel;
        private UIElement _control;
        private readonly ErrorsAndAlarms _alarmAndError;
        private MacroEditor _macroEditor;
        public ICommand ShowView { get; }

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

        public UtilityViewModel(GrblViewModel grblViewModel)
        {
            _grblViewModel = grblViewModel;
            ShowView = new Command(SetView);
            _alarmAndError = new ErrorsAndAlarms("");
            _macroEditor = new MacroEditor();
        }

        private void SetView(object view)
        {
            var marco = AppConfig.Settings.Macros;
            switch (view.ToString())
            {
                case "Alarm":
                    Control = _alarmAndError;
                    break;
                case "Macro":
                    Control = _macroEditor;
                    break;
            }
          
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
