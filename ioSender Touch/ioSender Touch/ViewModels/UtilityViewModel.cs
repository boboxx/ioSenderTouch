using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CNC.Core;

namespace ioSenderTouch.ViewModels
{
    public class UtilityViewModel
    {
        private readonly GrblViewModel _grblViewModel;

        public UtilityViewModel(GrblViewModel grblViewModel)
        {
            _grblViewModel = grblViewModel;
        }

    }
}
