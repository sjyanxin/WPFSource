// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 
/*============================================================
** 
** Class: SerializationFieldInfo 
**
** 
** Purpose: Provides a methods of representing imaginary fields
** which are unique to serialization.  In this case, what we're
** representing is the private members of parent classes.  We
** aggregate the RuntimeFieldInfo associated with this member 
** and return a managled form of the name.  The name that we
** return is .parentname.fieldname 
** 
**
============================================================*/ 

namespace System.Runtime.Serialization {

    using System; 
    using System.Reflection;
    using System.Globalization; 
    using System.Diagnostics.Contracts; 
    using System.Reflection.Cache;
    using System.Threading; 

    internal sealed class SerializationFieldInfo : FieldInfo {

        internal const String FakeNameSeparatorString = "+"; 

        private RuntimeFieldInfo m_field; 
        private String           m_serializationName; 

        public override Module Module { get { return m_field.Module; } } 
        public override int MetadataToken { get { return m_field.MetadataToken; } }

        internal SerializationFieldInfo(RuntimeFieldInfo field, String namePrefix) {
            Contract.Assert(field!=null,      "[SerializationFieldInfo.ctor]field!=null"); 
            Contract.Assert(namePrefix!=null, "[SerializationFieldInfo.ctor]namePrefix!=null");
 
            m_field = field; 
            m_serializationName = String.Concat(namePrefix, FakeNameSeparatorString, m_field.Name);
        } 

        //
        // MemberInfo methods
        // 
        public override String Name {
            get { 
                return m_serializationName; 
            }
        } 

        public override Type DeclaringType {
            get {
                return m_field.DeclaringType; 
            }
        } 
 
        public override Type ReflectedType {
            get { 
                return m_field.ReflectedType;
            }
        }
 
        public override Object[] GetCustomAttributes(bool inherit) {
            return m_field.GetCustomAttributes(inherit); 
        } 

        public override Object[] GetCustomAttributes(Type attributeType, bool inherit) { 
            return m_field.GetCustomAttributes(attributeType, inherit);
        }

        public override bool IsDefined(Type attributeType, bool inherit) { 
            return m_field.IsDefined(attributeType, inherit);
        } 
 
        //
        // FieldInfo methods 
        //
        public override Type FieldType {
            get {
                return m_field.FieldType; 
            }
        } 
 
        public override Object GetValue(Object obj) {
            return m_field.GetValue(obj); 
        }

        internal Object InternalGetValue(Object obj, bool requiresAccessCheck) {
            RtFieldInfo field = m_field as RtFieldInfo; 
            if (field != null)
                return field.InternalGetValue(obj, requiresAccessCheck); 
            else 
                return m_field.GetValue(obj);
        } 

        public override void SetValue(Object obj, Object value, BindingFlags invokeAttr, Binder binder, CultureInfo culture) {
            m_field.SetValue(obj, value, invokeAttr, binder, culture);
        } 

        [System.Security.SecurityCritical]  // auto-generated 
        internal void InternalSetValue(Object obj, Object value, BindingFlags invokeAttr, Binder binder, CultureInfo culture, bool requiresAccessCheck, bool isBinderDefault) { 
            //m_field.InternalSetValue(obj, value, invokeAttr, binder, culture, requiresAccessCheck, isBinderDefault);
            RtFieldInfo field = m_field as RtFieldInfo; 
            if (field != null)
                field.InternalSetValue(obj, value, invokeAttr, binder, culture, false);
            else
                m_field.SetValue(obj, value, invokeAttr, binder, culture); 
        }
 
        internal RuntimeFieldInfo FieldInfo { 
            get {
                return m_field; 
            }
        }

        public override RuntimeFieldHandle FieldHandle { 
            get {
                return m_field.FieldHandle; 
            } 
        }
 
        public override FieldAttributes Attributes {
            get {
                return m_field.Attributes;
            } 
        }
 
        #region Legacy Remoting Cache 
        private InternalCache m_cachedData;
 
        internal InternalCache RemotingCache
        {
            get
            { 
                // This grabs an internal copy of m_cachedData and uses
                // that instead of looking at m_cachedData directly because 
                // the cache may get cleared asynchronously.  This prevents 
                // us from having to take a lock.
                InternalCache cache = m_cachedData; 
                if (cache == null)
                {
                    cache = new InternalCache("MemberInfo");
                    InternalCache ret = Interlocked.CompareExchange(ref m_cachedData, cache, null); 
                    if (ret != null)
                        cache = ret; 
                    GC.ClearCache += new ClearCacheHandler(OnCacheClear); 
                }
                return cache; 
            }
        }

 
        internal void OnCacheClear(Object sender, ClearCacheEventArgs cacheEventArgs)
        { 
            m_cachedData = null; 
        }
        #endregion 
    }
}

