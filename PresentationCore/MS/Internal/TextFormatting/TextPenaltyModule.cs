//------------------------------------------------------------------------ 
//
//  Microsoft Windows Client Platform
//  Copyright (C) Microsoft Corporation
// 
//  File:      TextPenaltyModule.cs
// 
//  Contents:  Critical handle wrapping unmanaged text penalty module for 
//             penalty calculation of optimal paragraph vis PTS direct access.
// 
//  Spec:      http://team/sites/Avalon/Specs/Text%20Formatting%20API.doc
//
//  Created:   4-4-2006 Worachai Chaoweeraprasit (Wchao)
// 
//-----------------------------------------------------------------------
 
 
using System;
using System.Security; 
using System.Windows.Media;
using System.Windows.Media.TextFormatting;
using System.Runtime.InteropServices;
using MS.Internal.PresentationCore; 

using SR = MS.Internal.PresentationCore.SR; 
using SRID = MS.Internal.PresentationCore.SRID; 

 
namespace MS.Internal.TextFormatting
{
    /// <summary>
    /// Critical handle wrapper of unmanaged text penalty module. This class 
    /// is used exclusively by Framework thru friend-access. It provides direct
    /// access to the underlying dangerous handle to the unmanaged resource whose 
    /// lifetime is bound to the the underlying LS context. 
    /// </summary>
    [FriendAccessAllowed]   // used by Framework 
    internal sealed class TextPenaltyModule : IDisposable
    {
        private SecurityCriticalDataForSet<IntPtr>  _ploPenaltyModule;  // Pointer to LS penalty module
        private bool                                _isDisposed; 

 
        /// <summary> 
        /// This constructor is called by PInvoke when returning the critical handle
        /// </summary> 
        /// <SecurityNote>
        /// Critical - as this calls the setter of _ploPenaltyModule.
        /// Safe - as it does not set the value arbitrarily from the value it receives from caller.
        /// </SecurityNote> 
        [SecurityCritical, SecurityTreatAsSafe]
        internal TextPenaltyModule(SecurityCriticalDataForSet<IntPtr> ploc) 
        { 
            IntPtr ploPenaltyModule;
            LsErr lserr = UnsafeNativeMethods.LoAcquirePenaltyModule(ploc.Value, out ploPenaltyModule); 
            if (lserr != LsErr.None)
            {
                TextFormatterContext.ThrowExceptionFromLsError(SR.Get(SRID.AcquirePenaltyModuleFailure, lserr), lserr);
            } 

            _ploPenaltyModule.Value = ploPenaltyModule; 
        } 

 
        /// <summary>
        /// Finalize penalty module
        /// </summary>
        ~TextPenaltyModule() 
        {
            Dispose(false); 
        } 

 
        /// <summary>
        /// Explicitly clean up penalty module
        /// </summary>
        public void Dispose() 
        {
            Dispose(true); 
            GC.SuppressFinalize(this); 
        }
 

        /// <SecurityNote>
        /// Critical - as this calls method to dispose unmanaged penalty module.
        /// Safe - as it does not arbitrarily set critical data. 
        /// </SecurityNote>
        [SecurityCritical, SecurityTreatAsSafe] 
        private void Dispose(bool disposing) 
        {
            if (_ploPenaltyModule.Value != IntPtr.Zero) 
            {
                UnsafeNativeMethods.LoDisposePenaltyModule(_ploPenaltyModule.Value);
                _ploPenaltyModule.Value = IntPtr.Zero;
                _isDisposed = true; 
                GC.KeepAlive(this);
            } 
        } 

 
        /// <summary>
        /// This method should only be called by Framework to authorize direct access to
        /// unsafe LS penalty module for exclusive use of PTS during optimal paragraph
        /// penalty calculation. 
        /// </summary>
        /// <SecurityNote> 
        /// Critical - as this returns pointer to unmanaged memory owned by LS. 
        /// </SecurityNote>
        [SecurityCritical] 
        internal IntPtr DangerousGetHandle()
        {
            if (_isDisposed)
            { 
                throw new ObjectDisposedException(SR.Get(SRID.TextPenaltyModuleHasBeenDisposed));
            } 
 
            IntPtr penaltyModuleInternalHandle;
            LsErr lserr = UnsafeNativeMethods.LoGetPenaltyModuleInternalHandle(_ploPenaltyModule.Value, out penaltyModuleInternalHandle); 

            if (lserr != LsErr.None)
                TextFormatterContext.ThrowExceptionFromLsError(SR.Get(SRID.GetPenaltyModuleHandleFailure, lserr), lserr);
 
            GC.KeepAlive(this);
            return penaltyModuleInternalHandle; 
        } 
    }
} 


