using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Solvix.Client.Core.Interfaces
{
    public interface IThemeService
    {
        void SetTheme(AppTheme theme);
        AppTheme GetCurrentTheme();
        void LoadSavedTheme();
        void ApplyNeonGlow(bool enable);
    }
}
