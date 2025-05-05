using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Solvix.Client.Core.Interfaces
{
    public interface INavigationService
    {
        Task NavigateToAsync(string route);
    }
}
