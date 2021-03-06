// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 
/*============================================================
** 
** Class:  ObjectHandle 
**
** 
** ObjectHandle wraps object references. A Handle allows a
** marshal by value object to be returned through an
** indirection allowing the caller to control when the
** object is loaded into their domain. 
**
** 
===========================================================*/ 

namespace System.Runtime.Remoting{ 

    using System;
    using System.Security.Permissions;
    using System.Runtime.InteropServices; 
    using System.Runtime.Remoting;
#if FEATURE_REMOTING 
    using System.Runtime.Remoting.Activation; 
    using System.Runtime.Remoting.Lifetime;
#endif 

// We duplicate this type because we need to inherit from MarshalByRefObject iff FEATURE_REMOTING is
// defined.  It would be nice in the future to just wrap the class definition in an ifdef but right
// now this causes problems with Thinner and causes it to generate invalid source. 
#if FEATURE_REMOTING
    [ClassInterface(ClassInterfaceType.AutoDual)] 
    [System.Runtime.InteropServices.ComVisible(true)] 
    public class ObjectHandle: MarshalByRefObject, IObjectHandle
    { 
        private Object WrappedObject;

        private ObjectHandle()
        { 
        }
 
        public ObjectHandle(Object o) 
        {
            WrappedObject = o; 
        }

        public Object Unwrap()
        { 
            return WrappedObject;
        } 
 
        // ObjectHandle has a finite lifetime. For now the default
        // lifetime is being used, this can be changed in this method to 
        // specify a custom lifetime.
#if FEATURE_REMOTING
        [System.Security.SecurityCritical]  // auto-generated_required
        public override Object InitializeLifetimeService() 
        {
            BCLDebug.Trace("REMOTE", "ObjectHandle.InitializeLifetimeService"); 
 
            //
            // If the wrapped object has implemented InitializeLifetimeService to return null, 
            // we don't want to go to the base class (which will result in a lease being
            // requested from the MarshalByRefObject, which starts up the LeaseManager,
            // which starts up the ThreadPool, adding three threads to the process.
            // We check if the wrapped object is a MarshalByRef object, and call InitializeLifetimeServices on it 
            // and if it returns null, we return null. Otherwise we fall back to the old behavior.
            // 
 
            MarshalByRefObject mbr = WrappedObject as MarshalByRefObject;
            if (mbr != null) { 
                Object o = mbr.InitializeLifetimeService();
                if (o == null)
                    return null;
            } 
            ILease lease = (ILease)base.InitializeLifetimeService();
            return lease; 
        } 
#endif // FEATURE_REMOTING
    } 
#else // !FEATURE_REMOTING
    [ClassInterface(ClassInterfaceType.AutoDual)]
    [System.Runtime.InteropServices.ComVisible(true)]
    public class ObjectHandle : IObjectHandle { 
        private Object WrappedObject;
 
        private ObjectHandle() { 
        }
 
        public ObjectHandle(Object o) {
            WrappedObject = o;
        }
 
        public Object Unwrap() {
            return WrappedObject; 
        } 

#if FEATURE_REMOTING 
        [System.Security.SecurityCritical]  // auto-generated_required
        public override Object InitializeLifetimeService()
        {
            BCLDebug.Trace("REMOTE", "ObjectHandle.InitializeLifetimeService"); 

            // 
            // If the wrapped object has implemented InitializeLifetimeService to return null, 
            // we don't want to go to the base class (which will result in a lease being
            // requested from the MarshalByRefObject, which starts up the LeaseManager, 
            // which starts up the ThreadPool, adding three threads to the process.
            // We check if the wrapped object is a MarshalByRef object, and call InitializeLifetimeServices on it
            // and if it returns null, we return null. Otherwise we fall back to the old behavior.
            // 

            MarshalByRefObject mbr = WrappedObject as MarshalByRefObject; 
            if (mbr != null) { 
                Object o = mbr.InitializeLifetimeService();
                if (o == null) 
                    return null;
            }
            ILease lease = (ILease)base.InitializeLifetimeService();
            return lease; 
        }
#endif // FEATURE_REMOTING 
    } 
#endif // !FEATURE_REMOTING
} 

