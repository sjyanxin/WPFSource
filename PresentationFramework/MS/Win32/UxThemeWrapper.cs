using System; 
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices; 
using System.Security;
using System.Windows; 
using System.Windows.Media; 
using System.Text;
using MS.Win32; 
using MS.Internal;

namespace MS.Win32
{ 
    /// <summary>
    ///     Wrapper class for loading UxTheme system theme data 
    /// </summary> 
    internal static class UxThemeWrapper
    { 
        static UxThemeWrapper()
        {
            _isActive = SafeNativeMethods.IsUxThemeActive();
        } 

        internal static bool IsActive 
        { 
            get
            { 
                return _isActive;
            }
        }
 
        internal static string ThemeName
        { 
            get 
            {
                if (IsActive) 
                {
                    if (_themeName == null)
                    {
                        EnsureThemeName(); 
                    }
 
                    return _themeName; 
                }
                else 
                {
                    return "classic";
                }
            } 
        }
 
        internal static string ThemeColor 
        {
            get 
            {
                Debug.Assert(IsActive, "Queried ThemeColor while UxTheme is not active.");

                if (_themeColor == null) 
                {
                    EnsureThemeName(); 
                } 

                return _themeColor; 
            }
        }

        ///<SecurityNote> 
        /// Critical - as this code performs an elevation to get current theme name
        /// TreatAsSafe - the "critical data" is transformed into "safe data" 
        ///                      all the info stored is the currrent theme name and current color - e.g. "Luna", "NormalColor" 
        ///                      Does not contain a path - considered safe.
        ///</SecurityNote> 
        [SecurityCritical, SecurityTreatAsSafe]
        private static void EnsureThemeName()
        {
            StringBuilder themeName = new StringBuilder(Win32.NativeMethods.MAX_PATH); 
            StringBuilder themeColor = new StringBuilder(Win32.NativeMethods.MAX_PATH);
 
            if (UnsafeNativeMethods.GetCurrentThemeName(themeName, themeName.Capacity, 
                                                        themeColor, themeColor.Capacity,
                                                        null, 0) == 0) 
            {
                // Success
                _themeName = themeName.ToString();
                _themeName = Path.GetFileNameWithoutExtension(_themeName); 
                _themeColor = themeColor.ToString();
            } 
            else 
            {
                // Failed to retrieve the name 
                _themeName = _themeColor = String.Empty;
            }
        }
 
        internal static void OnThemeChanged()
        { 
            _isActive = SafeNativeMethods.IsUxThemeActive(); 

            _themeName = null; 
            _themeColor = null;
        }

        private static bool _isActive; 
        private static string _themeName;
        private static string _themeColor; 
    } 
}
 


