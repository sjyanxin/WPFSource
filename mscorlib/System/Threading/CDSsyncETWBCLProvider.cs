// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 
// =+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
// 
// CDSETWProvider.cs 
//
// <OWNER>[....]</OWNER> 
//
// A helper class for firing ETW events related to the Coordination Data Structure [....] primitives.
//
// This provider is used by CDS [....] primitives in both mscorlib.dll and system.dll. The purpose of sharing 
// the provider class is to be able to enable ETW tracing on all CDS [....] types with a single ETW provider GUID.
// 
// =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=- 
using System;
using System.Collections.Generic; 
using System.Text;

namespace System.Threading
{ 
#if !FEATURE_PAL    // PAL doesn't support  eventing
 
    using System.Diagnostics.Eventing; 

    [System.Runtime.CompilerServices.FriendAccessAllowed] 
    sealed internal class CdsSyncEtwBCLProvider : EventProviderBase
    {

        // 
        // Defines the singleton instance for the CDS [....] ETW provider
        // 
        // The CDS [....] Event provider GUID is {EC631D38-466B-4290-9306-834971BA0217} 
        //
        public static CdsSyncEtwBCLProvider Log = new CdsSyncEtwBCLProvider(); 
        private CdsSyncEtwBCLProvider() : base(new Guid(0xec631d38, 0x466b, 0x4290, 0x93, 0x6, 0x83, 0x49, 0x71, 0xba, 0x2, 0x17)) { }


        ///////////////////////////////////////////////////////////////////////////////////// 
        //
        // SpinLock Events 
        // 
        [Event(1, Level = EventLevel.LogAlways)]
        public void SpinLock_FastPathFailed(int ownerID) 
        {
            if (IsEnabled()) WriteEvent(1, ownerID);
        }
 
        /////////////////////////////////////////////////////////////////////////////////////
        // 
        // SpinWait Events 
        //
        [Event(2, Level = EventLevel.LogAlways)] 
        public void SpinWait_NextSpinWillYield()
        {
            if (IsEnabled()) WriteEvent(2);
        } 

 
        // 
        // Events below this point are used by the CDS types in System.DLL
        // 

        /////////////////////////////////////////////////////////////////////////////////////
        //
        // Barrier Events 
        //
        [Event(3, Level = EventLevel.Verbose)] 
        public void Barrier_PhaseFinished(bool currentSense, long phaseNum) 
        {
            if (IsEnabled(EventLevel.Verbose, ((EventKeywords)(-1)) )) WriteEvent(3, currentSense, phaseNum); 
        }

    }
#endif // !FEATURE_PAL 
}

