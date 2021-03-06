//------------------------------------------------------------------------------ 
//
// <copyright file="MediaContextNotificationWindow.cs" company="Microsoft">
//    Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright> 
//
// Description: 
//      A wrapper for a top-level hidden window that is used to process 
//      messages broadcasted to top-level windows only (such as DWM's
//      WM_DWMCOMPOSITIONCHANGED). If the WPF application doesn't have 
//      a top-level window (as it is the case for XBAP applications),
//      such messages would have been ignored.
//
//----------------------------------------------------------------------------- 

using System; 
using System.Windows.Threading; 

using System.Collections; 
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Media.Animation;
using System.Windows.Media.Composition; 
using Microsoft.Win32;
using Microsoft.Internal; 
using MS.Internal; 
using MS.Internal.Interop;
using MS.Win32; 
using System.Security;
using System.Security.Permissions;

using SR=MS.Internal.PresentationCore.SR; 
using SRID=MS.Internal.PresentationCore.SRID;
 
namespace System.Windows.Media 
{
    /// <summary> 
    /// The MediaContextNotificationWindow class provides its owner
    /// MediaContext with the ability to receive and forward window
    /// messages broadcasted to top-level windows.
    /// </summary> 
    internal class MediaContextNotificationWindow : IDisposable
    { 
        /// <summary> 
        /// Initializes static variables for this class.
        /// </summary> 
        /// <SecurityNote>
        /// Critical        - Sets the SecurityCritical static variables holding the message ids; calls RegisterWindowMessage.
        /// TreatAsSafe     - The message ids are not exposed; no external parameters are taken in.
        /// </SecurityNote> 
        [SecurityCritical, SecurityTreatAsSafe]
        static MediaContextNotificationWindow() 
        { 
            s_channelNotifyMessage = UnsafeNativeMethods.RegisterWindowMessage("MilChannelNotify");
            s_dwmRedirectionEnvironmentChanged = UnsafeNativeMethods.RegisterWindowMessage("DwmRedirectionEnvironmentChangedHint"); 
        }

        //+---------------------------------------------------------------------
        // 
        //  Internal Methods
        // 
        //--------------------------------------------------------------------- 

        #region Internal Methods 

        /// <summary>
        /// Sets the owner MediaContext and creates the notification window.
        /// </summary> 
        /// <SecurityNote>
        ///     Critical - Creates an HwndWrapper and adds a hook. 
        ///     TreatAsSafe: Critical data is not exposed. 
        /// </SecurityNote>
        [SecurityCritical, SecurityTreatAsSafe] 
        internal MediaContextNotificationWindow(MediaContext ownerMediaContext)
        {
            // Remember the pointer to the owner MediaContext that we'll forward the broadcasts to.
            _ownerMediaContext = ownerMediaContext; 

            // Create a top-level, invisible window so we can get the WM_DWMCOMPOSITIONCHANGED 
            // and other DWM notifications that are broadcasted to top-level windows only. 
            HwndWrapper hwndNotification;
            hwndNotification = new HwndWrapper(0, NativeMethods.WS_POPUP, 0, 0, 0, 0, 0, "MediaContextNotificationWindow", IntPtr.Zero, null); 

            _hwndNotificationHook = new HwndWrapperHook(MessageFilter);

            _hwndNotification = new SecurityCriticalDataClass<HwndWrapper>(hwndNotification); 
            _hwndNotification.Value.AddHook(_hwndNotificationHook);
 
            _isDisposed = false; 

            // 
            // On Vista, we need to know when the Magnifier goes on and off
            // in order to switch to and from software rendering because the
            // Vista Magnifier cannot magnify D3D content. To receive the
            // window message informing us of this, we must tell the DWM 
            // we are MIL content.
            // 
            // The Win7 Magnifier can magnify D3D content so it's not an 
            // issue there. In fact, Win7 doesn't even send the WM.
            // 
            // If the DWM is not running, this call will result in NoOp.
            //

            ChangeWindowMessageFilter(s_dwmRedirectionEnvironmentChanged, 1 /* MSGFLT_ADD */); 
            MS.Internal.HRESULT.Check(MilContent_AttachToHwnd(_hwndNotification.Value.Handle));
        } 
 
        ///<SecurityNote>
        ///     Critical - Calls dispose on the critical hwnd wrapper. 
        ///     TreatAsSafe: It is safe to dispose the wrapper
        ///</SecurityNote>
        [SecurityCritical, SecurityTreatAsSafe]
        public void Dispose() 
        {
            if (!_isDisposed) 
            { 
                //
                // If DWM is not running, this call will result in NoOp. 
                //
                MS.Internal.HRESULT.Check(MilContent_DetachFromHwnd(_hwndNotification.Value.Handle));

                _hwndNotification.Value.Dispose(); 

                _hwndNotificationHook = null; 
                _hwndNotification = null; 

                _ownerMediaContext = null; 

                _isDisposed = true;

                GC.SuppressFinalize(this); 
            }
        } 
 
        #endregion Internal Methods
 

        //+----------------------------------------------------------------------
        //
        //  Private Methods 
        //
        //--------------------------------------------------------------------- 
 
        #region Private Methods
 
        /// <summary>
        /// Tells a channel to send notifications to a particular target's window.
        /// </summary>
        /// <param name="channel"> 
        /// The channel from which we want notifications.
        /// </param> 
        /// <securitynote> 
        /// Critical        - Calls a critical channel method.
        /// TreatAsSafe     - We are associated with the window handle that we 
        ///                   are passing to the channel, so somebody already
        ///                   decided that it's OK for us to interact with that
        ///                   window. We also registered a window message so
        ///                   that we can avoid collisions with other messages. 
        /// </securitynote>
        [SecurityCritical, SecurityTreatAsSafe] 
        internal void SetAsChannelNotificationWindow() 
        {
            if (_isDisposed) 
            {
                throw new ObjectDisposedException("MediaContextNotificationWindow");
            }
 
            _ownerMediaContext.Channel.SetNotificationWindow(_hwndNotification.Value.Handle, s_channelNotifyMessage);
        } 
 
        /// <summary>
        /// If any of the interesting broadcast messages is seen, forward them to the owner MediaContext. 
        /// </summary>
        /// <SecurityNote>
        ///     Critical: Calls into unmanaged code, uses sensitive HWND data
        ///     TreatAsSafe: No sensitive information is disclosed. It's safe to "attach" the window to the DWM. 
        /// </SecurityNote>
        [SecurityCritical, SecurityTreatAsSafe] 
        private IntPtr MessageFilter(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) 
        {
            if (_isDisposed) 
            {
                throw new ObjectDisposedException("MediaContextNotificationWindow");
            }
 
            WindowMessage message = (WindowMessage)msg;
            Debug.Assert(_ownerMediaContext != null); 
 
            if (message == WindowMessage.WM_DWMCOMPOSITIONCHANGED)
            { 
                //
                // We need to register as MIL content to receive the Vista Magnifier messages
                // (see comments in ctor).
                // 
                // We're going to attempt to attach to DWM every time the desktop composition
                // state changes to ensure that we properly handle DWM crashing/restarting/etc. 
                // 
                MS.Internal.HRESULT.Check(MilContent_AttachToHwnd(_hwndNotification.Value.Handle));
            } 
            else if (message == s_channelNotifyMessage)
            {
                _ownerMediaContext.NotifyChannelMessage();
            } 
            else if (message == s_dwmRedirectionEnvironmentChanged)
            { 
                MediaSystem.NotifyRedirectionEnvironmentChanged(); 
            }
 
            return IntPtr.Zero;
        }

        /// <SecurityNote> 
        ///     Critical: This code causes unmanaged code elevation
        /// </SecurityNote> 
        [SecurityCritical, SuppressUnmanagedCodeSecurity] 
        [DllImport(DllImport.MilCore)]
        private static extern int MilContent_AttachToHwnd( 
            IntPtr hwnd
            );

        /// <SecurityNote> 
        ///     Critical: This code causes unmanaged code elevation
        /// </SecurityNote> 
        [SecurityCritical, SuppressUnmanagedCodeSecurity] 
        [DllImport(DllImport.MilCore)]
        private static extern int MilContent_DetachFromHwnd( 
            IntPtr hwnd
            );

        /// <summary> 
        /// Allow lower integrity applications to send specified window messages
        /// in case we are elevated. Failure is non-fatal and on down-level 
        /// platforms this call will result in a no-op. 
        /// </summary>
        /// <SecurityNote> 
        /// Critical -- Calls unsafe native methods GetModuleHandle and GetProcAddress.
        ///             Manually elevates unmanaged code permissions to pinvoke through
        ///             a function pointer.
        /// </SecurityNote> 
        [SecurityCritical]
        private void ChangeWindowMessageFilter(WindowMessage message, uint flag) 
        { 
            // Find the address of ChangeWindowMessageFilter in user32.dll.
            IntPtr user32Module = UnsafeNativeMethods.GetModuleHandle("user32.dll"); 

            // Get the address of the function. If this fails it means the OS
            // doesn't support this function, in which case we don't
            // need to do anything further. 
            IntPtr functionAddress = UnsafeNativeMethods.GetProcAddressNoThrow(
                    new HandleRef(null, user32Module), 
                    "ChangeWindowMessageFilter"); 

            if  (functionAddress != IntPtr.Zero) 
            {
                // Convert the function pointer into a callable delegate and then call it
                ChangeWindowMessageFilterNative function = Marshal.GetDelegateForFunctionPointer(
                    functionAddress, 
                    typeof(ChangeWindowMessageFilterNative)) as ChangeWindowMessageFilterNative;
 
                // In order to call the function we need unmanaged code access, 
                // because the function is native code.
                (new SecurityPermission(SecurityPermissionFlag.UnmanagedCode)).Assert(); 
                try
                {
                    function(message, flag);
                } 
                finally
                { 
                    SecurityPermission.RevertAssert(); 
                }
            } 
        }

        /// <summary>
        /// Prototype for user32's ChangeWindowMessageFilter function, which we load dynamically on Vista+. 
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] 
        private delegate void ChangeWindowMessageFilterNative(WindowMessage message, uint flag); 

        #endregion Private Methods 


        //+----------------------------------------------------------------------
        // 
        //  Private Fields
        // 
        //---------------------------------------------------------------------- 

        #region Private Fields 

        private bool _isDisposed;

        // The owner MediaContext 
        private MediaContext _ownerMediaContext;
 
        // A top-level hidden window. 
        private SecurityCriticalDataClass<HwndWrapper> _hwndNotification;
 
        // The message filter hook for the top-level hidden window.
        private HwndWrapperHook _hwndNotificationHook;

        // The window message used to announce a channel notification. 
        private static WindowMessage s_channelNotifyMessage;
 
        // We receive this when the Vista Magnifier goes on/off 
        private static WindowMessage s_dwmRedirectionEnvironmentChanged;
 
        #endregion Private Fields
    }
}
 

