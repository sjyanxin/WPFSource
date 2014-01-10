// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 
/*============================================================
** 
** Class: WeakReference 
**
** Purpose: A wrapper for establishing a WeakReference to an Object. 
**
===========================================================*/
namespace System {
 
    using System;
    using System.Runtime.Remoting; 
    using System.Runtime.Serialization; 
    using System.Security.Permissions;
    using System.Runtime.InteropServices; 
    using System.Threading;
    using System.Diagnostics.Contracts;
    [System.Runtime.InteropServices.ComVisible(true)]
    [SecurityPermissionAttribute(SecurityAction.InheritanceDemand, Flags=SecurityPermissionFlag.UnmanagedCode)] 
    [Serializable]
    public class WeakReference : ISerializable { 
        // Most methods using m_handle should use GC.KeepAlive(this) 
        // to avoid potential handle recycling attacks.  The GC special
        // cases the finalizer for WeakReference & only clears them 
        // when all threads are suspended, but you might still run into
        // problems if your code is at least partially interruptible.
        // It's just too much complexity to think about.
        internal volatile IntPtr m_handle; 
        internal bool m_IsLongReference;
 
#if FEATURE_CORECLR 
        // Migrating InheritanceDemands requires this default ctor, so we can mark it SafeCritical
        protected WeakReference() { 
            Contract.Assert(false, "WeakReferences protected default ctor should never be used!");
            throw new NotImplementedException();
        }
#endif 
        // Creates a new WeakReference that keeps track of target.
        // Assumes a Short Weak Reference (ie TrackResurrection is false.) 
        // 
        public WeakReference(Object target)
            : this(target, false) { 
        }

        //Creates a new WeakReference that keeps track of target.
        // 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public WeakReference(Object target, bool trackResurrection) { 
            m_IsLongReference=trackResurrection; 
            m_handle = GCHandle.InternalAlloc(target,
                            trackResurrection ? GCHandleType.WeakTrackResurrection : GCHandleType.Weak); 
        }


        [System.Security.SecuritySafeCritical]  // auto-generated 
        protected WeakReference(SerializationInfo info, StreamingContext context) {
            if (info==null) { 
                throw new ArgumentNullException("info"); 
            }
            Contract.EndContractBlock(); 
            Object temp = info.GetValue("TrackedObject",typeof(Object));
            m_IsLongReference = info.GetBoolean("TrackResurrection");
            m_handle = GCHandle.InternalAlloc(temp,
                                              m_IsLongReference ? GCHandleType.WeakTrackResurrection : GCHandleType.Weak); 
        }
 
        //Determines whether or not this instance of WeakReference still refers to an object 
        //that has not been collected.
        // 
        public virtual bool IsAlive {
            [System.Security.SecuritySafeCritical]  // auto-generated
            get {
                IntPtr h = m_handle; 

                // In determining whether it is valid to use this object, we need to at least expose this 
                // without throwing an exception. 
                if (IntPtr.Zero == h)
                    return false; 

                bool result = (GCHandle.InternalGet(h)!=null);

                // We want to ensure that if the target is live, then we will 
                // return it to the user. We need to keep this WeakReference object
                // live so m_handle doesn't get set to 0 or reused. 
                // Since m_handle is volatile, the following statement will 
                // guarantee the weakref object is live till the following
                // statement. 
                return (m_handle == IntPtr.Zero) ? false : result;
            }
        }
 
        //Returns a boolean indicating whether or not we're tracking objects until they're collected (true)
        //or just until they're finalized (false). 
        // 
        public virtual bool TrackResurrection {
            get { return m_IsLongReference; } 
        }

        //Gets the Object stored in the handle if it's accessible.
        // Or sets it. 
        //
        public virtual Object Target { 
            [System.Security.SecuritySafeCritical]  // auto-generated 
            get {
                IntPtr h = m_handle; 
                // Should only happen when used illegally, like using a
                // WeakReference from a finalizer.
                if (IntPtr.Zero == h)
                    return null; 

                Object o = GCHandle.InternalGet(h); 
 
                // We want to ensure that if the target is live, then we will
                // return it to the user. We need to keep this WeakReference object 
                // live so m_handle doesn't get set to 0 or reused.
                // Since m_handle is volatile, the following statement will
                // guarantee the weakref object is live till the following
                // statement. 
                return (m_handle == IntPtr.Zero) ? null : o;
            } 
            [System.Security.SecuritySafeCritical]  // auto-generated 
            set {
                IntPtr h = m_handle; 
                if (h == IntPtr.Zero)
                    throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_HandleIsNotInitialized"));

                // There is a ---- w/ finalization where m_handle gets set to 
                // NULL and the WeakReference becomes invalid.  Here we have to
                // do the following in order: 
                // 
                // 1.  Get the old object value
                // 2.  Get m_handle 
                // 3.  HndInterlockedCompareExchange(m_handle, newValue, oldValue);
                //
                // If the interlocked-cmp-exchange fails, then either we lost a ----
                // with another updater, or we lost a ---- w/ the finalizer.  In 
                // either case, we can just let the other guy win.
                Object oldValue = GCHandle.InternalGet(h); 
                h = m_handle; 
                if (h == IntPtr.Zero)
                    throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_HandleIsNotInitialized")); 
                GCHandle.InternalCompareExchange(h, value, oldValue, false /* isPinned */);

                // Ensure we don't have any handle recycling attacks in this
                // method where the finalizer frees the handle. 
                GC.KeepAlive(this);
            } 
        } 

        // Free all system resources associated with this reference. 
        //
        // Note: The WeakReference finalizer is not actually run, but
        // treated specially in gc.cpp's ScanForFinalization
        // This is needed for subclasses deriving from WeakReference, however. 
        #pragma warning disable 420
        [System.Security.SecuritySafeCritical]  // auto-generated 
        ~WeakReference() { 
            IntPtr old_handle = m_handle;
            if (old_handle != IntPtr.Zero) { 
                if (old_handle == Interlocked.CompareExchange(ref m_handle, IntPtr.Zero, old_handle))
                    GCHandle.InternalFree(old_handle);
            }
        } 

        [System.Security.SecurityCritical] 
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context) 
        {
            if (info==null) { 
                throw new ArgumentNullException("info");
            }
            Contract.EndContractBlock();
            info.AddValue("TrackedObject", Target, typeof(Object)); 
            info.AddValue("TrackResurrection", m_IsLongReference);
        } 
 
    }
 
}

