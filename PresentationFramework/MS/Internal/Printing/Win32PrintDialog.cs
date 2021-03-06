//  Microsoft Avalon 
//  Copyright (c) Microsoft Corporation, 2005
//
//  File:       Win32PrintDialog.cs
// 
//  05/24/2003 : mharper - created
// 
//------------------------------------------------------------------------------ 

using System; 
using System.Drawing.Printing;
using System.Printing.Interop;
using System.Printing;
using System.Runtime.InteropServices; 
using System.Security;
using System.Security.Permissions; 
using System.Windows.Controls; 

namespace MS.Internal.Printing 
{
    /// <summary>
    /// This entire class is implemented in this file.  However, the class
    /// is marked partial because this class utilizes/implements a marshaler 
    /// class that is private to it in another file.  The object is called
    /// PrintDlgExMarshaler and warranted its own file. 
    /// </summary> 
    internal partial class Win32PrintDialog
    { 
        #region Constructor

        /// <summary>
        /// Constructs an instance of the Win32PrintDialog.  This class is used for 
        /// displaying the Win32 PrintDlgEx dialog and obtaining a user selected
        /// printer and PrintTicket for a print operation. 
        /// </summary> 
        /// <SecurityNote>
        ///     Critical:    - Sets critical data. 
        ///     TreatAsSafe: - Sets the critical data to null for initialization.
        /// </SecurityNote>
        [SecurityCritical, SecurityTreatAsSafe]
        public 
        Win32PrintDialog()
        { 
            _printTicket = null; 
            _printQueue = null;
            _minPage = 1; 
            _maxPage = 9999;
            _pageRangeSelection = PageRangeSelection.AllPages;
        }
 
        #endregion Constructor
 
        #region Internal methods 

        /// <summary> 
        /// Displays a modal Win32 print dialog to allow the user to select the desired
        /// printer and set the printing options.  The data generated by this method
        /// can be accessed via the properties on the instance of this class.
        /// </summary> 
        /// <SecurityNote>
        ///     Critical:    - Create an instance of a critical class, calls critical 
        ///                    methods on that instance, and retrieves critical properties. 
        ///     TreatAsSafe: - This code simply displays a WIN32 based dialog, gets the
        ///                    print settings from the user, and saves the data away as 
        ///                    critical.  The "unsafe" data that does into the unmanaged
        ///                    are properties marked critical (PrintTicket and PrintQueue)
        ///                    on this class.  The other data that enters the unmanaged
        ///                    API are values that are integer based and uninteresting so 
        ///                    therefore are not critical (MinPage, MaxPage, and page range
        ///                    values.  Any data that is created by this method that is 
        ///                    considered unsafe is marked critical.  There are 2 properties 
        ///                    that are extracted from the unmanaged call that are not considered
        ///                    critical.  These are _pageRange and _pageRangeSelection.  We do not 
        ///                    care if these are exposed since the data means nothing except to
        ///                    the code that is doing the printing.
        /// </SecurityNote>
        [SecurityCritical, SecurityTreatAsSafe] 
        internal
        UInt32 
        ShowDialog() 
        {
            UInt32 result = NativeMethods.PD_RESULT_CANCEL; 

            //
            // Get the process main window handle
            // 
            IntPtr owner = IntPtr.Zero;
 
            if ((System.Windows.Application.Current != null) && 
                (System.Windows.Application.Current.MainWindow != null))
            { 
                System.Windows.Interop.WindowInteropHelper helper =
                    new System.Windows.Interop.WindowInteropHelper(System.Windows.Application.Current.MainWindow);
                owner = helper.CriticalHandle;
            } 

            try 
            { 
                if (this._printQueue == null || this._printTicket == null)
                { 
                    // Normally printDlgEx.SyncToStruct() probes the printer if both the print queue and print
                    // ticket are not null.
                    // If either is null we probe the printer ourselves
                    // If we dont end users will get notified that printing is disabled *after* 
                    // the print dialog has been displayed.
 
                    ProbeForPrintingSupport(); 
                }
 
                //
                // Create a PrintDlgEx instance to invoke the Win32 Print Dialog
                //
                using (PrintDlgExMarshaler printDlgEx = new PrintDlgExMarshaler(owner, this)) 
                {
                    printDlgEx.SyncToStruct(); 
 
                    //
                    // Display the Win32 print dialog 
                    //
                    Int32 hr = UnsafeNativeMethods.PrintDlgEx(printDlgEx.UnmanagedPrintDlgEx);
                    if (hr == MS.Win32.NativeMethods.S_OK)
                    { 
                        result = printDlgEx.SyncFromStruct();
                    } 
                } 
            }
            // 
            // NOTE:
            // This code was previously catch(PrintingNotSupportedException), but that created a circular dependency
            // between ReachFramework.dll and PresentationFramework.dll. Instead, we now catch Exception, check its full type name
            // and rethrow if it doesn't match. Not perfect, but better than having a circular dependency. 
            //
            catch(Exception e) 
            { 
                if (String.Equals(e.GetType().FullName, "System.Printing.PrintingNotSupportedException", StringComparison.Ordinal))
                { 
                    string message = System.Windows.SR.Get(System.Windows.SRID.PrintDialogInstallPrintSupportMessageBox);
                    string caption = System.Windows.SR.Get(System.Windows.SRID.PrintDialogInstallPrintSupportCaption);

                    bool isRtlCaption = caption != null && caption.Length > 0 && caption[0] == RightToLeftMark; 
                    System.Windows.MessageBoxOptions mbOptions = isRtlCaption ? System.Windows.MessageBoxOptions.RtlReading : System.Windows.MessageBoxOptions.None;
 
                    int type = 
                          (int) System.Windows.MessageBoxButton.OK
                        | (int) System.Windows.MessageBoxImage.Information 
                        | (int) mbOptions;

                    if (owner == IntPtr.Zero)
                    { 
                        owner = MS.Win32.UnsafeNativeMethods.GetActiveWindow();
                    } 
 
                    if(0 != MS.Win32.UnsafeNativeMethods.MessageBox(new HandleRef(null, owner), message, caption, type))
                    { 
                         result = NativeMethods.PD_RESULT_CANCEL;
                    }
                }
                else 
                {
                    // Not a PrintingNotSupportedException, rethrow 
                    throw; 
                }
            } 

            return result;
        }
 
        #endregion Internal methods
 
        #region Internal properties 

        /// <SecurityNote> 
        ///     Critical: Accesses critical data.
        /// </SecurityNote>
        internal PrintTicket PrintTicket
        { 
            [SecurityCritical]
            get 
            { 
                return _printTicket;
            } 
            [SecurityCritical]
            set
            {
                _printTicket = value; 
            }
        } 
 
        /// <SecurityNote>
        ///     Critical: Accesses critical data. 
        /// </SecurityNote>
        internal PrintQueue PrintQueue
        {
            [SecurityCritical] 
            get
            { 
                return _printQueue; 
            }
            [SecurityCritical] 
            set
            {
                _printQueue = value;
            } 
        }
 
        /// <summary> 
        /// Gets or sets the minimum page number allowed in the page ranges.
        /// </summary> 
        internal UInt32 MinPage
        {
            get
            { 
                return _minPage;
            } 
            set 
            {
                _minPage = value; 
            }
        }

        /// <summary> 
        /// Gets or sets the maximum page number allowed in the page ranges.
        /// </summary> 
        internal UInt32 MaxPage 
        {
            get 
            {
                return _maxPage;
            }
            set 
            {
                _maxPage = value; 
            } 
        }
 
        /// <summary>
        /// Gets or Sets the PageRangeSelection option for the print dialog.
        /// </summary>
        internal PageRangeSelection PageRangeSelection 
        {
            get 
            { 
                return _pageRangeSelection;
            } 
            set
            {
                _pageRangeSelection = value;
            } 
        }
 
        /// <summary> 
        /// Gets or sets a PageRange objects used when the PageRangeSelection
        /// option is set to UserPages. 
        /// </summary>
        internal PageRange PageRange
        {
            get 
            {
                return _pageRange; 
            } 
            set
            { 
                _pageRange = value;
            }
        }
 
        /// <summary>
        /// Gets or sets a flag to enable/disable the page range control on the dialog. 
        /// </summary> 
        internal bool PageRangeEnabled
        { 
            get
            {
                return _pageRangeEnabled;
            } 
            set
            { 
                _pageRangeEnabled = value; 
            }
        } 

        #endregion Internal properties

        #region Private methods 

        /// <summary> 
        /// Probe to see if printing support is installed 
        /// </summary>
        /// <SecurityNote> 
        /// Critical - Asserts DefaultPrinting permission in order to probe to see if a printer is available
        /// </SecurityNote>
        [SecurityCritical]
        private void ProbeForPrintingSupport() 
        {
            // Without a print queue object we have to make up a name for the printer. 
            // We will just ---- the print queue exception it generates later. 
            // We could avoid the exception if we had access to
            // MS.Internal.Printing.Configuration.NativeMethods.BindPTProviderThunk 

            string printerName = (this._printQueue != null) ? this._printQueue.FullName : string.Empty;

            (new PrintingPermission(PrintingPermissionLevel.DefaultPrinting)).Assert();  //BlessedAssert 
            try
            { 
                // If printer support is not installed this should throw a PrintingNotSupportedException 
                using (IDisposable converter = new PrintTicketConverter(printerName, 1))
                { 
                }
            }
            catch (PrintQueueException)
            { 
                // We can ---- print queue exceptions because they imply that printing
                // support is installed 
            } 
            finally
            { 
                PrintingPermission.RevertAssert();
            }
        }
 
        #endregion
 
        #region Private data 

        /// <SecurityNote> 
        ///     Critical: This is the print ticket used for printing
        ///               the current job.  It is critical because it
        ///               could contains some user sensitive data
        /// </SecurityNote> 
        [SecurityCritical]
        private 
        PrintTicket _printTicket; 

        /// <SecurityNote> 
        ///     Critical: This is an object that represents a print queue.
        ///               Any code that has access to this object has the
        ///               potential to print jobs to this printer so it is
        ///               a critical system resource. 
        /// </SecurityNote>
        [SecurityCritical] 
        private 
        PrintQueue _printQueue;
 
        private
        PageRangeSelection  _pageRangeSelection;

        private 
        PageRange           _pageRange;
 
        private 
        bool                _pageRangeEnabled;
 
        private
        UInt32              _minPage;

        private 
        UInt32              _maxPage;
 
        private 
        const char RightToLeftMark = '\u200F';
 
        #endregion Private data
    }
}

