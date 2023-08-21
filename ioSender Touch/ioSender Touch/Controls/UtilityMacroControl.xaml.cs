using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Mime;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using CNC.Controls;
using CNC.Core;
using CNC.Core.Comands;
using CNC.GCode;
using ioSenderTouch.ViewModels;

namespace ioSenderTouch.Controls
{
    /// <summary>
    /// Interaction logic for UtilityMacroControl.xaml
    /// </summary>
    public partial class UtilityMacroControl : UserControl
    {
        private GrblViewModel _model;
       
        public UtilityMacroControl()
        {
            InitializeComponent();
            Loaded += UtilityMacroControl_Loaded;
        }
        public ObservableCollection<Macro> Macros
        {
            get => (ObservableCollection<Macro>)GetValue(MacrosProp);
            set => SetValue(MacrosProp, value);
        }
        public static readonly DependencyProperty MacrosProp = DependencyProperty.Register(nameof(UtilityMacroControl.Macros), typeof(ObservableCollection<Macro>), typeof(UtilityMacroControl));
        private void UtilityMacroControl_Loaded(object sender, RoutedEventArgs e)
        {
            _model = DataContext as GrblViewModel;
            if (_model != null)
            {
                Macros = _model.UtilityMacros;
            }
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            var macro = Macros.FirstOrDefault(o =>
            {
                var tag = (sender as Button)?.Tag;
                return tag != null && o.Id == (int)tag;
            });
            if (macro != null && (!macro.ConfirmOnExecute || MessageBox.Show($"RunMacro{macro.Name}", "ioSender",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes))
                (DataContext as GrblViewModel)?.ExecuteMacro(macro.Code);
        }
    }
}
