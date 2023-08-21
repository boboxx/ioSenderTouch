using System.Windows.Controls;
using CNC.Core;
using ioSenderTouch.ViewModels;

namespace ioSenderTouch.Controls
{
    /// <summary>
    /// Interaction logic for FacingView.xaml
    /// </summary>
    public partial class SurfacingView : UserControl
    {
        private readonly SurfacingViewModel _model;

        public SurfacingView(GrblViewModel grblViewModel)
        {
            _model = new SurfacingViewModel(grblViewModel);
          
            InitializeComponent();
            this.DataContext = _model;


        }
    }
}
