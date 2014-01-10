// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 

namespace System.Runtime { 
    using System; 
    using System.Runtime.CompilerServices;
    using System.Runtime.ConstrainedExecution; 
    using System.Security.Permissions;
    using System.Diagnostics.Contracts;

    // This is the same format as in clr\src\vm\gcpriv.h 
    // make sure you change that one if you change this one!
    [Serializable] 
    public enum GCLatencyMode 
    {
        Batch = 0, 
        Interactive = 1,
        LowLatency = 2
    }
 
    public static class GCSettings
    { 
        public static GCLatencyMode LatencyMode 
        {
            [System.Security.SecuritySafeCritical]  // auto-generated 
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            get
            {
                return (GCLatencyMode)(GC.GetGCLatencyMode()); 
            }
 
            // We don't want to allow this API when hosted. 
            [System.Security.SecurityCritical]  // auto-generated_required
            [HostProtection(MayLeakOnAbort = true)] 
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            set
            {
                if ((value < GCLatencyMode.Batch) || (value > GCLatencyMode.LowLatency)) 
                {
                    throw new ArgumentOutOfRangeException(Environment.GetResourceString("ArgumentOutOfRange_Enum")); 
                } 
                Contract.EndContractBlock();
 
                GC.SetGCLatencyMode((int)value);
            }
        }
 
        public static bool IsServerGC
        { 
            [System.Security.SecuritySafeCritical]  // auto-generated 
            get {
                return GC.IsServerGC(); 
            }
        }
    }
} 

