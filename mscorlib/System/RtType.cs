// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 
//
// File: RtType.cs 
// 
// <OWNER>[....]</OWNER>
// 
// Implements System.RuntimeType
//
// ======================================================================================
 

using System; 
using System.Reflection; 
using System.Reflection.Cache;
using System.Runtime.ConstrainedExecution; 
using System.Globalization;
using System.Threading;
using System.Diagnostics;
using System.Security.Permissions; 
using System.Collections;
using System.Collections.Generic; 
using System.Runtime; 
using System.Runtime.Serialization;
using System.Runtime.CompilerServices; 
using System.Security;
using System.Text;
using System.Runtime.Remoting;
#if FEATURE_REMOTING 
using System.Runtime.Remoting.Proxies;
using System.Runtime.Remoting.Messaging; 
using System.Runtime.Remoting.Activation; 
#endif
using MdSigCallingConvention = System.Signature.MdSigCallingConvention; 
using RuntimeTypeCache = System.RuntimeType.RuntimeTypeCache;
using System.Runtime.InteropServices;
using DebuggerStepThroughAttribute = System.Diagnostics.DebuggerStepThroughAttribute;
using MdToken = System.Reflection.MetadataToken; 
using System.Runtime.Versioning;
using System.Diagnostics.Contracts; 
 
namespace System
{ 
    // this is a work around to get the concept of a calli. It's not as fast but it would be interesting to
    // see how it compares to the current implementation.
    // This delegate will disappear at some point in favor of calli
 
    internal delegate void CtorDelegate(Object instance);
 
    [Serializable] 
    internal class RuntimeType : Type, ISerializable, ICloneable
    { 
        #region Definitions

        [Serializable]
        internal class RuntimeTypeCache 
        {
            private const int MAXNAMELEN = 1024; 
 
            #region Definitions
            internal enum WhatsCached 
            {
                Nothing         = 0x0,
                EnclosingType   = 0x1,
            } 

            internal enum CacheType 
            { 
                Method,
                Constructor, 
                Field,
                Property,
                Event,
                Interface, 
                NestedType
            } 
 
            // This method serves two purposes, both enabling optimizations in ngen image
            // First, it tells ngen which instantiations of MemberInfoCache to save in the ngen image 
            // Second, it ensures MemberInfoCache<*Info>.Insert methods are pre-prepared in the
            // ngen image, and any runtime preparation that needs to happen, happens in the Prestub
            // worker of Prejitinit_Hack. At runtime we do not really want to execute the Insert methods
            // here; the calls are present just to fool the CER preparation code into preparing those methods during ngen 
            [System.Security.SecuritySafeCritical]  // auto-generated
            internal static void Prejitinit_HACK() 
            { 
                // make sure this conditional is around everything _including_ the call to
                // PrepareConstrainedRegions, the JIT/NGen just needs to see the callsite 
                // somewhere, but the callsite has a link demand which can trip up partially
                // trusted code.
                if (!s_dontrunhack)
                { 
                    RuntimeHelpers.PrepareConstrainedRegions();
                    try 
                    { } 
                    finally
                    { 
                            MemberInfoCache<RuntimeMethodInfo> micrmi = new MemberInfoCache<RuntimeMethodInfo>(null);
                            CerArrayList<RuntimeMethodInfo> list =null;
                            micrmi.Insert(ref list, "dummy", MemberListType.All);
 
                            MemberInfoCache<RuntimeConstructorInfo> micrci = new MemberInfoCache<RuntimeConstructorInfo>(null);
                            CerArrayList<RuntimeConstructorInfo> listc =null; 
                            micrci.Insert(ref listc, "dummy", MemberListType.All); 

                            MemberInfoCache<RuntimeFieldInfo> micrfi = new MemberInfoCache<RuntimeFieldInfo>(null); 
                            CerArrayList<RuntimeFieldInfo> listf =null;
                            micrfi.Insert(ref listf, "dummy", MemberListType.All);

                            MemberInfoCache<RuntimeType> micri = new MemberInfoCache<RuntimeType>(null); 
                            CerArrayList<RuntimeType> listi =null;
                            micri.Insert(ref listi, "dummy", MemberListType.All); 
 
                            MemberInfoCache<RuntimePropertyInfo> micrpi = new MemberInfoCache<RuntimePropertyInfo>(null);
                            CerArrayList<RuntimePropertyInfo> listp =null; 
                            micrpi.Insert(ref listp, "dummy", MemberListType.All);

                            MemberInfoCache<RuntimeEventInfo> micrei = new MemberInfoCache<RuntimeEventInfo>(null);
                            CerArrayList<RuntimeEventInfo> liste =null; 
                            micrei.Insert(ref liste, "dummy", MemberListType.All);
                    } 
                } 
            }
 
            private struct Filter
            {
                private Utf8String m_name;
                private MemberListType m_listType; 
                private uint m_nameHash;
 
                [System.Security.SecurityCritical]  // auto-generated 
                public unsafe Filter(byte* pUtf8Name, int cUtf8Name, MemberListType listType)
                { 
                    this.m_name = new Utf8String((void*) pUtf8Name, cUtf8Name);
                    this.m_listType = listType;
                    this.m_nameHash = 0;
 
                    if (RequiresStringComparison())
                    { 
                        m_nameHash = m_name.HashCaseInsensitive(); 
                    }
                } 

                public bool Match(Utf8String name)
                {
                    bool retVal = true; 

                    if (m_listType == MemberListType.CaseSensitive) 
                        retVal = m_name.Equals(name); 
                    else if (m_listType == MemberListType.CaseInsensitive)
                        retVal = m_name.EqualsCaseInsensitive(name); 

                    // Currently the callers of UsesStringComparison assume that if it returns false
                    // then the match always succeeds and can be skipped.  Assert that this is maintained.
                    Contract.Assert(retVal || RequiresStringComparison()); 

                    return retVal; 
                } 

                // Does the current match type require a string comparison? 
                // If not, we know Match will always return true and the call can be skipped
                // If so, we know we can have a valid hash to check against from GetHashToMatch
#if !FEATURE_CORECLR
                [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")] 
#endif
                public bool RequiresStringComparison() 
                { 
                    return (m_listType == MemberListType.CaseSensitive) ||
                           (m_listType == MemberListType.CaseInsensitive); 
                }

                public uint GetHashToMatch()
                { 
                    Contract.Assert(RequiresStringComparison());
 
                    return m_nameHash; 
                }
            } 

            [Serializable]
            private class MemberInfoCache<T> where T : MemberInfo
            { 
                #region Static Members
 
                [System.Security.SecuritySafeCritical]  // auto-generated 
                static MemberInfoCache()
                { 
                    // We need to prepare some code in this class for reliable
                    // execution on a per-instantiation basis. A static class
                    // constructor is ideal for this since we only need to do
                    // this once per-instantiation. We can't go through the 
                    // normal approach using RuntimeHelpers.PrepareMethod since
                    // that would involve using reflection and we'd wind back up 
                    // here recursively. So we call through an fcall helper 
                    // instead. The fcall is on RuntimeType to avoid having an
                    // fcall entry for a nested class (a generic one at that). 
                    // I've no idea if that would work, but I'm pretty sure it
                    // wouldn't. Also we pass in our own base type since using
                    // the mscorlib binder doesn't work for nested types (I've
                    // tried that one). 
                    PrepareMemberInfoCache(typeof(MemberInfoCache<T>).TypeHandle);
                } 
 
                #endregion
 
                #region Private Data Members
                // MemberInfo caches
                private CerHashtable<string, CerArrayList<T>> m_csMemberInfos;
                private CerHashtable<string, CerArrayList<T>> m_cisMemberInfos; 
                private CerArrayList<T> m_root;
                private bool m_cacheComplete; 
 
                // This is the strong reference back to the cache
                private RuntimeTypeCache m_runtimeTypeCache; 
                #endregion

                #region Constructor
                [System.Security.SecuritySafeCritical]  // auto-generated 
#if !FEATURE_CORECLR
                [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")] 
#endif 
                internal MemberInfoCache(RuntimeTypeCache runtimeTypeCache)
                { 
#if MDA_SUPPORTED
                    Mda.MemberInfoCacheCreation();
#endif
                    m_runtimeTypeCache = runtimeTypeCache; 
                    m_cacheComplete = false;
                } 
 
                [System.Security.SecuritySafeCritical]  // auto-generated
                internal MethodBase AddMethod(RuntimeType declaringType, RuntimeMethodHandleInternal method, CacheType cacheType) 
                {
                    Object list = null;
                    MethodAttributes methodAttributes = RuntimeMethodHandle.GetAttributes(method);
                    bool isPublic = (methodAttributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public; 
                    bool isStatic = (methodAttributes & MethodAttributes.Static) != 0;
                    bool isInherited = declaringType != ReflectedType; 
                    BindingFlags bindingFlags = RuntimeType.FilterPreCalculate(isPublic, isInherited, isStatic); 
                    switch (cacheType)
                    { 
                        case CacheType.Method:
                            List<RuntimeMethodInfo> mlist = new List<RuntimeMethodInfo>(1);
                            mlist.Add(new RuntimeMethodInfo(method, declaringType, m_runtimeTypeCache, methodAttributes, bindingFlags, null));
                            list = mlist; 
                            break;
                        case CacheType.Constructor: 
                            List<RuntimeConstructorInfo> clist = new List<RuntimeConstructorInfo>(1); 
                            clist.Add(new RuntimeConstructorInfo(method, declaringType, m_runtimeTypeCache, methodAttributes, bindingFlags));
                            list = clist; 
                            break;
                    }

                    CerArrayList<T> cerList = new CerArrayList<T>((List<T>)list); 

                    Insert(ref cerList, null, MemberListType.HandleToInfo); 
 
                    return (MethodBase)(object)cerList[0];
                } 

                [System.Security.SecuritySafeCritical]  // auto-generated
                internal FieldInfo AddField(RuntimeFieldHandleInternal field)
                { 
                    // create the runtime field info
                    List<RuntimeFieldInfo> list = new List<RuntimeFieldInfo>(1); 
                    FieldAttributes fieldAttributes = RuntimeFieldHandle.GetAttributes(field); 
                    bool isPublic = (fieldAttributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Public;
                    bool isStatic = (fieldAttributes & FieldAttributes.Static) != 0; 
                    bool isInherited = RuntimeFieldHandle.GetApproxDeclaringType(field) != ReflectedType;
                    BindingFlags bindingFlags = RuntimeType.FilterPreCalculate(isPublic, isInherited, isStatic);
                    list.Add(new RtFieldInfo(field, ReflectedType, m_runtimeTypeCache, bindingFlags));
 
                    CerArrayList<T> cerList = new CerArrayList<T>((List<T>)(object)list);
                    Insert(ref cerList, null, MemberListType.HandleToInfo); 
 
                    return (FieldInfo)(object)cerList[0];
                } 

                [System.Security.SecuritySafeCritical]  // auto-generated
                private unsafe CerArrayList<T> Populate(string name, MemberListType listType, CacheType cacheType)
                { 
                    List<T> list = null;
 
                    if (name == null || name.Length == 0 || 
                        (cacheType == CacheType.Constructor && name.FirstChar != '.' && name.FirstChar != '*'))
                    { 
                        list = GetListByName(null, 0, null, 0, listType, cacheType);
                    }
                    else
                    { 
                        int cNameLen = name.Length;
                        fixed (char* pName = name) 
                        { 
                            int cUtf8Name = Encoding.UTF8.GetByteCount(pName, cNameLen);
                            // allocating on the stack is faster than allocating on the GC heap 
                            // but we surely don't want to cause a stack overflow
                            // no one should be looking for a member whose name is longer than 1024
                            if (cUtf8Name > MAXNAMELEN)
                            { 
                                fixed (byte* pUtf8Name = new byte[cUtf8Name])
                                { 
                                    list = GetListByName(pName, cNameLen, pUtf8Name, cUtf8Name, listType, cacheType); 
                                }
                            } 
                            else
                            {
                                byte* pUtf8Name = stackalloc byte[cUtf8Name];
                                list = GetListByName(pName, cNameLen, pUtf8Name, cUtf8Name, listType, cacheType); 
                            }
                        } 
                    } 

                    CerArrayList<T> cerList = new CerArrayList<T>(list); 

                    Insert(ref cerList, name, listType);

                    return cerList; 
                }
 
                [System.Security.SecurityCritical]  // auto-generated 
                private unsafe List<T> GetListByName(char* pName, int cNameLen, byte* pUtf8Name, int cUtf8Name, MemberListType listType, CacheType cacheType)
                { 
                    if (cNameLen != 0)
                        Encoding.UTF8.GetBytes(pName, cNameLen, pUtf8Name, cUtf8Name);

                    Filter filter = new Filter(pUtf8Name, cUtf8Name, listType); 
                    List<T> list = null;
 
                    switch (cacheType) 
                    {
                        case CacheType.Method: 
                            list = PopulateMethods(filter) as List<T>;
                            break;
                        case CacheType.Field:
                            list = PopulateFields(filter) as List<T>; 
                            break;
                        case CacheType.Constructor: 
                            list = PopulateConstructors(filter) as List<T>; 
                            break;
                        case CacheType.Property: 
                            list = PopulateProperties(filter) as List<T>;
                            break;
                        case CacheType.Event:
                            list = PopulateEvents(filter) as List<T>; 
                            break;
                        case CacheType.NestedType: 
                            list = PopulateNestedClasses(filter) as List<T>; 
                            break;
                        case CacheType.Interface: 
                            list = PopulateInterfaces(filter) as List<T>;
                            break;
                        default:
                            BCLDebug.Assert(true, "Invalid CacheType"); 
                            break;
                    } 
 
                    return list;
                } 

                // May replace the list with a new one if certain cache
                // lookups succeed.  Also, may modify the contents of the list
                // after merging these new data structures with cached ones. 
                [System.Security.SecuritySafeCritical]  // auto-generated
                [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)] 
                internal void Insert(ref CerArrayList<T> list, string name, MemberListType listType) 
                {
                    bool lockTaken = false; 
                    bool preallocationComplete = false;

                    RuntimeHelpers.PrepareConstrainedRegions();
                    try 
                    {
                        Monitor.Enter(this, ref lockTaken); 
 
                        if (listType == MemberListType.CaseSensitive)
                        { 
                            if (m_csMemberInfos == null)
                                m_csMemberInfos = new CerHashtable<string, CerArrayList<T>>();
                            else
                                m_csMemberInfos.Preallocate(1); 
                        }
                        else if (listType == MemberListType.CaseInsensitive) 
                        { 
                            if (m_cisMemberInfos == null)
                                m_cisMemberInfos = new CerHashtable<string, CerArrayList<T>>(); 
                            else
                                m_cisMemberInfos.Preallocate(1);
                        }
 
                        if (m_root == null)
                            m_root = new CerArrayList<T>(list.Count); 
                        else 
                            m_root.Preallocate(list.Count);
 
                        preallocationComplete = true;
                    }
                    finally
                    { 
                        try
                        { 
                            if (preallocationComplete) 
                            {
                                if (listType == MemberListType.CaseSensitive) 
                                {
                                    // Ensure we always return a list that has
                                    // been merged with the global list.
                                    CerArrayList<T> cachedList = m_csMemberInfos[name]; 
                                    if (cachedList == null)
                                    { 
                                        MergeWithGlobalList(list); 
                                        m_csMemberInfos[name] = list;
                                    } 
                                    else
                                        list = cachedList;
                                }
                                else if (listType == MemberListType.CaseInsensitive) 
                                {
                                    // Ensure we always return a list that has 
                                    // been merged with the global list. 
                                    CerArrayList<T> cachedList = m_cisMemberInfos[name];
                                    if (cachedList == null) 
                                    {
                                        MergeWithGlobalList(list);
                                        m_cisMemberInfos[name] = list;
                                    } 
                                    else
                                        list = cachedList; 
                                } 
                                else
                                { 
                                    MergeWithGlobalList(list);
                                }

                                if (listType == MemberListType.All) 
                                {
                                    m_cacheComplete = true; 
                                } 
                            }
                        } 
                        finally
                        {
                            if (lockTaken)
                            { 
                                Monitor.Exit(this);
                            } 
                        } 
                    }
                } 

                // Modifies the existing list.
                [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
                private void MergeWithGlobalList(CerArrayList<T> list) 
                {
                    int cachedCount = m_root.Count; 
 
                    for (int i = 0; i < list.Count; i++)
                    { 
                        T newMemberInfo = list[i];
                        T cachedMemberInfo = null;
                        bool foundInCache = false;
 
                        for (int j = 0; j < cachedCount; j++)
                        { 
                            cachedMemberInfo = m_root[j]; 

                            if (newMemberInfo.CacheEquals(cachedMemberInfo)) 
                            {
                                list.Replace(i, cachedMemberInfo);
                                foundInCache = true;
                                break; 
                            }
                        } 
 
                        if (!foundInCache)
                            m_root.Add(newMemberInfo); 
                    }
                }
                #endregion
 
                #region Population Logic
                [System.Security.SecuritySafeCritical]  // auto-generated 
                private unsafe List<RuntimeMethodInfo> PopulateMethods(Filter filter) 
                {
                    List<RuntimeMethodInfo> list = new List<RuntimeMethodInfo>(); 

                    RuntimeType declaringType = ReflectedType;
                    Contract.Assert(declaringType != null);
 
                    bool isInterface =
                        (RuntimeTypeHandle.GetAttributes(declaringType) & TypeAttributes.ClassSemanticsMask) 
                        == TypeAttributes.Interface; 

                    if (isInterface) 
                    {
                        #region IsInterface

                        foreach (RuntimeMethodHandleInternal methodHandle in RuntimeTypeHandle.GetIntroducedMethods(declaringType)) 
                        {
                            if (filter.RequiresStringComparison()) 
                            { 
                                if (!RuntimeMethodHandle.MatchesNameHash(methodHandle,filter.GetHashToMatch()))
                                { 
                                    Contract.Assert(!filter.Match(RuntimeMethodHandle.GetUtf8Name(methodHandle)));
                                    continue;
                                }
 
                                if (!filter.Match(RuntimeMethodHandle.GetUtf8Name(methodHandle)))
                                    continue; 
                            } 

                            #region Loop through all methods on the interface 
                            Contract.Assert(!methodHandle.IsNullHandle());
                            // Except for .ctor, .cctor, IL_STUB*, and static methods, all interface methods should be abstract, virtual, and non-RTSpecialName.
                            // Note that this assumption will become invalid when we add support for non-abstract or static methods on interfaces.
                            Contract.Assert( 
                                (RuntimeMethodHandle.GetAttributes(methodHandle) & (MethodAttributes.RTSpecialName | MethodAttributes.Abstract | MethodAttributes.Virtual)) == (MethodAttributes.Abstract | MethodAttributes.Virtual) ||
                                (RuntimeMethodHandle.GetAttributes(methodHandle) & MethodAttributes.Static) == MethodAttributes.Static || 
                                RuntimeMethodHandle.GetName(methodHandle).Equals(".ctor") || 
                                RuntimeMethodHandle.GetName(methodHandle).Equals(".cctor") ||
                                RuntimeMethodHandle.GetName(methodHandle).StartsWith("IL_STUB", StringComparison.Ordinal)); 

                            #region Calculate Binding Flags
                            MethodAttributes methodAttributes = RuntimeMethodHandle.GetAttributes(methodHandle);
                            bool isPublic = (methodAttributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public; 
                            bool isStatic = (methodAttributes & MethodAttributes.Static) != 0;
                            bool isInherited = false; 
                            BindingFlags bindingFlags = RuntimeType.FilterPreCalculate(isPublic, isInherited, isStatic); 
                            #endregion
 
                            if ((methodAttributes & MethodAttributes.RTSpecialName) != 0 || RuntimeMethodHandle.IsILStub(methodHandle))
                                continue;

                            // get the unboxing stub or instantiating stub if needed 
                            RuntimeMethodHandleInternal instantiatedHandle = RuntimeMethodHandle.GetStubIfNeeded(methodHandle, declaringType, null);
 
                            RuntimeMethodInfo runtimeMethodInfo = new RuntimeMethodInfo( 
                            instantiatedHandle, declaringType, m_runtimeTypeCache, methodAttributes, bindingFlags, null);
 
                            list.Add(runtimeMethodInfo);
                            #endregion
                        }
                        #endregion 
                    }
                    else 
                    { 
                        #region IsClass or GenericParameter
                        while(RuntimeTypeHandle.IsGenericVariable(declaringType)) 
                            declaringType = declaringType.GetBaseType();

                        bool* overrides = stackalloc bool[RuntimeTypeHandle.GetNumVirtuals(declaringType)];
                        bool isValueType = declaringType.IsValueType; 

                        do 
                        { 
                            int vtableSlots = RuntimeTypeHandle.GetNumVirtuals(declaringType);
 
                            foreach (RuntimeMethodHandleInternal methodHandle in RuntimeTypeHandle.GetIntroducedMethods(declaringType))
                            {
                                if (filter.RequiresStringComparison())
                                { 
                                    if (!RuntimeMethodHandle.MatchesNameHash(methodHandle, filter.GetHashToMatch()))
                                    { 
                                        Contract.Assert(!filter.Match(RuntimeMethodHandle.GetUtf8Name(methodHandle))); 
                                        continue;
                                    } 

                                    if (!filter.Match(RuntimeMethodHandle.GetUtf8Name(methodHandle)))
                                        continue;
                                } 

                                #region Loop through all methods on the current type 
                                Contract.Assert(!methodHandle.IsNullHandle()); 

                                MethodAttributes methodAttributes = RuntimeMethodHandle.GetAttributes(methodHandle); 
                                MethodAttributes methodAccess = methodAttributes & MethodAttributes.MemberAccessMask;

                                #region Continue if this is a constructor
                                Contract.Assert( 
                                    (RuntimeMethodHandle.GetAttributes(methodHandle) & MethodAttributes.RTSpecialName) == 0 ||
                                    RuntimeMethodHandle.GetName(methodHandle).Equals(".ctor") || 
                                    RuntimeMethodHandle.GetName(methodHandle).Equals(".cctor") || 
                                    RuntimeMethodHandle.GetName(methodHandle).StartsWith("IL_STUB", StringComparison.Ordinal));
 
                                if ((methodAttributes & MethodAttributes.RTSpecialName) != 0 || RuntimeMethodHandle.IsILStub(methodHandle))
                                    continue;
                                #endregion
 
                                #region Continue if this is a private declared on a base type
                                bool isVirtual = false; 
                                int methodSlot = 0; 
                                if ((methodAttributes & MethodAttributes.Virtual) != 0)
                                { 
                                    // only virtual if actually in the vtableslot range, but GetSlot will
                                    // assert if an EnC method, which can't be virtual, so narrow down first
                                    // before calling GetSlot
                                    methodSlot = RuntimeMethodHandle.GetSlot(methodHandle); 
                                    isVirtual = (methodSlot < vtableSlots);
                                } 
                                bool isPrivate = methodAccess == MethodAttributes.Private; 
                                bool isPrivateVirtual = isVirtual & isPrivate;
                                bool isInherited = declaringType != ReflectedType; 
                                if (isInherited && isPrivate && !isPrivateVirtual)
                                    continue;
                                #endregion
 
                                #region Continue if this is a virtual and is already overridden
                                if (isVirtual) 
                                { 
                                    Contract.Assert(
                                        (methodAttributes & MethodAttributes.Abstract) != 0 || 
                                        (methodAttributes & MethodAttributes.Virtual) != 0 ||
                                        RuntimeMethodHandle.GetDeclaringType(methodHandle) != declaringType);

                                    if (overrides[methodSlot] == true) 
                                        continue;
 
                                    overrides[methodSlot] = true; 
                                }
                                else if (isValueType) 
                                {
                                    if ((methodAttributes & (MethodAttributes.Virtual | MethodAttributes.Abstract)) != 0)
                                        continue;
                                } 
                                else
                                { 
                                    Contract.Assert((methodAttributes & (MethodAttributes.Virtual | MethodAttributes.Abstract)) == 0); 
                                }
                                #endregion 

                                #region Calculate Binding Flags
                                bool isPublic = methodAccess == MethodAttributes.Public;
                                bool isStatic = (methodAttributes & MethodAttributes.Static) != 0; 
                                BindingFlags bindingFlags = RuntimeType.FilterPreCalculate(isPublic, isInherited, isStatic);
                                #endregion 
 
                                // get the unboxing stub or instantiating stub if needed
                                RuntimeMethodHandleInternal instantiatedHandle = RuntimeMethodHandle.GetStubIfNeeded(methodHandle, declaringType, null); 

                                RuntimeMethodInfo runtimeMethodInfo = new RuntimeMethodInfo(
                                instantiatedHandle, declaringType, m_runtimeTypeCache, methodAttributes, bindingFlags, null);
 
                                list.Add(runtimeMethodInfo);
                                #endregion 
                            } 

                            declaringType = RuntimeTypeHandle.GetBaseType(declaringType); 
                        } while (declaringType != null);
                        #endregion
                    }
 
                    return list;
                } 
 
                [System.Security.SecuritySafeCritical]  // auto-generated
                private List<RuntimeConstructorInfo> PopulateConstructors(Filter filter) 
                {
                    List<RuntimeConstructorInfo> list = new List<RuntimeConstructorInfo>();

                    if (ReflectedType.IsGenericParameter) 
                    {
                        return list; 
                    } 

                    RuntimeType declaringType= ReflectedType; 

                    foreach (RuntimeMethodHandleInternal methodHandle in RuntimeTypeHandle.GetIntroducedMethods(declaringType))
                    {
                        if (filter.RequiresStringComparison()) 
                        {
                            if (!RuntimeMethodHandle.MatchesNameHash(methodHandle, filter.GetHashToMatch())) 
                            { 
                                Contract.Assert(!filter.Match(RuntimeMethodHandle.GetUtf8Name(methodHandle)));
                                continue; 
                            }

                            if (!filter.Match(RuntimeMethodHandle.GetUtf8Name(methodHandle)))
                                continue; 
                        }
 
                        MethodAttributes methodAttributes = RuntimeMethodHandle.GetAttributes(methodHandle); 

                        Contract.Assert(!methodHandle.IsNullHandle()); 

                        if ((methodAttributes & MethodAttributes.RTSpecialName) == 0)
                            continue;
 
                        if (RuntimeMethodHandle.IsILStub(methodHandle))
                            continue; 
 
                        // Constructors should not be virtual or abstract
                        Contract.Assert( 
                            (methodAttributes & MethodAttributes.Abstract) == 0 &&
                            (methodAttributes & MethodAttributes.Virtual) == 0);

                        #region Calculate Binding Flags 
                        bool isPublic = (methodAttributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public;
                        bool isStatic = (methodAttributes & MethodAttributes.Static) != 0; 
                        bool isInherited = false; 
                        BindingFlags bindingFlags = RuntimeType.FilterPreCalculate(isPublic, isInherited, isStatic);
                        #endregion 

                        // get the unboxing stub or instantiating stub if needed
                        RuntimeMethodHandleInternal instantiatedHandle = RuntimeMethodHandle.GetStubIfNeeded(methodHandle, declaringType, null);
 
                        RuntimeConstructorInfo runtimeConstructorInfo =
                        new RuntimeConstructorInfo(instantiatedHandle, ReflectedType, m_runtimeTypeCache, methodAttributes, bindingFlags); 
 
                        list.Add(runtimeConstructorInfo);
                    } 

                    return list;
                }
 
                [System.Security.SecuritySafeCritical]  // auto-generated
                private unsafe List<RuntimeFieldInfo> PopulateFields(Filter filter) 
                { 
                    List<RuntimeFieldInfo> list = new List<RuntimeFieldInfo>();
 
                    RuntimeType declaringType = ReflectedType;

                    #region Populate all static, instance and literal fields
                    while(RuntimeTypeHandle.IsGenericVariable(declaringType)) 
                        declaringType = declaringType.GetBaseType();
 
                    while(declaringType != null) 
                    {
                        PopulateRtFields(filter, declaringType, list); 

                        PopulateLiteralFields(filter, declaringType, list);

                        declaringType = RuntimeTypeHandle.GetBaseType(declaringType); 
                    }
                    #endregion 
 
                    #region Populate Literal Fields on Interfaces
                    if (ReflectedType.IsGenericParameter) 
                    {
                        Type[] interfaces = ReflectedType.BaseType.GetInterfaces();

                        for (int i = 0; i < interfaces.Length; i++) 
                        {
                            // Populate literal fields defined on any of the interfaces implemented by the declaring type 
                            PopulateLiteralFields(filter, (RuntimeType)interfaces[i], list); 
                            PopulateRtFields(filter, (RuntimeType)interfaces[i], list);
                        } 
                    }
                    else
                    {
                        Type[] interfaces = RuntimeTypeHandle.GetInterfaces(ReflectedType); 

                        if (interfaces != null) 
                        { 
                            for (int i = 0; i < interfaces.Length; i++)
                            { 
                                // Populate literal fields defined on any of the interfaces implemented by the declaring type
                                PopulateLiteralFields(filter, (RuntimeType)interfaces[i], list);
                                PopulateRtFields(filter, (RuntimeType)interfaces[i], list);
                            } 
                        }
                    } 
                    #endregion 

                    return list; 
                }

                [System.Security.SecuritySafeCritical]  // auto-generated
                private unsafe void PopulateRtFields(Filter filter, RuntimeType declaringType, List<RuntimeFieldInfo> list) 
                {
                    IntPtr* pResult = stackalloc IntPtr[64]; 
                    int count = 64; 

                    if (!RuntimeTypeHandle.GetFields(declaringType, pResult, &count)) 
                    {
                        fixed(IntPtr* pBigResult = new IntPtr[count])
                        {
                            RuntimeTypeHandle.GetFields(declaringType, pBigResult, &count); 
                            PopulateRtFields(filter, pBigResult, count, declaringType, list);
                        } 
                    } 
                    else if (count > 0)
                    { 
                        PopulateRtFields(filter, pResult, count, declaringType, list);
                    }
                }
 
                [System.Security.SecurityCritical]  // auto-generated
                private unsafe void PopulateRtFields(Filter filter, 
                    IntPtr* ppFieldHandles, int count, RuntimeType declaringType, List<RuntimeFieldInfo> list) 
                {
                    Contract.Requires(declaringType != null); 
                    Contract.Requires(ReflectedType != null);

                    bool needsStaticFieldForGeneric = RuntimeTypeHandle.HasInstantiation(declaringType) && !RuntimeTypeHandle.ContainsGenericVariables(declaringType);
                    bool isInherited = declaringType != ReflectedType; 

                    for(int i = 0; i < count; i ++) 
                    { 
                        RuntimeFieldHandleInternal runtimeFieldHandle = new RuntimeFieldHandleInternal(ppFieldHandles[i]);
 
                        if (filter.RequiresStringComparison())
                        {
                            if (!RuntimeFieldHandle.MatchesNameHash(runtimeFieldHandle, filter.GetHashToMatch()))
                            { 
                                Contract.Assert(!filter.Match(RuntimeFieldHandle.GetUtf8Name(runtimeFieldHandle)));
                                continue; 
                            } 

                            if (!filter.Match(RuntimeFieldHandle.GetUtf8Name(runtimeFieldHandle))) 
                                continue;
                        }

                        Contract.Assert(!runtimeFieldHandle.IsNullHandle()); 

                        FieldAttributes fieldAttributes = RuntimeFieldHandle.GetAttributes(runtimeFieldHandle); 
                        FieldAttributes fieldAccess = fieldAttributes & FieldAttributes.FieldAccessMask; 

                        if (isInherited) 
                        {
                            if (fieldAccess == FieldAttributes.Private)
                                continue;
                        } 

                        #region Calculate Binding Flags 
                        bool isPublic = fieldAccess == FieldAttributes.Public; 
                        bool isStatic = (fieldAttributes & FieldAttributes.Static) != 0;
                        BindingFlags bindingFlags = RuntimeType.FilterPreCalculate(isPublic, isInherited, isStatic); 
                        #endregion

                        // correct the FieldDesc if needed
                        if (needsStaticFieldForGeneric && isStatic) 
                            runtimeFieldHandle = RuntimeFieldHandle.GetStaticFieldForGenericType(runtimeFieldHandle, declaringType);
 
                        RuntimeFieldInfo runtimeFieldInfo = 
                            new RtFieldInfo(runtimeFieldHandle, declaringType, m_runtimeTypeCache, bindingFlags);
 
                        list.Add(runtimeFieldInfo);
                    }
                }
 
                [System.Security.SecuritySafeCritical]  // auto-generated
                private unsafe void PopulateLiteralFields(Filter filter, RuntimeType declaringType, List<RuntimeFieldInfo> list) 
                { 
                    Contract.Requires(declaringType != null);
                    Contract.Requires(ReflectedType != null); 

                    int tkDeclaringType = RuntimeTypeHandle.GetToken(declaringType);

                    // Our policy is that TypeDescs do not have metadata tokens 
                    if (MdToken.IsNullToken(tkDeclaringType))
                        return; 
 
                    MetadataImport scope = RuntimeTypeHandle.GetMetadataImport(declaringType);
                    int cFields = scope.EnumFieldsCount(tkDeclaringType); 
                    int* tkFields = stackalloc int[cFields];
                    scope.EnumFields(tkDeclaringType, tkFields, cFields);

                    for (int i = 0; i < cFields; i++) 
                    {
                        int tkField = tkFields[i]; 
                        Contract.Assert(MdToken.IsTokenOfType(tkField, MetadataTokenType.FieldDef)); 
                        Contract.Assert(!MdToken.IsNullToken(tkField));
 
                        FieldAttributes fieldAttributes;
                        scope.GetFieldDefProps(tkField, out fieldAttributes);

                        FieldAttributes fieldAccess = fieldAttributes & FieldAttributes.FieldAccessMask; 

                        if ((fieldAttributes & FieldAttributes.Literal) != 0) 
                        { 
                            bool isInherited = declaringType != ReflectedType;
                            if (isInherited) 
                            {
                                bool isPrivate = fieldAccess == FieldAttributes.Private;
                                if (isPrivate)
                                    continue; 
                            }
 
                            if (filter.RequiresStringComparison()) 
                            {
                                Utf8String name; 
                                name = scope.GetName(tkField);

                                if (!filter.Match(name))
                                    continue; 
                            }
 
                            #region Calculate Binding Flags 
                            bool isPublic = fieldAccess == FieldAttributes.Public;
                            bool isStatic = (fieldAttributes & FieldAttributes.Static) != 0; 
                            BindingFlags bindingFlags = RuntimeType.FilterPreCalculate(isPublic, isInherited, isStatic);
                            #endregion

                            RuntimeFieldInfo runtimeFieldInfo = 
                            new MdFieldInfo(tkField, fieldAttributes, declaringType.GetTypeHandleInternal(), m_runtimeTypeCache, bindingFlags);
 
                            list.Add(runtimeFieldInfo); 
                        }
                    } 
                }

                private static void AddElementTypes(Type template, IList<Type> types)
                { 
                    if (!template.HasElementType)
                        return; 
 
                    AddElementTypes(template.GetElementType(), types);
 
                    for (int i = 0; i < types.Count; i ++)
                    {
                        if (template.IsArray)
                        { 
                            if (template.IsSzArray)
                                types[i] = types[i].MakeArrayType(); 
                            else 
                                types[i] = types[i].MakeArrayType(template.GetArrayRank());
                        } 
                        else if (template.IsPointer)
                        {
                            types[i] = types[i].MakePointerType();
                        } 
                    }
                } 
 
                [System.Security.SecuritySafeCritical]  // auto-generated
                private List<RuntimeType> PopulateInterfaces(Filter filter) 
                {
                    List<RuntimeType> list = new List<RuntimeType>();

                    RuntimeType declaringType = ReflectedType; 

                    if (!RuntimeTypeHandle.IsGenericVariable(declaringType)) 
                    { 
                        Type[] ifaces = RuntimeTypeHandle.GetInterfaces(declaringType);
 
                        if (ifaces != null)
                        {
                            for (int i = 0; i < ifaces.Length; i++)
                            { 
                                RuntimeType interfaceType = (RuntimeType)ifaces[i];
 
                                if (filter.RequiresStringComparison()) 
                                {
                                    if (!filter.Match(RuntimeTypeHandle.GetUtf8Name(interfaceType))) 
                                        continue;
                                }

                                Contract.Assert(interfaceType.IsInterface); 
                                list.Add(interfaceType);
                            } 
                        } 

                        if (ReflectedType.IsSzArray) 
                        {
                            RuntimeType arrayType = (RuntimeType)ReflectedType.GetElementType();

                            if (!arrayType.IsPointer) 
                            {
                                RuntimeType iList = (RuntimeType)typeof(IList<>).MakeGenericType(arrayType); 
 
                                if (iList.IsAssignableFrom(ReflectedType))
                                { 
                                    if (filter.Match(RuntimeTypeHandle.GetUtf8Name(iList)))
                                        list.Add(iList);

                                    Type[] iFaces = iList.GetInterfaces(); 
                                    for(int j = 0; j < iFaces.Length; j++)
                                    { 
                                        RuntimeType iFace = (RuntimeType)iFaces[j]; 
                                        if (iFace.IsGenericType && filter.Match(RuntimeTypeHandle.GetUtf8Name(iFace)))
                                            list.Add(iFace); 
                                    }
                                }
                            }
                        } 
                    }
                    else 
                    { 
                        List<RuntimeType> al = new List<RuntimeType>();
 
                        // Get all constraints
                        Type[] constraints = declaringType.GetGenericParameterConstraints();

                        // Populate transitive closure of all interfaces in constraint set 
                        for (int i = 0; i < constraints.Length; i++)
                        { 
                            RuntimeType constraint = (RuntimeType)constraints[i]; 
                            if (constraint.IsInterface)
                                al.Add(constraint); 

                            Type[] temp = constraint.GetInterfaces();
                            for (int j = 0; j < temp.Length; j++)
                                al.Add(temp[j] as RuntimeType); 
                        }
 
                        // Remove duplicates 
                        Dictionary<RuntimeType, RuntimeType> ht = new Dictionary<RuntimeType, RuntimeType>();
                        for (int i = 0; i < al.Count; i++) 
                        {
                            RuntimeType constraint = al[i];
                            if (!ht.ContainsKey(constraint))
                                ht[constraint] = constraint; 
                        }
 
                        RuntimeType[] interfaces = new RuntimeType[ht.Values.Count]; 
                        ht.Values.CopyTo(interfaces, 0);
 
                        // Populate link-list
                        for (int i = 0; i < interfaces.Length; i++)
                        {
                            if (filter.RequiresStringComparison()) 
                            {
                                if (!filter.Match(RuntimeTypeHandle.GetUtf8Name(interfaces[i]))) 
                                    continue; 
                            }
 
                            list.Add(interfaces[i]);
                        }
                    }
 
                    return list;
                } 
 
                [System.Security.SecuritySafeCritical]  // auto-generated
                private unsafe List<RuntimeType> PopulateNestedClasses(Filter filter) 
                {
                    List<RuntimeType> list = new List<RuntimeType>();

                    RuntimeType declaringType = ReflectedType; 

                    while (RuntimeTypeHandle.IsGenericVariable(declaringType)) 
                    { 
                        declaringType = declaringType.GetBaseType();
                    } 

                    int tkEnclosingType = RuntimeTypeHandle.GetToken(declaringType);

                    // For example, TypeDescs do not have metadata tokens 
                    if (MdToken.IsNullToken(tkEnclosingType))
                        return list; 
 
                    RuntimeModule moduleHandle = RuntimeTypeHandle.GetModule(declaringType);
                    MetadataImport scope = ModuleHandle.GetMetadataImport(moduleHandle); 

                    int cNestedClasses = scope.EnumNestedTypesCount(tkEnclosingType);
                    int* tkNestedClasses = stackalloc int[cNestedClasses];
                    scope.EnumNestedTypes(tkEnclosingType, tkNestedClasses, cNestedClasses); 

                    for (int i = 0; i < cNestedClasses; i++) 
                    { 
                        RuntimeType nestedType = null;
 
                        try
                        {
                            nestedType = ModuleHandle.ResolveTypeHandleInternal(moduleHandle, tkNestedClasses[i], null, null);
                        } 
                        catch(System.TypeLoadException)
                        { 
                            // In a reflection emit scenario, we may have a token for a class which 
                            // has not been baked and hence cannot be loaded.
                            continue; 
                        }

                        if (filter.RequiresStringComparison())
                        { 
                            if (!filter.Match(RuntimeTypeHandle.GetUtf8Name(nestedType)))
                                continue; 
                        } 

                        list.Add(nestedType); 
                    }

                    return list;
                } 

                [System.Security.SecuritySafeCritical]  // auto-generated 
                private unsafe List<RuntimeEventInfo> PopulateEvents(Filter filter) 
                {
                    Contract.Requires(ReflectedType != null); 

                    Dictionary<String, RuntimeEventInfo> csEventInfos = new Dictionary<String, RuntimeEventInfo>();

                    RuntimeType declaringType = ReflectedType; 
                    List<RuntimeEventInfo> list = new List<RuntimeEventInfo>();
 
                    bool isInterface = (RuntimeTypeHandle.GetAttributes(declaringType) & TypeAttributes.ClassSemanticsMask) == TypeAttributes.Interface; 

                    if (!isInterface) 
                    {
                        while(RuntimeTypeHandle.IsGenericVariable(declaringType))
                            declaringType = declaringType.GetBaseType();
 
                        // Populate associates off of the class hierarchy
                        while(declaringType != null) 
                        { 
                            PopulateEvents(filter, declaringType, csEventInfos, list);
                            declaringType = RuntimeTypeHandle.GetBaseType(declaringType); 
                        }
                    }
                    else
                    { 
                        // Populate associates for this interface
                        PopulateEvents(filter, declaringType, csEventInfos, list); 
                    } 

                    return list; 
                }

                [System.Security.SecuritySafeCritical]  // auto-generated
                private unsafe void PopulateEvents( 
                    Filter filter, RuntimeType declaringType, Dictionary<String, RuntimeEventInfo> csEventInfos, List<RuntimeEventInfo> list)
                { 
                    int tkDeclaringType = RuntimeTypeHandle.GetToken(declaringType); 

                    // Arrays, Pointers, ByRef types and others generated only the fly by the RT do not have tokens. 
                    if (MdToken.IsNullToken(tkDeclaringType))
                        return;

                    MetadataImport scope = RuntimeTypeHandle.GetMetadataImport(declaringType); 
                    int cEvents = scope.EnumEventsCount(tkDeclaringType);
                    int* tkEvents = stackalloc int[cEvents]; 
                    scope.EnumEvents(tkDeclaringType, tkEvents, cEvents); 
                    PopulateEvents(filter, declaringType, scope, tkEvents, cEvents, csEventInfos, list);
                } 

                [System.Security.SecurityCritical]  // auto-generated
                private unsafe void PopulateEvents(Filter filter,
                    RuntimeType declaringType, MetadataImport scope, int* tkEvents, int cAssociates, Dictionary<String, RuntimeEventInfo> csEventInfos, List<RuntimeEventInfo> list) 
                {
                    for (int i = 0; i < cAssociates; i++) 
                    { 
                        int tkEvent = tkEvents[i];
                        bool isPrivate; 

                        Contract.Assert(!MdToken.IsNullToken(tkEvent));
                        Contract.Assert(MdToken.IsTokenOfType(tkEvent, MetadataTokenType.Event));
 
                        if (filter.RequiresStringComparison())
                        { 
                            Utf8String name; 
                            name = scope.GetName(tkEvent);
 
                            if (!filter.Match(name))
                                continue;
                        }
 
                        RuntimeEventInfo eventInfo = new RuntimeEventInfo(
                            tkEvent, declaringType, m_runtimeTypeCache, out isPrivate); 
 
                        #region Remove Inherited Privates
                        if (declaringType != m_runtimeTypeCache.GetRuntimeType() && isPrivate) 
                            continue;
                        #endregion

                        #region Remove Duplicates 
                        if (csEventInfos.GetValueOrDefault(eventInfo.Name) != null)
                            continue; 
 
                        csEventInfos[eventInfo.Name] = eventInfo;
                        #endregion 

                        list.Add(eventInfo);
                    }
                } 

                [System.Security.SecuritySafeCritical]  // auto-generated 
                private unsafe List<RuntimePropertyInfo> PopulateProperties(Filter filter) 
                {
                    Contract.Requires(ReflectedType != null); 

                    // m_csMemberInfos can be null at this point. It will be initialized when Insert
                    // is called in Populate after this returns.
 
                    RuntimeType declaringType = ReflectedType;
                    Contract.Assert(declaringType != null); 
 
                    List<RuntimePropertyInfo> list = new List<RuntimePropertyInfo>();
 
                    bool isInterface = (RuntimeTypeHandle.GetAttributes(declaringType) & TypeAttributes.ClassSemanticsMask) == TypeAttributes.Interface;

                    if (!isInterface)
                    { 
                        while(RuntimeTypeHandle.IsGenericVariable(declaringType))
                            declaringType = declaringType.GetBaseType(); 
 
                        Dictionary<String, List<RuntimePropertyInfo>> csPropertyInfos = new Dictionary<String, List<RuntimePropertyInfo>>();
 
                        // All elements automatically initialized to false.
                        bool[] usedSlots = new bool[RuntimeTypeHandle.GetNumVirtuals(declaringType)];

                        // Populate associates off of the class hierarchy 
                        do
                        { 
                            PopulateProperties(filter, declaringType, csPropertyInfos, usedSlots, list); 
                            declaringType = RuntimeTypeHandle.GetBaseType(declaringType);
                        } while (declaringType != null); 
                    }
                    else
                    {
                        // Populate associates for this interface 
                        PopulateProperties(filter, declaringType, null, null, list);
                    } 
 
                    return list;
                } 

                [System.Security.SecuritySafeCritical]  // auto-generated
                private unsafe void PopulateProperties(
                    Filter filter, 
                    RuntimeType declaringType,
                    Dictionary<String, List<RuntimePropertyInfo>> csPropertyInfos, 
                    bool[] usedSlots, 
                    List<RuntimePropertyInfo> list)
                { 
                    int tkDeclaringType = RuntimeTypeHandle.GetToken(declaringType);

                    // Arrays, Pointers, ByRef types and others generated only the fly by the RT do not have tokens.
                    if (MdToken.IsNullToken(tkDeclaringType)) 
                        return;
 
                    MetadataImport scope = RuntimeTypeHandle.GetMetadataImport(declaringType); 
                    int cProperties = scope.EnumPropertiesCount(tkDeclaringType);
                    int* tkProperties = stackalloc int[cProperties]; 
                    scope.EnumProperties(tkDeclaringType, tkProperties, cProperties);
                    PopulateProperties(filter, declaringType, tkProperties, cProperties, csPropertyInfos, usedSlots, list);
                }
 
                [System.Security.SecurityCritical]  // auto-generated
                private unsafe void PopulateProperties( 
                    Filter filter, 
                    RuntimeType declaringType,
                    int* tkProperties, 
                    int cProperties,
                    Dictionary<String, List<RuntimePropertyInfo>> csPropertyInfos,
                    bool[] usedSlots,
                    List<RuntimePropertyInfo> list) 
                {
                    RuntimeModule declaringModuleHandle = RuntimeTypeHandle.GetModule(declaringType); 
 
                    int numVirtuals = RuntimeTypeHandle.GetNumVirtuals(declaringType);
 
                    Contract.Assert((declaringType.IsInterface && usedSlots == null && csPropertyInfos == null) ||
                                    (!declaringType.IsInterface && usedSlots != null && csPropertyInfos != null && usedSlots.Length >= numVirtuals));

                    for (int i = 0; i < cProperties; i++) 
                    {
                        int tkProperty = tkProperties[i]; 
                        bool isPrivate; 

                        Contract.Assert(!MdToken.IsNullToken(tkProperty)); 
                        Contract.Assert(MdToken.IsTokenOfType(tkProperty, MetadataTokenType.Property));

                        if (filter.RequiresStringComparison())
                        { 
                            if (!ModuleHandle.ContainsPropertyMatchingHash(declaringModuleHandle, tkProperty, filter.GetHashToMatch()))
                            { 
                                Contract.Assert(!filter.Match(declaringType.GetRuntimeModule().MetadataImport.GetName(tkProperty))); 
                                continue;
                            } 

                            Utf8String name;
                            name = declaringType.GetRuntimeModule().MetadataImport.GetName(tkProperty);
 
                            if (!filter.Match(name))
                                continue; 
                        } 

                        RuntimePropertyInfo propertyInfo = 
                            new RuntimePropertyInfo(
                            tkProperty, declaringType, m_runtimeTypeCache, out isPrivate);

                        // If this is a class, not an interface 
                        if (usedSlots != null /* && csPropertyInfos != null */)
                        { 
                            #region Remove Privates 
                            if (declaringType != ReflectedType && isPrivate)
                                continue; 
                            #endregion

                            #region Duplicate check based on vtable slots
 
                            // The inheritance of properties are defined by the inheritance of their
                            // getters and setters. 
                            // A property on a base type is "overriden" by a property on a sub type 
                            // if the getter/setter of the latter occupies the same vtable slot as
                            // the getter/setter of the former. 

                            RuntimeMethodInfo getter = (RuntimeMethodInfo)propertyInfo.GetGetMethod();

                            if (getter != null) 
                            {
                                int getterSlot = RuntimeMethodHandle.GetSlot(getter); 
 
                                if (getterSlot < numVirtuals)
                                { 
                                    Contract.Assert(getter.IsVirtual);
                                    if (usedSlots[getterSlot] == true)
                                        continue;
                                    else 
                                        usedSlots[getterSlot] = true;
                                } 
                            } 
                            else
                            { 
                                // We only need to examine the setter if a getter doesn't exist.
                                // It is not logical for the getter to be virtual but not the setter.
                                RuntimeMethodInfo setter = (RuntimeMethodInfo)propertyInfo.GetSetMethod();
                                if (setter != null) 
                                {
                                    int setterSlot = RuntimeMethodHandle.GetSlot(setter); 
 
                                    if (setterSlot < numVirtuals)
                                    { 
                                        Contract.Assert(setter.IsVirtual);
                                        if (usedSlots[setterSlot] == true)
                                            continue;
                                        else 
                                            usedSlots[setterSlot] = true;
                                    } 
                                } 
                            }
                            #endregion 

                            #region Duplicate check based on name and signature
                            // For backward compatibility, even if the vtable slots don't match, we will still treat
                            // a property as duplicate if the names and signatures match. 
                            List<RuntimePropertyInfo> cache = csPropertyInfos.GetValueOrDefault(propertyInfo.Name);
 
                            if (cache == null) 
                            {
                                cache = new List<RuntimePropertyInfo>(1); 
                                csPropertyInfos[propertyInfo.Name] = cache;
                            }
                            else
                            { 
                                for (int j = 0; j < cache.Count; j++)
                                { 
                                    if (propertyInfo.EqualsSig(cache[j])) 
                                    {
                                        cache = null; 
                                        break;
                                    }
                                }
                            } 

                            if (cache == null) 
                                continue; 

                            cache.Add(propertyInfo); 
                            #endregion
                        }

                        list.Add(propertyInfo); 
                    }
                } 
                #endregion 

                #region NonPrivate Members 
                internal CerArrayList<T> GetMemberList(MemberListType listType, string name, CacheType cacheType)
                {
                    CerArrayList<T> list = null;
 
                    switch(listType)
                    { 
                        case MemberListType.CaseSensitive: 
                            if (m_csMemberInfos == null)
                            { 
                                return Populate(name, listType, cacheType);
                            }
                            else
                            { 
                                list = m_csMemberInfos[name];
 
                                if (list == null) 
                                    return Populate(name, listType, cacheType);
 
                                return list;
                            }

                        case MemberListType.All: 
                            if (m_cacheComplete)
                                return m_root; 
 
                            return Populate(null, listType, cacheType);
 
                        default:
                            if (m_cisMemberInfos == null)
                            {
                                return Populate(name, listType, cacheType); 
                            }
                            else 
                            { 
                                list = m_cisMemberInfos[name];
 
                                if (list == null)
                                    return Populate(name, listType, cacheType);

                                return list; 
                            }
                    } 
                } 

                internal RuntimeType ReflectedType 
                {
                    get
                    {
                        return m_runtimeTypeCache.GetRuntimeType(); 
                    }
                } 
                #endregion 
            }
            #endregion 

            #region Private Data Members
            private WhatsCached m_whatsCached;
            private RuntimeType m_runtimeType; 
            private RuntimeType m_enclosingType;
            private TypeCode m_typeCode; 
            private string m_name; 
            private string m_fullname;
            private string m_toString; 
            private string m_namespace;
            private bool m_isGlobal;
            private bool m_bIsDomainInitialized;
            private MemberInfoCache<RuntimeMethodInfo> m_methodInfoCache; 
            private MemberInfoCache<RuntimeConstructorInfo> m_constructorInfoCache;
            private MemberInfoCache<RuntimeFieldInfo> m_fieldInfoCache; 
            private MemberInfoCache<RuntimeType> m_interfaceCache; 
            private MemberInfoCache<RuntimeType> m_nestedClassesCache;
            private MemberInfoCache<RuntimePropertyInfo> m_propertyInfoCache; 
            private MemberInfoCache<RuntimeEventInfo> m_eventInfoCache;
            private static CerHashtable<RuntimeMethodInfo, RuntimeMethodInfo> s_methodInstantiations;
            private static bool s_dontrunhack = false;
            #endregion 

            #region Constructor 
            internal RuntimeTypeCache(RuntimeType runtimeType) 
            {
                m_typeCode = TypeCode.Empty; 
                m_runtimeType = runtimeType;
                m_isGlobal = RuntimeTypeHandle.GetModule(runtimeType).RuntimeType == runtimeType;
                s_dontrunhack = true;
                Prejitinit_HACK(); 
            }
            #endregion 
 
            #region Private Members
#if !FEATURE_CORECLR 
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif
            private string ConstructName(ref string name, bool nameSpace, bool fullinst, bool assembly)
            { 
                if (name == null)
                { 
                    name = new RuntimeTypeHandle(m_runtimeType).ConstructName(nameSpace, fullinst, assembly); 
                }
                return name; 
            }

            private CerArrayList<T> GetMemberList<T>(ref MemberInfoCache<T> m_cache, MemberListType listType, string name, CacheType cacheType)
                where T : MemberInfo 
            {
                MemberInfoCache<T> existingCache = GetMemberCache<T>(ref m_cache); 
                return existingCache.GetMemberList(listType, name, cacheType); 
            }
 
#if !FEATURE_CORECLR
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif
            private MemberInfoCache<T> GetMemberCache<T>(ref MemberInfoCache<T> m_cache) 
                where T : MemberInfo
            { 
                MemberInfoCache<T> existingCache = m_cache; 

                if (existingCache == null) 
                {
                    MemberInfoCache<T> newCache = new MemberInfoCache<T>(this);
                    existingCache = Interlocked.CompareExchange(ref m_cache, newCache, null);
                    if (existingCache == null) 
                        existingCache = newCache;
                } 
 
                return existingCache;
            } 
            #endregion

            #region Internal Members
            internal bool DomainInitialized 
            {
                get { return m_bIsDomainInitialized; } 
                set { m_bIsDomainInitialized = value; } 
            }
 
            internal string GetName()
            {
                return ConstructName(ref m_name, false, false, false);
            } 

            [System.Security.SecurityCritical]  // auto-generated 
            internal unsafe string GetNameSpace() 
            {
                // @Optimization - Use ConstructName to populate m_namespace 
                if (m_namespace == null)
                {
                    Type type = m_runtimeType;
                    type = type.GetRootElementType(); 

                    while (type.IsNested) 
                        type = type.DeclaringType; 

                    m_namespace = RuntimeTypeHandle.GetMetadataImport((RuntimeType)type).GetNamespace(type.MetadataToken).ToString(); 
                }

                return m_namespace;
            } 

            internal string GetToString() 
            { 
                return ConstructName(ref m_toString, true, false, false);
            } 

            internal string GetFullName()
            {
                if (!m_runtimeType.IsGenericTypeDefinition && m_runtimeType.ContainsGenericParameters) 
                    return null;
 
                return ConstructName(ref m_fullname, true, true, false); 
            }
 
            internal TypeCode TypeCode
            {
                get { return m_typeCode; }
                set { m_typeCode = value; } 
            }
 
            [System.Security.SecuritySafeCritical]  // auto-generated 
            internal unsafe RuntimeType GetEnclosingType()
            { 
                if ((m_whatsCached & WhatsCached.EnclosingType) == 0)
                {
                    m_enclosingType = RuntimeTypeHandle.GetDeclaringType(GetRuntimeType());
 
                    m_whatsCached |= WhatsCached.EnclosingType;
                } 
 
                return m_enclosingType;
            } 

            internal RuntimeType GetRuntimeType()
            {
                return m_runtimeType; 
            }
 
            internal bool IsGlobal 
            {
                [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)] 
                get { return m_isGlobal; }
            }
            internal RuntimeType RuntimeType { get { return m_runtimeType; } }
 
            internal void InvalidateCachedNestedType()
            { 
                m_nestedClassesCache = null; 
            }
            #endregion 

            #region Caches Accessors
            [System.Security.SecurityCritical]  // auto-generated
            internal MethodInfo GetGenericMethodInfo(RuntimeMethodHandleInternal genericMethod) 
            {
                if (s_methodInstantiations == null) 
                    Interlocked.CompareExchange(ref s_methodInstantiations, new CerHashtable<RuntimeMethodInfo, RuntimeMethodInfo>(), null); 

                CerHashtable<RuntimeMethodInfo, RuntimeMethodInfo> methodInstantiations = s_methodInstantiations; 

                LoaderAllocator la = (LoaderAllocator)RuntimeMethodHandle.GetLoaderAllocator(genericMethod);

                if (la != null) 
                {
                    if (la.m_methodInstantiations == null) 
                        Interlocked.CompareExchange(ref la.m_methodInstantiations, new CerHashtable<RuntimeMethodInfo, RuntimeMethodInfo>(), null); 

                    methodInstantiations = la.m_methodInstantiations; 
                }

                RuntimeMethodInfo rmi = new RuntimeMethodInfo(
                    genericMethod, RuntimeMethodHandle.GetDeclaringType(genericMethod), this, 
                    RuntimeMethodHandle.GetAttributes(genericMethod), (BindingFlags)(-1), la);
 
                RuntimeMethodInfo crmi = null; 

                crmi = methodInstantiations[rmi]; 
                if (crmi != null)
                    return crmi;

                bool lockTaken = false; 
                bool preallocationComplete = false;
                RuntimeHelpers.PrepareConstrainedRegions(); 
                try 
                {
                    Monitor.Enter(methodInstantiations, ref lockTaken); 

                    crmi = methodInstantiations[rmi];
                    if (crmi != null)
                        return crmi; 

                    methodInstantiations.Preallocate(1); 
 
                    preallocationComplete = true;
                } 
                finally
                {
                    if (preallocationComplete)
                    { 
                        methodInstantiations[rmi] = rmi;
                    } 
 
                    if (lockTaken)
                    { 
                        Monitor.Exit(methodInstantiations);
                    }
                }
 
                return rmi;
            } 
 
            internal CerArrayList<RuntimeMethodInfo> GetMethodList(MemberListType listType, string name)
            { 
                return GetMemberList<RuntimeMethodInfo>(ref m_methodInfoCache, listType, name, CacheType.Method);
            }

            internal CerArrayList<RuntimeConstructorInfo> GetConstructorList(MemberListType listType, string name) 
            {
                return GetMemberList<RuntimeConstructorInfo>(ref m_constructorInfoCache, listType, name, CacheType.Constructor); 
            } 

            internal CerArrayList<RuntimePropertyInfo> GetPropertyList(MemberListType listType, string name) 
            {
                return GetMemberList<RuntimePropertyInfo>(ref m_propertyInfoCache, listType, name, CacheType.Property);
            }
 
            internal CerArrayList<RuntimeEventInfo> GetEventList(MemberListType listType, string name)
            { 
                return GetMemberList<RuntimeEventInfo>(ref m_eventInfoCache, listType, name, CacheType.Event); 
            }
 
            internal CerArrayList<RuntimeFieldInfo> GetFieldList(MemberListType listType, string name)
            {
                return GetMemberList<RuntimeFieldInfo>(ref m_fieldInfoCache, listType, name, CacheType.Field);
            } 

            internal CerArrayList<RuntimeType> GetInterfaceList(MemberListType listType, string name) 
            { 
                return GetMemberList<RuntimeType>(ref m_interfaceCache, listType, name, CacheType.Interface);
            } 

            internal CerArrayList<RuntimeType> GetNestedTypeList(MemberListType listType, string name)
            {
                return GetMemberList<RuntimeType>(ref m_nestedClassesCache, listType, name, CacheType.NestedType); 
            }
 
            internal MethodBase GetMethod(RuntimeType declaringType, RuntimeMethodHandleInternal method) 
            {
                GetMemberCache<RuntimeMethodInfo>(ref m_methodInfoCache); 
                return m_methodInfoCache.AddMethod(declaringType, method, CacheType.Method);
            }

            internal MethodBase GetConstructor(RuntimeType declaringType, RuntimeMethodHandleInternal constructor) 
            {
                GetMemberCache<RuntimeConstructorInfo>(ref m_constructorInfoCache); 
                return m_constructorInfoCache.AddMethod(declaringType, constructor, CacheType.Constructor); 
            }
 
            internal FieldInfo GetField(RuntimeFieldHandleInternal field)
            {
                GetMemberCache<RuntimeFieldInfo>(ref m_fieldInfoCache);
                return m_fieldInfoCache.AddField(field); 
            }
 
            #endregion 
        }
        #endregion 

        #region Legacy Remoting Cache
        // The size of CachedData is accounted for by BaseObjectWithCachedData in object.h.
        // This member is currently being used by Remoting for caching remoting data. If you 
        // need to cache data here, talk to the Remoting team to work out a mechanism, so that
        // both caching systems can happily work together. 
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
 
        #region Static Members

        #region Internal
        internal static RuntimeType GetType(String typeName, bool throwOnError, bool ignoreCase, bool reflectionOnly, 
            ref StackCrawlMark stackMark)
        { 
            if (typeName == null) 
                throw new ArgumentNullException("typeName");
            Contract.EndContractBlock(); 

            return RuntimeTypeHandle.GetTypeByName(
                typeName, throwOnError, ignoreCase, reflectionOnly, ref stackMark, false);
        } 

        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)] 
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        [SuppressUnmanagedCodeSecurity] 
        static internal extern void PrepareMemberInfoCache(RuntimeTypeHandle rt);

        internal static MethodBase GetMethodBase(RuntimeModule scope, int typeMetadataToken)
        { 
            return GetMethodBase(ModuleHandle.ResolveMethodHandleInternal(scope, typeMetadataToken));
        } 
 
        internal static MethodBase GetMethodBase(IRuntimeMethodInfo methodHandle)
        { 
            return GetMethodBase(null, methodHandle);
        }

        [System.Security.SecuritySafeCritical] 
#if !FEATURE_CORECLR
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")] 
#endif 
        internal static MethodBase GetMethodBase(RuntimeType reflectedType, IRuntimeMethodInfo methodHandle)
        { 
            MethodBase retval = RuntimeType.GetMethodBase(reflectedType, methodHandle.Value);
            GC.KeepAlive(methodHandle);
            return retval;
        } 

        [System.Security.SecurityCritical]  // auto-generated 
        internal unsafe static MethodBase GetMethodBase(RuntimeType reflectedType, RuntimeMethodHandleInternal methodHandle) 
        {
            Contract.Assert(!methodHandle.IsNullHandle()); 

            if (RuntimeMethodHandle.IsDynamicMethod(methodHandle))
            {
                Resolver resolver = RuntimeMethodHandle.GetResolver(methodHandle); 

                if (resolver != null) 
                    return resolver.GetDynamicMethod(); 

                return null; 
            }

            // verify the type/method relationship
            RuntimeType declaredType = RuntimeMethodHandle.GetDeclaringType(methodHandle); 

            RuntimeType[] methodInstantiation = null; 
 
            if (reflectedType == null)
                reflectedType = declaredType as RuntimeType; 

            if (reflectedType != declaredType && !reflectedType.IsSubclassOf(declaredType))
            {
                // object[] is assignable from string[]. 
                if (reflectedType.IsArray)
                { 
                    // 

                    // The whole purpose of this chunk of code is not only for error checking. 
                    // GetMember has a side effect of populating the member cache of reflectedType,
                    // doing so will ensure we construct the correct MethodInfo/ConstructorInfo objects.
                    // Without this the reflectedType.Cache.GetMethod call below may return a MethodInfo
                    // object whose ReflectedType is string[] and DeclaringType is object[]. That would 
                    // be (arguabally) incorrect because string[] is not a subclass of object[].
                    MethodBase[] methodBases = reflectedType.GetMember( 
                        RuntimeMethodHandle.GetName(methodHandle), MemberTypes.Constructor | MemberTypes.Method, 
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) as MethodBase[];
 
                    bool loaderAssuredCompatible = false;
                    for (int i = 0; i < methodBases.Length; i++)
                    {
                        IRuntimeMethodInfo rmi = (IRuntimeMethodInfo)methodBases[i]; 
                        if (rmi.Value.Value == methodHandle.Value)
                            loaderAssuredCompatible = true; 
                    } 

                    if (!loaderAssuredCompatible) 
                        throw new ArgumentException(String.Format(
                            CultureInfo.CurrentCulture, Environment.GetResourceString("Argument_ResolveMethodHandle"),
                            reflectedType.ToString(), declaredType.ToString()));
                } 
                // Action<in string> is assignable from, but not a subclass of Action<in object>.
                else if (declaredType.IsGenericType) 
                { 
                    // ignoring instantiation is the ReflectedType a subtype of the DeclaringType
                    RuntimeType declaringDefinition = (RuntimeType)declaredType.GetGenericTypeDefinition(); 

                    RuntimeType baseType = reflectedType;

                    while (baseType != null) 
                    {
                        RuntimeType baseDefinition = baseType; 
 
                        if (baseDefinition.IsGenericType && !baseType.IsGenericTypeDefinition)
                            baseDefinition = (RuntimeType)baseDefinition.GetGenericTypeDefinition(); 

                        if (baseDefinition == declaringDefinition)
                            break;
 
                        baseType = baseType.GetBaseType();
                    } 
 
                    if (baseType == null)
                    { 
                        // ignoring instantiation is the ReflectedType is not a subtype of the DeclaringType
                        throw new ArgumentException(String.Format(
                            CultureInfo.CurrentCulture, Environment.GetResourceString("Argument_ResolveMethodHandle"),
                            reflectedType.ToString(), declaredType.ToString())); 
                    }
 
                    // remap the method to same method on the subclass ReflectedType 
                    declaredType = baseType;
 
                    // if the original methodHandle was the definition then we don't need to rebind generic method arguments
                    // because all RuntimeMethodHandles retrieved off of the canonical method table are definitions. That's
                    // why for everything else we need to rebind the generic method arguments.
                    if (!RuntimeMethodHandle.IsGenericMethodDefinition(methodHandle)) 
                    {
                        methodInstantiation = RuntimeMethodHandle.GetMethodInstantiationInternal(methodHandle); 
                    } 

                    // lookup via v-table slot the RuntimeMethodHandle on the new declaring type 
                    methodHandle = RuntimeMethodHandle.GetMethodFromCanonical(methodHandle, declaredType);
                }
                else if (!declaredType.IsAssignableFrom(reflectedType))
                { 
                    // declaredType is not Array, not generic, and not assignable from reflectedType
                    throw new ArgumentException(String.Format( 
                        CultureInfo.CurrentCulture, Environment.GetResourceString("Argument_ResolveMethodHandle"), 
                        reflectedType.ToString(), declaredType.ToString()));
                } 
            }

            // If methodInstantiation is not null, GetStubIfNeeded will rebind the generic method arguments
            // if declaredType is an instantiated generic type and methodHandle is not generic, get the instantiated MethodDesc (if needed) 
            // if declaredType is a value type, get the unboxing stub (if needed)
 
            // this is so that our behavior here is consistent with that of Type.GetMethod 
            // See MemberInfoCache<RuntimeConstructorInfo>.PopulateMethods and MemberInfoCache<RuntimeMethodInfoInfo>.PopulateConstructors
 
            methodHandle = RuntimeMethodHandle.GetStubIfNeeded(methodHandle, declaredType, methodInstantiation);
            MethodBase retval;

            if (RuntimeMethodHandle.IsConstructor(methodHandle)) 
            {
                // Constructor case: constructors cannot be generic 
                retval = reflectedType.Cache.GetConstructor(declaredType, methodHandle); 
            }
            else 
            {
                // Method case
                if (RuntimeMethodHandle.HasMethodInstantiation(methodHandle) && !RuntimeMethodHandle.IsGenericMethodDefinition(methodHandle))
                    retval = reflectedType.Cache.GetGenericMethodInfo(methodHandle); 
                else
                    retval = reflectedType.Cache.GetMethod(declaredType, methodHandle); 
            } 

            GC.KeepAlive(methodInstantiation); 
            return retval;
        }

        internal bool DomainInitialized 
        {
            get { return Cache.DomainInitialized; } 
            set { Cache.DomainInitialized = value; } 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        internal unsafe static FieldInfo GetFieldInfo(IRuntimeFieldInfo fieldHandle)
        {
            return GetFieldInfo(RuntimeFieldHandle.GetApproxDeclaringType(fieldHandle), fieldHandle); 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        internal unsafe static FieldInfo GetFieldInfo(RuntimeType reflectedType, IRuntimeFieldInfo field)
        { 
            RuntimeFieldHandleInternal fieldHandle = field.Value;

            // verify the type/method relationship
            if (reflectedType == null) 
            {
                reflectedType = RuntimeFieldHandle.GetApproxDeclaringType(fieldHandle); 
            } 
            else
            { 
                RuntimeType declaredType = RuntimeFieldHandle.GetApproxDeclaringType(fieldHandle);
                if (reflectedType != declaredType)
                {
                    if (!RuntimeFieldHandle.AcquiresContextFromThis(fieldHandle) || 
                        !RuntimeTypeHandle.CompareCanonicalHandles(declaredType, reflectedType))
                    { 
                        throw new ArgumentException(String.Format( 
                            CultureInfo.CurrentCulture, Environment.GetResourceString("Argument_ResolveFieldHandle"),
                            reflectedType.ToString(), 
                            declaredType.ToString()));
                    }
                }
            } 

            FieldInfo retVal = reflectedType.Cache.GetField(fieldHandle); 
            GC.KeepAlive(field); 
            return retVal;
        } 

        // Called internally
        private unsafe static PropertyInfo GetPropertyInfo(RuntimeType reflectedType, int tkProperty)
        { 
            RuntimePropertyInfo property = null;
            CerArrayList<RuntimePropertyInfo> candidates = 
                reflectedType.Cache.GetPropertyList(MemberListType.All, null); 

            for (int i = 0; i < candidates.Count; i++) 
            {
                property = candidates[i];
                if (property.MetadataToken == tkProperty)
                    return property; 
            }
 
            Contract.Assume(false, "Unreachable code"); 
            throw new SystemException();
        } 

        private static void ThrowIfTypeNeverValidGenericArgument(RuntimeType type)
        {
            if (type.IsPointer || type.IsByRef || type == typeof(void)) 
                throw new ArgumentException(
                    Environment.GetResourceString("Argument_NeverValidGenericArgument", type.ToString())); 
        } 

 
        internal static void SanityCheckGenericArguments(RuntimeType[] genericArguments, RuntimeType[] genericParamters)
        {
            if (genericArguments == null)
                throw new ArgumentNullException(); 
            Contract.EndContractBlock();
 
            for(int i = 0; i < genericArguments.Length; i++) 
            {
                if (genericArguments[i] == null) 
                    throw new ArgumentNullException();

                ThrowIfTypeNeverValidGenericArgument(genericArguments[i]);
            } 

            if (genericArguments.Length != genericParamters.Length) 
                throw new ArgumentException( 
                    Environment.GetResourceString("Argument_NotEnoughGenArguments", genericArguments.Length, genericParamters.Length));
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated
        internal static void ValidateGenericArguments(MemberInfo definition, RuntimeType[] genericArguments, Exception e)
        { 
            RuntimeType[] typeContext = null;
            RuntimeType[] methodContext = null; 
            RuntimeType[] genericParamters = null; 

            if (definition is Type) 
            {
                RuntimeType genericTypeDefinition = (RuntimeType)definition;
                genericParamters = genericTypeDefinition.GetGenericArgumentsInternal();
                typeContext = genericArguments; 
            }
            else 
            { 
                RuntimeMethodInfo genericMethodDefinition = (RuntimeMethodInfo)definition;
                genericParamters = genericMethodDefinition.GetGenericArgumentsInternal(); 
                methodContext = genericArguments;

                RuntimeType declaringType = (RuntimeType)genericMethodDefinition.DeclaringType;
                if (declaringType != null) 
                {
                    typeContext = declaringType.GetTypeHandleInternal().GetInstantiationInternal(); 
                } 
            }
 
            for (int i = 0; i < genericArguments.Length; i++)
            {
                Type genericArgument = genericArguments[i];
                Type genericParameter = genericParamters[i]; 

                if (!RuntimeTypeHandle.SatisfiesConstraints(genericParameter.GetTypeHandleInternal().GetTypeChecked(), 
                    typeContext, methodContext, genericArgument.GetTypeHandleInternal().GetTypeChecked())) 
                {
                    throw new ArgumentException( 
                        Environment.GetResourceString("Argument_GenConstraintViolation",
                        i.ToString(CultureInfo.CurrentCulture), genericArgument.ToString(), definition.ToString(), genericParameter.ToString()), e);
                }
            } 
        }
 
        private static void SplitName(string fullname, out string name, out string ns) 
        {
            name = null; 
            ns = null;

            if (fullname == null)
                return; 

            // Get namespace 
            int nsDelimiter = fullname.LastIndexOf(".", StringComparison.Ordinal); 
            if (nsDelimiter != -1 )
            { 
                ns = fullname.Substring(0, nsDelimiter);
                int nameLength = fullname.Length - ns.Length - 1;
                if (nameLength != 0)
                    name = fullname.Substring(nsDelimiter + 1, nameLength); 
                else
                    name = ""; 
                Contract.Assert(fullname.Equals(ns + "." + name)); 
            }
            else 
            {
                name = fullname;
            }
 
        }
        #endregion 
 
        #region Filters
        internal static BindingFlags FilterPreCalculate(bool isPublic, bool isInherited, bool isStatic) 
        {
            BindingFlags bindingFlags = isPublic ? BindingFlags.Public : BindingFlags.NonPublic;

            if (isInherited) 
            {
                // We arrange things so the DeclaredOnly flag means "include inherited members" 
                bindingFlags |= BindingFlags.DeclaredOnly; 

                if (isStatic) 
                {
                    bindingFlags |= BindingFlags.Static | BindingFlags.FlattenHierarchy;
                }
                else 
                {
                    bindingFlags |= BindingFlags.Instance; 
                } 
            }
            else 
            {
                if (isStatic)
                {
                    bindingFlags |= BindingFlags.Static; 
                }
                else 
                { 
                    bindingFlags |= BindingFlags.Instance;
                } 
            }

            return bindingFlags;
        } 

        // Calculate prefixLookup, ignoreCase, and listType for use by GetXXXCandidates 
        private static void FilterHelper( 
            BindingFlags bindingFlags, ref string name, bool allowPrefixLookup, out bool prefixLookup,
            out bool ignoreCase, out MemberListType listType) 
        {
            prefixLookup = false;
            ignoreCase = false;
 
            if (name != null)
            { 
                if ((bindingFlags & BindingFlags.IgnoreCase) != 0) 
                {
                    name = name.ToLower(CultureInfo.InvariantCulture); 
                    ignoreCase = true;
                    listType = MemberListType.CaseInsensitive;
                }
                else 
                {
                    listType = MemberListType.CaseSensitive; 
                } 

                if (allowPrefixLookup && name.EndsWith("*", StringComparison.Ordinal)) 
                {
                    // We set prefixLookup to true if name ends with a "*".
                    // We will also set listType to All so that all members are included in
                    // the candidates which are later filtered by FilterApplyPrefixLookup. 
                    name = name.Substring(0, name.Length - 1);
                    prefixLookup = true; 
                    listType = MemberListType.All; 
                }
            } 
            else
            {
                listType = MemberListType.All;
            } 
        }
 
        // Used by the singular GetXXX APIs (Event, Field, Interface, NestedType) where prefixLookup is not supported. 
        private static void FilterHelper(BindingFlags bindingFlags, ref string name, out bool ignoreCase, out MemberListType listType)
        { 
            bool prefixLookup;
            FilterHelper(bindingFlags, ref name, false, out prefixLookup, out ignoreCase, out listType);
        }
 
        // Only called by GetXXXCandidates, GetInterfaces, and GetNestedTypes when FilterHelper has set "prefixLookup" to true.
        // Most of the plural GetXXX methods allow prefix lookups while the singular GetXXX methods mostly do not. 
        private static bool FilterApplyPrefixLookup(MemberInfo memberInfo, string name, bool ignoreCase) 
        {
            Contract.Assert(name != null); 

            if (ignoreCase)
            {
                if (!memberInfo.Name.ToLower(CultureInfo.InvariantCulture).StartsWith(name, StringComparison.Ordinal)) 
                    return false;
            } 
            else 
            {
                if (!memberInfo.Name.StartsWith(name, StringComparison.Ordinal)) 
                    return false;
            }

            return true; 
        }
 
 
        // Used by FilterApplyType to perform all the filtering based on name and BindingFlags
        private static bool FilterApplyBase( 
            MemberInfo memberInfo, BindingFlags bindingFlags, bool isPublic, bool isNonProtectedInternal, bool isStatic,
            string name, bool prefixLookup)
        {
            #region Preconditions 
            Contract.Requires(memberInfo != null);
            Contract.Requires(name == null || (bindingFlags & BindingFlags.IgnoreCase) == 0 || (name.ToLower(CultureInfo.InvariantCulture).Equals(name))); 
            #endregion 

            #region Filter by Public & Private 
            if (isPublic)
            {
                if ((bindingFlags & BindingFlags.Public) == 0)
                    return false; 
            }
            else 
            { 
                if ((bindingFlags & BindingFlags.NonPublic) == 0)
                    return false; 
            }
            #endregion

            bool isInherited = !Object.ReferenceEquals(memberInfo.DeclaringType, memberInfo.ReflectedType); 

            #region Filter by DeclaredOnly 
            if ((bindingFlags & BindingFlags.DeclaredOnly) != 0 && isInherited) 
                return false;
            #endregion 

            #region Filter by Static & Instance
            if (memberInfo.MemberType != MemberTypes.TypeInfo &&
                memberInfo.MemberType != MemberTypes.NestedType) 
            {
                if (isStatic) 
                { 
                    if ((bindingFlags & BindingFlags.FlattenHierarchy) == 0 && isInherited)
                        return false; 

                    if ((bindingFlags & BindingFlags.Static) == 0)
                        return false;
                } 
                else
                { 
                    if ((bindingFlags & BindingFlags.Instance) == 0) 
                        return false;
                } 
            }
            #endregion

            #region Filter by name wrt prefixLookup and implicitly by case sensitivity 
            if (prefixLookup == true)
            { 
                if (!FilterApplyPrefixLookup(memberInfo, name, (bindingFlags & BindingFlags.IgnoreCase) != 0)) 
                    return false;
            } 
            #endregion

            #region Asymmetries
            // @Asymmetry - Internal, inherited, instance, non-protected, non-virtual, non-abstract members returned 
            //              iff BindingFlags !DeclaredOnly, Instance and Public are present except for fields
            if (((bindingFlags & BindingFlags.DeclaredOnly) == 0) &&        // DeclaredOnly not present 
                 isInherited  &&                                            // Is inherited Member 

                (isNonProtectedInternal) &&                                 // Is non-protected internal member 
                ((bindingFlags & BindingFlags.NonPublic) != 0) &&           // BindingFlag.NonPublic present

                (!isStatic) &&                                              // Is instance member
                ((bindingFlags & BindingFlags.Instance) != 0))              // BindingFlag.Instance present 
            {
                MethodInfo methodInfo = memberInfo as MethodInfo; 
 
                if (methodInfo == null)
                    return false; 

                if (!methodInfo.IsVirtual && !methodInfo.IsAbstract)
                    return false;
            } 
            #endregion
 
            return true; 
        }
 

        // Used by GetInterface and GetNestedType(s) which don't need parameter type filtering.
        private static bool FilterApplyType(
            Type type, BindingFlags bindingFlags, string name, bool prefixLookup, string ns) 
        {
            Contract.Requires((object)type != null); 
            Contract.Assert(type is RuntimeType); 

            bool isPublic = type.IsNestedPublic || type.IsPublic; 
            bool isStatic = false;

            if (!RuntimeType.FilterApplyBase(type, bindingFlags, isPublic, type.IsNestedAssembly, isStatic, name, prefixLookup))
                return false; 

            if (ns != null && !type.Namespace.Equals(ns)) 
                return false; 

            return true; 
        }


        private static bool FilterApplyMethodInfo( 
            RuntimeMethodInfo method, BindingFlags bindingFlags, CallingConventions callConv, Type[] argumentTypes)
        { 
            // Optimization: Pre-Calculate the method binding flags to avoid casting. 
            return FilterApplyMethodBase(method, method.BindingFlags, bindingFlags, callConv, argumentTypes);
        } 

#if !FEATURE_CORECLR
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif 
        private static bool FilterApplyConstructorInfo(
            RuntimeConstructorInfo constructor, BindingFlags bindingFlags, CallingConventions callConv, Type[] argumentTypes) 
        { 
            // Optimization: Pre-Calculate the method binding flags to avoid casting.
            return FilterApplyMethodBase(constructor, constructor.BindingFlags, bindingFlags, callConv, argumentTypes); 
        }

        // Used by GetMethodCandidates/GetConstructorCandidates, InvokeMember, and CreateInstanceImpl to perform the necessary filtering.
        // Should only be called by FilterApplyMethodInfo and FilterApplyConstructorInfo. 
        private static bool FilterApplyMethodBase(
            MethodBase methodBase, BindingFlags methodFlags, BindingFlags bindingFlags, CallingConventions callConv, Type[] argumentTypes) 
        { 
            Contract.Requires(methodBase != null);
 
            bindingFlags ^= BindingFlags.DeclaredOnly;

            #region Apply Base Filter
            if ((bindingFlags & methodFlags) != methodFlags) 
                return false;
            #endregion 
 
            #region Check CallingConvention
            if ((callConv & CallingConventions.Any) == 0) 
            {
                if ((callConv & CallingConventions.VarArgs) != 0 &&
                    (methodBase.CallingConvention & CallingConventions.VarArgs) == 0)
                    return false; 

                if ((callConv & CallingConventions.Standard) != 0 && 
                    (methodBase.CallingConvention & CallingConventions.Standard) == 0) 
                    return false;
            } 
            #endregion

            #region If argumentTypes supplied
            if (argumentTypes != null) 
            {
                ParameterInfo[] parameterInfos = methodBase.GetParametersNoCopy(); 
 
                if (argumentTypes.Length != parameterInfos.Length)
                { 
                    #region Invoke Member, Get\Set & Create Instance specific case
                    // If the number of supplied arguments differs than the number in the signature AND
                    // we are not filtering for a dynamic call -- InvokeMethod or CreateInstance -- filter out the method.
                    if ((bindingFlags & 
                        (BindingFlags.InvokeMethod | BindingFlags.CreateInstance | BindingFlags.GetProperty | BindingFlags.SetProperty)) == 0)
                        return false; 
 
                    bool testForParamArray = false;
                    bool excessSuppliedArguments = argumentTypes.Length > parameterInfos.Length; 

                    if (excessSuppliedArguments)
                    { // more supplied arguments than parameters, additional arguments could be vararg
                        #region Varargs 
                        // If method is not vararg, additional arguments can not be passed as vararg
                        if ((methodBase.CallingConvention & CallingConventions.VarArgs) == 0) 
                        { 
                            testForParamArray = true;
                        } 
                        else
                        {
                            // If Binding flags did not include varargs we would have filtered this vararg method.
                            // This Invariant established during callConv check. 
                            Contract.Assert((callConv & CallingConventions.VarArgs) != 0);
                        } 
                        #endregion 
                    }
                    else 
                    {// fewer supplied arguments than parameters, missing arguments could be optional
                        #region OptionalParamBinding
                        if ((bindingFlags & BindingFlags.OptionalParamBinding) == 0)
                        { 
                            testForParamArray = true;
                        } 
                        else 
                        {
                            // From our existing code, our policy here is that if a parameterInfo 
                            // is optional then all subsequent parameterInfos shall be optional.

                            // Thus, iff the first parameterInfo is not optional then this MethodInfo is no longer a canidate.
                            if (!parameterInfos[argumentTypes.Length].IsOptional) 
                                testForParamArray = true;
                        } 
                        #endregion 
                    }
 
                    #region ParamArray
                    if (testForParamArray)
                    {
                        if  (parameterInfos.Length == 0) 
                            return false;
 
                        // The last argument of the signature could be a param array. 
                        bool shortByMoreThanOneSuppliedArgument = argumentTypes.Length < parameterInfos.Length - 1;
 
                        if (shortByMoreThanOneSuppliedArgument)
                            return false;

                        ParameterInfo lastParameter = parameterInfos[parameterInfos.Length - 1]; 

                        if (!lastParameter.ParameterType.IsArray) 
                            return false; 

                        if (!lastParameter.IsDefined(typeof(ParamArrayAttribute), false)) 
                            return false;
                    }
                    #endregion
 
                    #endregion
                } 
                else 
                {
                    #region Exact Binding 
                    if ((bindingFlags & BindingFlags.ExactBinding) != 0)
                    {
                        // Legacy behavior is to ignore ExactBinding when InvokeMember is specified.
                        // Why filter by InvokeMember? If the answer is we leave this to the binder then why not leave 
                        // all the rest of this  to the binder too? Further, what other semanitc would the binder
                        // use for BindingFlags.ExactBinding besides this one? Further, why not include CreateInstance 
                        // in this if statement? That's just InvokeMethod with a constructor, right? 
                        if ((bindingFlags & (BindingFlags.InvokeMethod)) == 0)
                        { 
                            for(int i = 0; i < parameterInfos.Length; i ++)
                            {
                                // a null argument type implies a null arg which is always a perfect match
                                if ((object)argumentTypes[i] != null && !Object.ReferenceEquals(parameterInfos[i].ParameterType, argumentTypes[i])) 
                                    return false;
                            } 
                        } 
                    }
                    #endregion 
                }
            }
            #endregion
 
            return true;
        } 
 
        #endregion
 
        #endregion

        #region Private Data Members
        private object m_keepalive; // This will be filled with a LoaderAllocator reference when this RuntimeType represents a collectible type 
        private IntPtr m_cache;
        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        internal IntPtr m_handle; 

        private class TypeCacheQueue
        {
            // must be a power of 2 for this to work 
            const int QUEUE_SIZE = 4;
 
            Object[] liveCache; 

            internal TypeCacheQueue() 
            {
                liveCache = new Object[QUEUE_SIZE];
            }
        } 
        private static TypeCacheQueue s_typeCache = null;
 
        internal static readonly RuntimeType ValueType = (RuntimeType)typeof(System.ValueType); 
        internal static readonly RuntimeType EnumType = (RuntimeType)typeof(System.Enum);
 
        private static readonly RuntimeType ObjectType = (RuntimeType)typeof(System.Object);
        private static readonly RuntimeType StringType = (RuntimeType)typeof(System.String);
        private static readonly RuntimeType DelegateType = (RuntimeType)typeof(System.Delegate);
        #endregion 

        #region Constructor 
        internal RuntimeType() { throw new NotSupportedException(); } 
        #endregion
 
        #region Private\Internal Members
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        internal override bool CacheEquals(object o)
        { 
            RuntimeType m = o as RuntimeType;
 
            if (m == null) 
                return false;
 
            return m.m_handle.Equals(m_handle);
        }

        private RuntimeTypeCache Cache 
        {
            [System.Security.SecuritySafeCritical]  // auto-generated 
            [ResourceExposure(ResourceScope.None)] 
            [ResourceConsumption(ResourceScope.AppDomain, ResourceScope.AppDomain)]
            get 
            {
                if (m_cache.IsNull())
                {
                    IntPtr newgcHandle = new RuntimeTypeHandle(this).GetGCHandle(GCHandleType.WeakTrackResurrection); 
                    IntPtr gcHandle = Interlocked.CompareExchange(ref m_cache, newgcHandle, (IntPtr)0);
                    // Leak the handle if the type is collectible. It will be reclaimed when 
                    // the type goes away. 
                    if (!gcHandle.IsNull() && !new RuntimeTypeHandle(this).IsCollectible())
                        GCHandle.InternalFree(newgcHandle); 
                }

                RuntimeTypeCache cache = GCHandle.InternalGet(m_cache) as RuntimeTypeCache;
                if (cache == null) 
                {
                    cache = new RuntimeTypeCache(this); 
                    RuntimeTypeCache existingCache = GCHandle.InternalCompareExchange(m_cache, cache, null, false) as RuntimeTypeCache; 
                    if (existingCache != null)
                        cache = existingCache; 
                    if (s_typeCache == null)
                        s_typeCache = new TypeCacheQueue();
                    //s_typeCache.Add(cache);
                } 
/*
                RuntimeTypeCache cache = m_cache as RuntimeTypeCache; 
                if (cache == null) 
                {
                    cache = new RuntimeTypeCache(TypeHandle); 
                    RuntimeTypeCache existingCache = Interlocked.CompareExchange(ref m_cache, cache, null) as RuntimeTypeCache;
                    if (existingCache != null)
                        cache = existingCache;
                } 
*/
                Contract.Assert(cache != null); 
                return cache; 
            }
        } 

        internal bool IsSpecialSerializableType()
        {
            RuntimeType rt = this; 
            do
            { 
                // In all sane cases we only need to compare the direct level base type with 
                // System.Enum and System.MulticastDelegate. However, a generic argument can
                // have a base type constraint that is Delegate or even a real delegate type. 
                // Let's maintain compatibility and return true for them.
                if (rt == RuntimeType.DelegateType || rt == RuntimeType.EnumType)
                    return true;
 
                rt = rt.GetBaseType();
            } while (rt != null); 
 
            return false;
        } 
        #endregion

        #region Type Overrides
 
        #region Get XXXInfo Candidates
        private MethodInfo[] GetMethodCandidates( 
            String name, BindingFlags bindingAttr, CallingConventions callConv, 
            Type[] types, bool allowPrefixLookup)
        { 
            bool prefixLookup, ignoreCase;
            MemberListType listType;
            RuntimeType.FilterHelper(bindingAttr, ref name, allowPrefixLookup, out prefixLookup, out ignoreCase, out listType);
 
            CerArrayList<RuntimeMethodInfo> cache = Cache.GetMethodList(listType, name);
 
            List<MethodInfo> candidates = new List<MethodInfo>(cache.Count); 
            for (int i = 0; i < cache.Count; i++)
            { 
                RuntimeMethodInfo methodInfo = cache[i];
                if (FilterApplyMethodInfo(methodInfo, bindingAttr, callConv, types) &&
                    (!prefixLookup || RuntimeType.FilterApplyPrefixLookup(methodInfo, name, ignoreCase)))
                { 
                    candidates.Add(methodInfo);
                } 
            } 

            return candidates.ToArray(); 
        }


        private ConstructorInfo[]  GetConstructorCandidates( 
            string name, BindingFlags bindingAttr, CallingConventions callConv,
            Type[] types, bool allowPrefixLookup) 
        { 
            bool prefixLookup, ignoreCase;
            MemberListType listType; 
            RuntimeType.FilterHelper(bindingAttr, ref name, allowPrefixLookup, out prefixLookup, out ignoreCase, out listType);

            CerArrayList<RuntimeConstructorInfo> cache = Cache.GetConstructorList(listType, name);
 
            List<ConstructorInfo> candidates = new List<ConstructorInfo>(cache.Count);
            for (int i = 0; i < cache.Count; i++) 
            { 
                RuntimeConstructorInfo constructorInfo = cache[i];
                if (FilterApplyConstructorInfo(constructorInfo, bindingAttr, callConv, types) && 
                    (!prefixLookup || RuntimeType.FilterApplyPrefixLookup(constructorInfo, name, ignoreCase)))
                {
                    candidates.Add(constructorInfo);
                } 
            }
 
            return candidates.ToArray(); 
        }
 

        private PropertyInfo[] GetPropertyCandidates(
            String name, BindingFlags bindingAttr, Type[] types, bool allowPrefixLookup)
        { 
            bool prefixLookup, ignoreCase;
            MemberListType listType; 
            RuntimeType.FilterHelper(bindingAttr, ref name, allowPrefixLookup, out prefixLookup, out ignoreCase, out listType); 

            CerArrayList<RuntimePropertyInfo> cache = Cache.GetPropertyList(listType, name); 

            bindingAttr ^= BindingFlags.DeclaredOnly;

            List<PropertyInfo> candidates = new List<PropertyInfo>(cache.Count); 
            for (int i = 0; i < cache.Count; i++)
            { 
                RuntimePropertyInfo propertyInfo = cache[i]; 
                if ((bindingAttr & propertyInfo.BindingFlags) == propertyInfo.BindingFlags &&
                    (!prefixLookup || RuntimeType.FilterApplyPrefixLookup(propertyInfo, name, ignoreCase)) && 
                    (types == null || (propertyInfo.GetIndexParameters().Length == types.Length)))
                {
                    candidates.Add(propertyInfo);
                } 
            }
 
            return candidates.ToArray(); 
        }
 

        private EventInfo[] GetEventCandidates(String name, BindingFlags bindingAttr, bool allowPrefixLookup)
        {
            bool prefixLookup, ignoreCase; 
            MemberListType listType;
            RuntimeType.FilterHelper(bindingAttr, ref name, allowPrefixLookup, out prefixLookup, out ignoreCase, out listType); 
 
            CerArrayList<RuntimeEventInfo> cache = Cache.GetEventList(listType, name);
 
            bindingAttr ^= BindingFlags.DeclaredOnly;

            List<EventInfo> candidates = new List<EventInfo>(cache.Count);
            for (int i = 0; i < cache.Count; i++) 
            {
                RuntimeEventInfo eventInfo = cache[i]; 
                if ((bindingAttr & eventInfo.BindingFlags) == eventInfo.BindingFlags && 
                    (!prefixLookup || RuntimeType.FilterApplyPrefixLookup(eventInfo, name, ignoreCase)))
                { 
                    candidates.Add(eventInfo);
                }
            }
 
            return candidates.ToArray();
        } 
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        private FieldInfo[] GetFieldCandidates(String name, BindingFlags bindingAttr, bool allowPrefixLookup) 
        {
            bool prefixLookup, ignoreCase;
            MemberListType listType;
            RuntimeType.FilterHelper(bindingAttr, ref name, allowPrefixLookup, out prefixLookup, out ignoreCase, out listType); 

            CerArrayList<RuntimeFieldInfo> cache = Cache.GetFieldList(listType, name); 
 
            bindingAttr ^= BindingFlags.DeclaredOnly;
 
            List<FieldInfo> candidates = new List<FieldInfo>(cache.Count);
            for (int i = 0; i < cache.Count; i++)
            {
                RuntimeFieldInfo fieldInfo = cache[i]; 
                if ((bindingAttr & fieldInfo.BindingFlags) == fieldInfo.BindingFlags &&
                    (!prefixLookup || FilterApplyPrefixLookup(fieldInfo, name, ignoreCase))) 
                { 
                    candidates.Add(fieldInfo);
                } 
            }

            return candidates.ToArray();
        } 

        private Type[] GetNestedTypeCandidates(String fullname, BindingFlags bindingAttr, bool allowPrefixLookup) 
        { 
            bool prefixLookup, ignoreCase;
            bindingAttr &= ~BindingFlags.Static; 
            string name, ns;
            MemberListType listType;
            SplitName(fullname, out name, out ns);
            RuntimeType.FilterHelper(bindingAttr, ref name, allowPrefixLookup, out prefixLookup, out ignoreCase, out listType); 

            CerArrayList<RuntimeType> cache = Cache.GetNestedTypeList(listType, name); 
 
            List<Type> candidates = new List<Type>(cache.Count);
            for (int i = 0; i < cache.Count; i++) 
            {
                RuntimeType nestedClass = cache[i];
                if (RuntimeType.FilterApplyType(nestedClass, bindingAttr, name, prefixLookup, ns))
                { 
                    candidates.Add(nestedClass);
                } 
            } 

            return candidates.ToArray(); 
        }
        #endregion

        #region Get All XXXInfos 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public override MethodInfo[] GetMethods(BindingFlags bindingAttr) 
        { 
            return GetMethodCandidates(null, bindingAttr, CallingConventions.Any, null, false);
        } 

[System.Security.SecuritySafeCritical]  // auto-generated
[System.Runtime.InteropServices.ComVisible(true)]
#if !FEATURE_CORECLR 
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif 
        public override ConstructorInfo[] GetConstructors(BindingFlags bindingAttr) 
        {
            return GetConstructorCandidates(null, bindingAttr, CallingConventions.Any, null, false); 
        }

        [System.Security.SecuritySafeCritical]  // auto-generated
#if !FEATURE_CORECLR 
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif 
        public override PropertyInfo[] GetProperties(BindingFlags bindingAttr) 
        {
            return GetPropertyCandidates(null, bindingAttr, null, false); 
        }

        [System.Security.SecuritySafeCritical]  // auto-generated
        public override EventInfo[] GetEvents(BindingFlags bindingAttr) 
        {
            return GetEventCandidates(null, bindingAttr, false); 
        } 

#if !FEATURE_CORECLR 
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif
        public override FieldInfo[] GetFields(BindingFlags bindingAttr)
        { 
            return GetFieldCandidates(null, bindingAttr, false);
        } 
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public override Type[] GetInterfaces() 
        {
              CerArrayList<RuntimeType> candidates = this.Cache.GetInterfaceList(MemberListType.All, null);
              Type[] interfaces = new Type[candidates.Count];
              for (int i = 0; i < candidates.Count; i++) 
                  JitHelpers.UnsafeSetArrayElement(interfaces, i, candidates[i]);
 
              return interfaces; 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public override Type[] GetNestedTypes(BindingFlags bindingAttr)
        {
            return GetNestedTypeCandidates(null, bindingAttr, false); 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        public override MemberInfo[] GetMembers(BindingFlags bindingAttr)
        { 
            MethodInfo[] methods = GetMethodCandidates(null, bindingAttr, CallingConventions.Any, null, false);
            ConstructorInfo[] constructors = GetConstructorCandidates(null, bindingAttr, CallingConventions.Any, null, false);
            PropertyInfo[] properties = GetPropertyCandidates(null, bindingAttr, null, false);
            EventInfo[] events = GetEventCandidates(null, bindingAttr, false); 
            FieldInfo[] fields = GetFieldCandidates(null, bindingAttr, false);
            Type[] nestedTypes = GetNestedTypeCandidates(null, bindingAttr, false); 
            // Interfaces are excluded from the result of GetMembers 

            MemberInfo[] members = new MemberInfo[ 
                methods.Length +
                constructors.Length +
                properties.Length +
                events.Length + 
                fields.Length +
                nestedTypes.Length]; 
 
            int i = 0;
            Array.Copy(methods, 0, members, i, methods.Length); i += methods.Length; 
            Array.Copy(constructors, 0, members, i, constructors.Length); i += constructors.Length;
            Array.Copy(properties, 0, members, i, properties.Length); i += properties.Length;
            Array.Copy(events, 0, members, i, events.Length); i += events.Length;
            Array.Copy(fields, 0, members, i, fields.Length); i += fields.Length; 
            Array.Copy(nestedTypes, 0, members, i, nestedTypes.Length); i += nestedTypes.Length;
 
            Contract.Assert(i == members.Length); 

            return members; 
        }

        [System.Security.SecuritySafeCritical]  // auto-generated
        public override InterfaceMapping GetInterfaceMap(Type ifaceType) 
        {
            if (IsGenericParameter) 
                throw new InvalidOperationException(Environment.GetResourceString("Arg_GenericParameter")); 

            if ((object)ifaceType == null) 
                throw new ArgumentNullException("ifaceType");
            Contract.EndContractBlock();

            RuntimeType ifaceRtType = ifaceType as RuntimeType; 

            if (ifaceRtType == null) 
                throw new ArgumentException(Environment.GetResourceString("Argument_MustBeRuntimeType"), "ifaceType"); 

            RuntimeTypeHandle ifaceRtTypeHandle = ifaceRtType.GetTypeHandleInternal(); 

            GetTypeHandleInternal().VerifyInterfaceIsImplemented(ifaceRtTypeHandle);
            Contract.Assert(ifaceType.IsInterface);  // VerifyInterfaceIsImplemented enforces this invariant
            Contract.Assert(!IsInterface); // VerifyInterfaceIsImplemented enforces this invariant 

            // SZArrays implement the methods on IList`1, IEnumerable`1, and ICollection`1 with 
            // SZArrayHelper and some runtime magic. We don't have accurate interface maps for them. 
            if (IsSzArray && ifaceType.IsGenericType)
                throw new ArgumentException(Environment.GetResourceString("Argument_ArrayGetInterfaceMap")); 

            int ifaceSlotCount = RuntimeTypeHandle.GetInterfaceMethodSlots(ifaceRtType);
            int ifaceStaticMethodCount = 0;
 
            // @Optimization - Most interface have the same number of static members.
 
            // Filter out static methods 
            for (int i = 0; i < ifaceSlotCount; i ++)
            { 
                if ((RuntimeMethodHandle.GetAttributes(RuntimeTypeHandle.GetMethodAt(ifaceRtType, i)) & MethodAttributes.Static) != 0)
                    ifaceStaticMethodCount++;
            }
 
            int ifaceInstanceMethodCount = ifaceSlotCount - ifaceStaticMethodCount;
 
            InterfaceMapping im; 
            im.InterfaceType = ifaceType;
            im.TargetType = this; 
            im.InterfaceMethods = new MethodInfo[ifaceInstanceMethodCount];
            im.TargetMethods = new MethodInfo[ifaceInstanceMethodCount];

            for(int i = 0; i < ifaceSlotCount; i++) 
            {
                RuntimeMethodHandleInternal ifaceRtMethodHandle = RuntimeTypeHandle.GetMethodAt(ifaceRtType, i); 
                if (ifaceStaticMethodCount > 0 && ((RuntimeMethodHandle.GetAttributes(ifaceRtMethodHandle) & MethodAttributes.Static) != 0)) 
                {
                    ifaceStaticMethodCount--; 
                    continue;
                }

                // GetMethodBase will convert this to the instantiating/unboxing stub if necessary 
                MethodBase ifaceMethodBase = RuntimeType.GetMethodBase(ifaceRtType, ifaceRtMethodHandle);
                Contract.Assert(ifaceMethodBase is RuntimeMethodInfo); 
                im.InterfaceMethods[i] = (MethodInfo)ifaceMethodBase; 

                // If the slot is -1, then virtual stub dispatch is active. 
                int slot = GetTypeHandleInternal().GetInterfaceMethodImplementationSlot(ifaceRtTypeHandle, ifaceRtMethodHandle);

                if (slot == -1) continue;
 
                RuntimeMethodHandleInternal classRtMethodHandle = RuntimeTypeHandle.GetMethodAt(this, slot);
 
                // GetMethodBase will convert this to the instantiating/unboxing stub if necessary 
                MethodBase rtTypeMethodBase = RuntimeType.GetMethodBase(this, classRtMethodHandle);
                // a class may not implement all the methods of an interface (abstract class) so null is a valid value 
                Contract.Assert(rtTypeMethodBase == null || rtTypeMethodBase is RuntimeMethodInfo);
                im.TargetMethods[i] = (MethodInfo)rtTypeMethodBase;
            }
 
            return im;
        } 
        #endregion 

        #region Find XXXInfo 
        [System.Security.SecuritySafeCritical]  // auto-generated
        protected override MethodInfo GetMethodImpl(
            String name, BindingFlags bindingAttr, Binder binder, CallingConventions callConv,
            Type[] types, ParameterModifier[] modifiers) 
        {
            MethodInfo[] candidates = GetMethodCandidates(name, bindingAttr, callConv, types, false); 
 
            if (candidates.Length == 0)
                return null; 

            if (types == null || types.Length == 0)
            {
                if (candidates.Length == 1) 
                {
                    return candidates[0]; 
                } 
                else if (types == null)
                { 
                    for (int j = 1; j < candidates.Length; j++)
                    {
                        MethodInfo methodInfo = candidates[j];
                        if (!System.DefaultBinder.CompareMethodSigAndName(methodInfo, candidates[0])) 
                        {
                            throw new AmbiguousMatchException(Environment.GetResourceString("Arg_AmbiguousMatchException")); 
                        } 
                    }
 
                    // All the methods have the exact same name and sig so return the most derived one.
                    return System.DefaultBinder.FindMostDerivedNewSlotMeth(candidates, candidates.Length) as MethodInfo;
                }
            } 

            if (binder == null) 
                binder = DefaultBinder; 

            return binder.SelectMethod(bindingAttr, candidates, types, modifiers) as MethodInfo; 
        }


        [System.Security.SecuritySafeCritical]  // auto-generated 
        protected override ConstructorInfo GetConstructorImpl(
            BindingFlags bindingAttr, Binder binder, CallingConventions callConvention, 
            Type[] types, ParameterModifier[] modifiers) 
        {
            ConstructorInfo[] candidates = GetConstructorCandidates(null, bindingAttr, CallingConventions.Any, types, false); 

            if (binder == null)
                binder = DefaultBinder;
 
            if (candidates.Length == 0)
                return null; 
 
            if (types.Length == 0 && candidates.Length == 1)
            { 
                ParameterInfo[] parameters = (candidates[0]).GetParametersNoCopy();
                if (parameters == null || parameters.Length == 0)
                {
                    return candidates[0]; 
                }
            } 
 
            if ((bindingAttr & BindingFlags.ExactBinding) != 0)
                return System.DefaultBinder.ExactBinding(candidates, types, modifiers) as ConstructorInfo; 

            return binder.SelectMethod(bindingAttr, candidates, types, modifiers) as ConstructorInfo;
        }
 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        protected override PropertyInfo GetPropertyImpl( 
            String name, BindingFlags bindingAttr, Binder binder, Type returnType, Type[] types, ParameterModifier[] modifiers)
        { 
            if (name == null) throw new ArgumentNullException();
            Contract.EndContractBlock();

            PropertyInfo[] candidates = GetPropertyCandidates(name, bindingAttr, types, false); 

            if (binder == null) 
                binder = DefaultBinder; 

            if (candidates.Length == 0) 
                return null;

            if (types == null || types.Length == 0)
            { 
                // no arguments
                if (candidates.Length == 1) 
                { 
                    if ((object)returnType != null && !returnType.IsEquivalentTo(candidates[0].PropertyType))
                        return null; 

                    return candidates[0];
                }
                else 
                {
                    if ((object)returnType == null) 
                        // if we are here we have no args or property type to select over and we have more than one property with that name 
                        throw new AmbiguousMatchException(Environment.GetResourceString("Arg_AmbiguousMatchException"));
                } 
            }

            if ((bindingAttr & BindingFlags.ExactBinding) != 0)
                return System.DefaultBinder.ExactPropertyBinding(candidates, returnType, types, modifiers); 

            return binder.SelectProperty(bindingAttr, candidates, returnType, types, modifiers); 
        } 

 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public override EventInfo GetEvent(String name, BindingFlags bindingAttr)
        {
            if (name == null) throw new ArgumentNullException(); 
            Contract.EndContractBlock();
 
            bool ignoreCase; 
            MemberListType listType;
            RuntimeType.FilterHelper(bindingAttr, ref name, out ignoreCase, out listType); 

            CerArrayList<RuntimeEventInfo> cache = Cache.GetEventList(listType, name);
            EventInfo match = null;
 
            bindingAttr ^= BindingFlags.DeclaredOnly;
 
            for (int i = 0; i < cache.Count; i++) 
            {
                RuntimeEventInfo eventInfo = cache[i]; 
                if ((bindingAttr & eventInfo.BindingFlags) == eventInfo.BindingFlags)
                {
                    if (match != null)
                        throw new AmbiguousMatchException(Environment.GetResourceString("Arg_AmbiguousMatchException")); 

                    match = eventInfo; 
                } 
            }
 
            return match;
        }

        [System.Security.SecuritySafeCritical]  // auto-generated 
        public override FieldInfo GetField(String name, BindingFlags bindingAttr)
        { 
            if (name == null) throw new ArgumentNullException(); 
            Contract.EndContractBlock();
 
            bool ignoreCase;
            MemberListType listType;
            RuntimeType.FilterHelper(bindingAttr, ref name, out ignoreCase, out listType);
 
            CerArrayList<RuntimeFieldInfo> cache = Cache.GetFieldList(listType, name);
            FieldInfo match = null; 
 
            bindingAttr ^= BindingFlags.DeclaredOnly;
            bool multipleStaticFieldMatches = false; 

            for (int i = 0; i < cache.Count; i++)
            {
                RuntimeFieldInfo fieldInfo = cache[i]; 
                if ((bindingAttr & fieldInfo.BindingFlags) == fieldInfo.BindingFlags)
                { 
                    if (match != null) 
                    {
                        if (Object.ReferenceEquals(fieldInfo.DeclaringType, match.DeclaringType)) 
                            throw new AmbiguousMatchException(Environment.GetResourceString("Arg_AmbiguousMatchException"));

                        if ((match.DeclaringType.IsInterface == true) && (fieldInfo.DeclaringType.IsInterface == true))
                            multipleStaticFieldMatches = true; 
                    }
 
                    if (match == null || fieldInfo.DeclaringType.IsSubclassOf(match.DeclaringType) || match.DeclaringType.IsInterface) 
                        match = fieldInfo;
                } 
            }

            if (multipleStaticFieldMatches && match.DeclaringType.IsInterface)
                throw new AmbiguousMatchException(Environment.GetResourceString("Arg_AmbiguousMatchException")); 

            return match; 
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        public override Type GetInterface(String fullname, bool ignoreCase)
        {
            if (fullname == null) throw new ArgumentNullException();
            Contract.EndContractBlock(); 

            BindingFlags bindingAttr = BindingFlags.Public | BindingFlags.NonPublic; 
 
            bindingAttr &= ~BindingFlags.Static;
 
            if (ignoreCase)
                bindingAttr |= BindingFlags.IgnoreCase;

            string name, ns; 
            MemberListType listType;
            SplitName(fullname, out name, out ns); 
            RuntimeType.FilterHelper(bindingAttr, ref name, out ignoreCase, out listType); 

            CerArrayList<RuntimeType> cache = Cache.GetInterfaceList(listType, name); 

            RuntimeType match = null;

            for (int i = 0; i < cache.Count; i++) 
            {
                RuntimeType iface = cache[i]; 
                if (RuntimeType.FilterApplyType(iface, bindingAttr, name, false, ns)) 
                {
                    if (match != null) 
                        throw new AmbiguousMatchException(Environment.GetResourceString("Arg_AmbiguousMatchException"));

                    match = iface;
                } 
            }
 
            return match; 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public override Type GetNestedType(String fullname, BindingFlags bindingAttr)
        {
            if (fullname == null) throw new ArgumentNullException(); 
            Contract.EndContractBlock();
 
            bool ignoreCase; 
            bindingAttr &= ~BindingFlags.Static;
            string name, ns; 
            MemberListType listType;
            SplitName(fullname, out name, out ns);
            RuntimeType.FilterHelper(bindingAttr, ref name, out ignoreCase, out listType);
 
            CerArrayList<RuntimeType> cache = Cache.GetNestedTypeList(listType, name);
 
            RuntimeType match = null; 

            for (int i = 0; i < cache.Count; i++) 
            {
                RuntimeType nestedType = cache[i];
                if (RuntimeType.FilterApplyType(nestedType, bindingAttr, name, false, ns))
                { 
                    if (match != null)
                        throw new AmbiguousMatchException(Environment.GetResourceString("Arg_AmbiguousMatchException")); 
 
                    match = nestedType;
                } 
            }

            return match;
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        public override MemberInfo[] GetMember(String name, MemberTypes type, BindingFlags bindingAttr) 
        {
            if (name == null) throw new ArgumentNullException(); 
            Contract.EndContractBlock();

            MethodInfo[] methods = new MethodInfo[0];
            ConstructorInfo[] constructors = new ConstructorInfo[0]; 
            PropertyInfo[] properties = new PropertyInfo[0];
            EventInfo[] events = new EventInfo[0]; 
            FieldInfo[] fields = new FieldInfo[0]; 
            Type[] nestedTypes = new Type[0];
 
            // Methods
            if ((type & MemberTypes.Method) != 0)
                methods = GetMethodCandidates(name, bindingAttr, CallingConventions.Any, null, true);
 
            // Constructors
            if ((type & MemberTypes.Constructor) != 0) 
                constructors = GetConstructorCandidates(name, bindingAttr, CallingConventions.Any, null, true); 

            // Properties 
            if ((type & MemberTypes.Property) != 0)
                properties = GetPropertyCandidates(name, bindingAttr, null, true);

            // Events 
            if ((type & MemberTypes.Event) != 0)
                events = GetEventCandidates(name, bindingAttr, true); 
 
            // Fields
            if ((type & MemberTypes.Field) != 0) 
                fields = GetFieldCandidates(name, bindingAttr, true);

            // NestedTypes
            if ((type & (MemberTypes.NestedType | MemberTypes.TypeInfo)) != 0) 
                nestedTypes = GetNestedTypeCandidates(name, bindingAttr, true);
 
            switch(type) 
            {
                case MemberTypes.Method | MemberTypes.Constructor: 
                    MethodBase[] compressBaseses = new MethodBase[methods.Length + constructors.Length];
                    Array.Copy(methods, compressBaseses, methods.Length);
                    Array.Copy(constructors, 0, compressBaseses, methods.Length, constructors.Length);
                    return compressBaseses; 

                case MemberTypes.Method: 
                    return methods; 

                case MemberTypes.Constructor: 
                    return constructors;

                case MemberTypes.Field:
                    return fields; 

                case MemberTypes.Property: 
                    return properties; 

                case MemberTypes.Event: 
                    return events;

                case MemberTypes.NestedType:
                    return nestedTypes; 

                case MemberTypes.TypeInfo: 
                    return nestedTypes; 
            }
 
            MemberInfo[] compressMembers = new MemberInfo[
                methods.Length +
                constructors.Length +
                properties.Length + 
                events.Length +
                fields.Length + 
                nestedTypes.Length]; 

            int i = 0; 
            if (methods.Length > 0) Array.Copy(methods, 0, compressMembers, i, methods.Length); i += methods.Length;
            if (constructors.Length > 0) Array.Copy(constructors, 0, compressMembers, i, constructors.Length); i += constructors.Length;
            if (properties.Length > 0) Array.Copy(properties, 0, compressMembers, i, properties.Length); i += properties.Length;
            if (events.Length > 0) Array.Copy(events, 0, compressMembers, i, events.Length); i += events.Length; 
            if (fields.Length > 0) Array.Copy(fields, 0, compressMembers, i, fields.Length); i += fields.Length;
            if (nestedTypes.Length > 0) Array.Copy(nestedTypes, 0, compressMembers, i, nestedTypes.Length); i += nestedTypes.Length; 
 
            Contract.Assert(i == compressMembers.Length);
 
            return compressMembers;
        }
        #endregion
 
        #region Identity
        public override Module Module 
        { 
            get
            { 
                return GetRuntimeModule();
            }
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated
#if !FEATURE_CORECLR 
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")] 
#endif
        internal RuntimeModule GetRuntimeModule() 
        {
            return RuntimeTypeHandle.GetModule(this);
        }
 
        public override Assembly Assembly
        { 
            get 
            {
                return GetRuntimeAssembly(); 
            }
        }

        [System.Security.SecuritySafeCritical]  // auto-generated 
#if !FEATURE_CORECLR
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")] 
#endif 
        internal RuntimeAssembly GetRuntimeAssembly()
        { 
            return RuntimeTypeHandle.GetAssembly(this);
        }

        public override RuntimeTypeHandle TypeHandle 
        {
            get 
            { 
                return new RuntimeTypeHandle(this);
            } 
        }

        internal override bool IsRuntimeType { get { return true; } }
 
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
#if !FEATURE_CORECLR 
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")] 
#endif
        internal sealed override RuntimeTypeHandle GetTypeHandleInternal() 
        {
            return new RuntimeTypeHandle(this);
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        protected override TypeCode GetTypeCodeImpl() 
        { 
            TypeCode typeCode = Cache.TypeCode;
 
            if (typeCode != TypeCode.Empty)
                return typeCode;

            CorElementType corElementType = RuntimeTypeHandle.GetCorElementType(this); 
            switch (corElementType)
            { 
                case CorElementType.Boolean: 
                    typeCode = TypeCode.Boolean; break;
                case CorElementType.Char: 
                    typeCode = TypeCode.Char; break;
                case CorElementType.I1:
                    typeCode = TypeCode.SByte; break;
                case CorElementType.U1: 
                    typeCode = TypeCode.Byte; break;
                case CorElementType.I2: 
                    typeCode = TypeCode.Int16; break; 
                case CorElementType.U2:
                    typeCode = TypeCode.UInt16; break; 
                case CorElementType.I4:
                    typeCode = TypeCode.Int32; break;
                case CorElementType.U4:
                    typeCode = TypeCode.UInt32; break; 
                case CorElementType.I8:
                    typeCode = TypeCode.Int64; break; 
                case CorElementType.U8: 
                    typeCode = TypeCode.UInt64; break;
                case CorElementType.R4: 
                    typeCode = TypeCode.Single; break;
                case CorElementType.R8:
                    typeCode = TypeCode.Double; break;
                case CorElementType.String: 
                    typeCode = TypeCode.String; break;
                case CorElementType.ValueType: 
                    if (this == Convert.ConvertTypes[(int)TypeCode.Decimal]) 
                        typeCode = TypeCode.Decimal;
                    else if (this == Convert.ConvertTypes[(int)TypeCode.DateTime]) 
                        typeCode = TypeCode.DateTime;
                    else if (this.IsEnum)
                        typeCode = Type.GetTypeCode(Enum.GetUnderlyingType(this));
                    else 
                        typeCode = TypeCode.Object;
                    break; 
                default: 
                    if (this == Convert.ConvertTypes[(int)TypeCode.DBNull])
                        typeCode = TypeCode.DBNull; 
                    else if (this == Convert.ConvertTypes[(int)TypeCode.String])
                        typeCode = TypeCode.String;
                    else
                        typeCode = TypeCode.Object; 
                    break;
            } 
 
            Cache.TypeCode = typeCode;
 
            return typeCode;
        }

        public override MethodBase DeclaringMethod 
        {
            [System.Security.SecuritySafeCritical]  // auto-generated 
            get 
            {
                if (!IsGenericParameter) 
                    throw new InvalidOperationException(Environment.GetResourceString("Arg_NotGenericParameter"));
                Contract.EndContractBlock();

                IRuntimeMethodInfo declaringMethod = RuntimeTypeHandle.GetDeclaringMethod(this); 

                if (declaringMethod == null) 
                    return null; 

                return GetMethodBase(RuntimeMethodHandle.GetDeclaringType(declaringMethod), declaringMethod); 
            }
        }
        #endregion
 
        #region Hierarchy
        [System.Security.SecuritySafeCritical]  // auto-generated 
#if !FEATURE_CORECLR 
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif 
        public override bool IsInstanceOfType(Object o)
        {
            return RuntimeTypeHandle.IsInstanceOfType(this, o);
        } 

        [System.Runtime.InteropServices.ComVisible(true)] 
        [Pure] 
        public override bool IsSubclassOf(Type type)
        { 
            if ((object)type == null)
                throw new ArgumentNullException("type");
            Contract.EndContractBlock();
            RuntimeType rtType = type as RuntimeType; 
            if (rtType == null)
                return false; 
 
            RuntimeType baseType = GetBaseType();
 
            while (baseType != null)
            {
                if (baseType == rtType)
                    return true; 

                baseType = baseType.GetBaseType(); 
            } 

            // pretty much everything is a subclass of object, even interfaces 
            // notice that interfaces are really odd because they do not have a BaseType
            // yet IsSubclassOf(typeof(object)) returns true
            if (rtType == RuntimeType.ObjectType && rtType != this)
                return true; 

            return false; 
        } 

        [System.Security.SecuritySafeCritical] 
        public override bool IsAssignableFrom(Type c)
        {
            if ((object)c == null)
                return false; 

            if (Object.ReferenceEquals(c, this)) 
                return true; 

            RuntimeType fromType = c.UnderlyingSystemType as RuntimeType; 

            // For runtime type, let the VM decide.
            if (fromType != null)
            { 
                // both this and c (or their underlying system types) are runtime types
                return RuntimeTypeHandle.CanCastTo(fromType, this); 
            } 

            // Special case for TypeBuilder to be backward-compatible. 
            if (c is System.Reflection.Emit.TypeBuilder)
            {
                // If c is a subclass of this class, then c can be cast to this type.
                if (c.IsSubclassOf(this)) 
                    return true;
 
                if (this.IsInterface) 
                {
                    return c.ImplementInterface(this); 
                }
                else if (this.IsGenericParameter)
                {
                    Type[] constraints = GetGenericParameterConstraints(); 
                    for (int i = 0; i < constraints.Length; i++)
                        if (!constraints[i].IsAssignableFrom(c)) 
                            return false; 

                    return true; 
                }
            }

            // For anything else we return false. 
            return false;
        } 
 
#if !FEATURE_CORECLR
        // Reflexive, symmetric, transitive. 
        public override bool IsEquivalentTo(Type other)
        {
            RuntimeType otherRtType = other as RuntimeType;
            if ((object)otherRtType == null) 
                return false;
 
            if (otherRtType == this) 
                return true;
 
            // It's not worth trying to perform further checks in managed
            // as they would lead to FCalls anyway.
            return RuntimeTypeHandle.IsEquivalentTo(this, otherRtType);
        } 
#endif // FEATURE_CORECLR
 
        public override Type BaseType 
        {
            [System.Security.SecuritySafeCritical]  // auto-generated 
            get
            {
                return GetBaseType();
            } 
        }
 
        private RuntimeType GetBaseType() 
        {
            if (IsInterface) 
                return null;

            if (RuntimeTypeHandle.IsGenericVariable(this))
            { 
                Type[] constraints = GetGenericParameterConstraints();
 
                RuntimeType baseType = RuntimeType.ObjectType; 

                for (int i = 0; i < constraints.Length; i++) 
                {
                    RuntimeType constraint = (RuntimeType)constraints[i];

                    if (constraint.IsInterface) 
                        continue;
 
                    if (constraint.IsGenericParameter) 
                    {
                        GenericParameterAttributes special; 
                        special = constraint.GenericParameterAttributes & GenericParameterAttributes.SpecialConstraintMask;

                        if ((special & GenericParameterAttributes.ReferenceTypeConstraint) == 0 &&
                            (special & GenericParameterAttributes.NotNullableValueTypeConstraint) == 0) 
                            continue;
                    } 
 
                    baseType = constraint;
                } 

                if (baseType == RuntimeType.ObjectType)
                {
                    GenericParameterAttributes special; 
                    special = GenericParameterAttributes & GenericParameterAttributes.SpecialConstraintMask;
                    if ((special & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0) 
                        baseType = RuntimeType.ValueType; 
                }
 
                return baseType;
            }

            return RuntimeTypeHandle.GetBaseType(this); 
        }
 
        public override Type UnderlyingSystemType 
        {
#if !FEATURE_CORECLR 
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif
            get
            { 
                // Origional Comment: Return the underlying Type that represents the IReflect Object.
                // For expando object, this is the (Object) IReflectInstance.GetType().  For Type object it is this. 
                return this; 
            }
        } 
        #endregion

        #region Name
        public override String FullName 
        {
            [System.Security.SecuritySafeCritical]  // auto-generated 
#if !FEATURE_CORECLR 
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif 
            get
            {
                return Cache.GetFullName();
            } 
        }
 
        public override String AssemblyQualifiedName 
        {
            get 
            {
                if (!IsGenericTypeDefinition && ContainsGenericParameters)
                    return null;
 
                return Assembly.CreateQualifiedName(this.Assembly.FullName, this.FullName);
            } 
        } 

        public override String Namespace 
        {
            [System.Security.SecuritySafeCritical]  // auto-generated
#if !FEATURE_CORECLR
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")] 
#endif
            get 
            { 
                string ns = Cache.GetNameSpace();
 
                if (ns == null || ns.Length == 0)
                    return null;

                return ns; 
            }
        } 
        #endregion 

        #region Attributes 
        [System.Security.SecuritySafeCritical]  // auto-generated
#if !FEATURE_CORECLR
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif 
        protected override TypeAttributes GetAttributeFlagsImpl()
        { 
            return RuntimeTypeHandle.GetAttributes(this); 
        }
 
        public override Guid GUID
        {
            [System.Security.SecuritySafeCritical]  // auto-generated
            get 
            {
                Guid result = new Guid (); 
                GetGUID(ref result); 
                return result;
            } 
        }

        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private extern void GetGUID(ref Guid result); 
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        protected override bool IsContextfulImpl() 
        {
            return RuntimeTypeHandle.IsContextful(this);
        }
 
        /*
        protected override bool IsMarshalByRefImpl() 
        { 
            return GetTypeHandleInternal().IsMarshalByRef();
        } 
        */

#if !FEATURE_CORECLR
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")] 
#endif
        protected override bool IsByRefImpl() 
        { 
            return RuntimeTypeHandle.IsByRef(this);
        } 

        protected override bool IsPrimitiveImpl()
        {
            return RuntimeTypeHandle.IsPrimitive(this); 
        }
 
        protected override bool IsPointerImpl() 
        {
            return RuntimeTypeHandle.IsPointer(this); 
        }

        [System.Security.SecuritySafeCritical]  // auto-generated
#if !FEATURE_CORECLR 
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif 
        protected override bool IsCOMObjectImpl() 
        {
            return RuntimeTypeHandle.IsComObject(this, false); 
        }

        [System.Security.SecuritySafeCritical]  // auto-generated
        internal override bool HasProxyAttributeImpl() 
        {
            return RuntimeTypeHandle.HasProxyAttribute(this); 
        } 

#if !FEATURE_CORECLR 
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif
        protected override bool IsValueTypeImpl()
        { 
            // We need to return true for generic parameters with the ValueType constraint.
            // So we cannot use the faster RuntimeTypeHandle.IsValueType because it returns 
            // false for all generic parameters. 
            if (this == typeof(ValueType) || this == typeof(Enum))
                return false; 

            return IsSubclassOf(typeof(ValueType));
        }
 
        public override bool IsEnum
        { 
#if !FEATURE_CORECLR 
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif 
            get
            {
                return GetBaseType() == RuntimeType.EnumType;
            } 
        }
 
#if !FEATURE_CORECLR 
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif 
        protected override bool HasElementTypeImpl()
        {
            return RuntimeTypeHandle.HasElementType(this);
        } 

        public override GenericParameterAttributes GenericParameterAttributes 
        { 
            [System.Security.SecuritySafeCritical]  // auto-generated
            get 
            {
                if (!IsGenericParameter)
                    throw new InvalidOperationException(Environment.GetResourceString("Arg_NotGenericParameter"));
                Contract.EndContractBlock(); 

                GenericParameterAttributes attributes; 
 
                RuntimeTypeHandle.GetMetadataImport(this).GetGenericParamProps(MetadataToken, out attributes);
 
                return attributes;
            }
        }
 
        public override bool IsSecurityCritical
        { 
#if !FEATURE_CORECLR 
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif 
            get { return new RuntimeTypeHandle(this).IsSecurityCritical(); }
        }
        public override bool IsSecuritySafeCritical
        { 
#if !FEATURE_CORECLR
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")] 
#endif 
            get { return new RuntimeTypeHandle(this).IsSecuritySafeCritical(); }
        } 
        public override bool IsSecurityTransparent
        {
#if !FEATURE_CORECLR
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")] 
#endif
            get { return new RuntimeTypeHandle(this).IsSecurityTransparent(); } 
        } 
        #endregion
 
        #region Arrays
        internal override bool IsSzArray
        {
            get 
            {
                return RuntimeTypeHandle.IsSzArray(this); 
            } 
        }
 
#if !FEATURE_CORECLR
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif
        protected override bool IsArrayImpl() 
        {
            return RuntimeTypeHandle.IsArray(this); 
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        public override int GetArrayRank()
        {
            if (!IsArrayImpl())
                throw new ArgumentException(Environment.GetResourceString("Argument_HasToBeArrayClass")); 

            return RuntimeTypeHandle.GetArrayRank(this); 
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated 
#if !FEATURE_CORECLR
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif
        public override Type GetElementType() 
        {
            return RuntimeTypeHandle.GetElementType(this); 
        } 
        #endregion
 
        #region Enums
        public override string[] GetEnumNames()
        {
            if (!IsEnum) 
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeEnum"), "enumType");
 
            String[] ret = Enum.InternalGetNames(this); 

            // Make a copy since we can't hand out the same array since users can modify them 
            String[] retVal = new String[ret.Length];

            Array.Copy(ret, retVal, ret.Length);
 
            return retVal;
        } 
 
        [SecuritySafeCritical]
        public override Array GetEnumValues() 
        {
            if (!IsEnum)
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeEnum"), "enumType");
 
            // Get all of the values
            ulong[] values = Enum.InternalGetValues(this); 
 
            // Create a generic Array
            Array ret = Array.UnsafeCreateInstance(this, values.Length); 

            for (int i = 0; i < values.Length; i++)
            {
                Object val = Enum.ToObject(this, values[i]); 
                ret.SetValue(val, i);
            } 
 
            return ret;
        } 

        [SecuritySafeCritical]
        public override Type GetEnumUnderlyingType()
        { 
            if (!IsEnum)
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeEnum"), "enumType"); 
 
            return Enum.InternalGetUnderlyingType(this);
        } 

        public override bool IsEnumDefined(object value)
        {
            if (value == null) 
                throw new ArgumentNullException("value");
            Contract.EndContractBlock(); 
 
            // Check if both of them are of the same type
            RuntimeType valueType = (RuntimeType)value.GetType(); 

            // If the value is an Enum then we need to extract the underlying value from it
            if (valueType.IsEnum)
            { 
                if (!valueType.IsEquivalentTo(this))
                    throw new ArgumentException(Environment.GetResourceString("Arg_EnumAndObjectMustBeSameType", valueType.ToString(), this.ToString())); 
 
                valueType = (RuntimeType)valueType.GetEnumUnderlyingType();
            } 

            // If a string is passed in
            if (valueType == RuntimeType.StringType)
            { 
                // Get all of the Fields, calling GetHashEntry directly to avoid copying
                string[] names = Enum.InternalGetNames(this); 
                if (Array.IndexOf(names, value) >= 0) 
                    return true;
                else 
                    return false;
            }

            // If an enum or integer value is passed in 
            if (Type.IsIntegerType(valueType))
            { 
                RuntimeType underlyingType = Enum.InternalGetUnderlyingType(this); 
                if (underlyingType != valueType)
                    throw new ArgumentException(Environment.GetResourceString("Arg_EnumUnderlyingTypeAndObjectMustBeSameType", valueType.ToString(), underlyingType.ToString())); 

                ulong[] ulValues = Enum.InternalGetValues(this);
                ulong ulValue = Enum.ToUInt64(value);
 
                return (Array.BinarySearch(ulValues, ulValue) >= 0);
            } 
 
            throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_UnknownEnumType"));
        } 

        public override string GetEnumName(object value)
        {
            if (value == null) 
                throw new ArgumentNullException("value");
            Contract.EndContractBlock(); 
 
            Type valueType = value.GetType();
 
            if (!(valueType.IsEnum || IsIntegerType(valueType)))
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeEnumBaseTypeOrEnum"), "value");

            ulong[] ulValues = Enum.InternalGetValues(this); 
            ulong ulValue = Enum.ToUInt64(value);
            int index = Array.BinarySearch(ulValues, ulValue); 
 
            if (index >= 0)
            { 
                string[] names = Enum.InternalGetNames(this);
                return names[index];
            }
 
            return null;
        } 
        #endregion 

        #region Generics 
        internal RuntimeType[] GetGenericArgumentsInternal()
        {
            return GetRootElementType().GetTypeHandleInternal().GetInstantiationInternal();
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        public override Type[] GetGenericArguments() 
        {
            Type[] types = GetRootElementType().GetTypeHandleInternal().GetInstantiationPublic(); 

            if (types == null)
                types = new Type[0];
 
            return types;
        } 
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public override Type MakeGenericType(Type[] instantiation) 
        {
            if (instantiation == null)
                throw new ArgumentNullException("instantiation");
            Contract.EndContractBlock(); 

            RuntimeType[] instantiationRuntimeType = new RuntimeType[instantiation.Length]; 
 
            if (!IsGenericTypeDefinition)
                throw new InvalidOperationException( 
                    Environment.GetResourceString("Arg_NotGenericTypeDefinition", this));

            if (GetGenericArguments().Length != instantiation.Length)
                throw new ArgumentException(Environment.GetResourceString("Argument_GenericArgsCount"), "instantiation"); 

            for (int i = 0; i < instantiation.Length; i ++) 
            { 
                Type instantiationElem = instantiation[i];
                if (instantiationElem == null) 
                    throw new ArgumentNullException();

                RuntimeType rtInstantiationElem = instantiationElem as RuntimeType;
 
                if (rtInstantiationElem == null)
                { 
#if FEATURE_REFLECTION_EMIT_REFACTORING 
                    throw new ArgumentException(Environment.GetResourceString("Arg_MustAllBeRuntimeType"));
#else //FEATURE_REFLECTION_EMIT_REFACTORING 
                    Type[] instantiationCopy = new Type[instantiation.Length];
                    for (int iCopy = 0; iCopy < instantiation.Length; iCopy++)
                        instantiationCopy[iCopy] = instantiation[iCopy];
                    instantiation = instantiationCopy; 
                    return System.Reflection.Emit.TypeBuilderInstantiation.MakeGenericType(this, instantiation);
#endif //FEATURE_REFLECTION_EMIT_REFACTORING 
                } 

                instantiationRuntimeType[i] = rtInstantiationElem; 
            }

            RuntimeType[] genericParameters = GetGenericArgumentsInternal();
 
            SanityCheckGenericArguments(instantiationRuntimeType, genericParameters);
 
            Type ret = null; 
            try
            { 
                ret = new RuntimeTypeHandle(this).Instantiate(instantiationRuntimeType);
            }
            catch (TypeLoadException e)
            { 
                ValidateGenericArguments(this, instantiationRuntimeType, e);
                throw e; 
            } 

            return ret; 
        }

        public override bool IsGenericTypeDefinition
        { 
#if !FEATURE_CORECLR
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")] 
#endif 
            get { return RuntimeTypeHandle.IsGenericTypeDefinition(this); }
        } 

        public override bool IsGenericParameter
        {
#if !FEATURE_CORECLR 
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif 
            get { return RuntimeTypeHandle.IsGenericVariable(this); } 
        }
 
        public override int GenericParameterPosition
        {
            [System.Security.SecuritySafeCritical]  // auto-generated
            get 
            {
                if (!IsGenericParameter) 
                    throw new InvalidOperationException(Environment.GetResourceString("Arg_NotGenericParameter")); 
                Contract.EndContractBlock();
 
                return new RuntimeTypeHandle(this).GetGenericVariableIndex();
            }
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated
#if !FEATURE_CORECLR 
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")] 
#endif
        public override Type GetGenericTypeDefinition() 
        {
            if (!IsGenericType)
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_NotGenericType"));
            Contract.EndContractBlock(); 

            return RuntimeTypeHandle.GetGenericTypeDefinition(this); 
        } 

        public override bool IsGenericType 
        {
#if !FEATURE_CORECLR
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif 
            get { return RuntimeTypeHandle.IsGenericType(this); }
        } 
 
        public override bool ContainsGenericParameters
        { 
            [System.Security.SecuritySafeCritical]  // auto-generated
#if !FEATURE_CORECLR
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif 
            get { return GetRootElementType().GetTypeHandleInternal().ContainsGenericVariables(); }
        } 
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public override Type[] GetGenericParameterConstraints() 
        {
            if (!IsGenericParameter)
                throw new InvalidOperationException(Environment.GetResourceString("Arg_NotGenericParameter"));
            Contract.EndContractBlock(); 

            Type[] constraints = new RuntimeTypeHandle(this).GetConstraints(); 
 
            if (constraints == null)
                constraints = new Type[0]; 

            return constraints;
        }
        #endregion 

        #region Misc 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        public override Type MakePointerType() { return new RuntimeTypeHandle(this).MakePointer(); }
        [System.Security.SecuritySafeCritical]  // auto-generated 
        public override Type MakeByRefType() { return new RuntimeTypeHandle(this).MakeByRef(); }
        [System.Security.SecuritySafeCritical]  // auto-generated
        public override Type MakeArrayType() { return new RuntimeTypeHandle(this).MakeSZArray(); }
        [System.Security.SecuritySafeCritical]  // auto-generated 
        public override Type MakeArrayType(int rank)
        { 
            if (rank <= 0) 
                throw new IndexOutOfRangeException();
            Contract.EndContractBlock(); 

            return new RuntimeTypeHandle(this).MakeArray(rank);
        }
        public override StructLayoutAttribute StructLayoutAttribute 
        {
            [System.Security.SecuritySafeCritical] // overrides transparent public member 
            get 
            {
                return (StructLayoutAttribute)StructLayoutAttribute.GetCustomAttribute(this); 
            }
        }
        #endregion
 
        #region Invoke Member
        private const BindingFlags MemberBindingMask        = (BindingFlags)0x000000FF; 
        private const BindingFlags InvocationMask           = (BindingFlags)0x0000FF00; 
        private const BindingFlags BinderNonCreateInstance  = BindingFlags.InvokeMethod | BinderGetSetField | BinderGetSetProperty;
        private const BindingFlags BinderGetSetProperty     = BindingFlags.GetProperty | BindingFlags.SetProperty; 
        private const BindingFlags BinderSetInvokeProperty  = BindingFlags.InvokeMethod | BindingFlags.SetProperty;
        private const BindingFlags BinderGetSetField        = BindingFlags.GetField | BindingFlags.SetField;
        private const BindingFlags BinderSetInvokeField     = BindingFlags.SetField | BindingFlags.InvokeMethod;
        private const BindingFlags BinderNonFieldGetSet     = (BindingFlags)0x00FFF300; 
        private const BindingFlags ClassicBindingMask       =
            BindingFlags.InvokeMethod | BindingFlags.GetProperty | BindingFlags.SetProperty | 
            BindingFlags.PutDispProperty | BindingFlags.PutRefDispProperty; 
        private static RuntimeType s_typedRef = (RuntimeType)typeof(TypedReference);
 
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        static private extern bool CanValueSpecialCast(RuntimeType valueType, RuntimeType targetType); 

        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        static private extern Object AllocateValueType(RuntimeType type, object value, bool fForceTypeChange); 

        [System.Security.SecuritySafeCritical]  // auto-generated
        internal unsafe Object CheckValue(Object value, Binder binder, CultureInfo culture, BindingFlags invokeAttr)
        { 
            // this method is used by invocation in reflection to check whether a value can be assigned to type.
            if (IsInstanceOfType(value)) 
            { 
                // Since this cannot be a generic parameter, we use RuntimeTypeHandle.IsValueType here
                // because it is faster than RuntimeType.IsValueType 
                Contract.Assert(!IsGenericParameter);
                if (!Object.ReferenceEquals(value.GetType(), this) && RuntimeTypeHandle.IsValueType(this))
                {
                    // must be an equivalent type, re-box to the target type 
                    return AllocateValueType(TypeHandle.GetRuntimeType(), value, true);
                } 
                else 
                {
                    return value; 
                }
            }

            // if this is a ByRef get the element type and check if it's compatible 
            bool isByRef = IsByRef;
            if (isByRef) 
            { 
                Type elementType = GetElementType();
                if (elementType.IsInstanceOfType(value) || value == null) 
                {
                    // need to create an instance of the ByRef if null was provided, but only if primitive, enum or value type
                    return AllocateValueType(elementType.TypeHandle.GetRuntimeType(), value, false);
                } 
            }
            else if (value == null) 
                return value; 
            else if (this == s_typedRef)
                // everything works for a typedref 
                return value;

            // check the strange ones courtesy of reflection:
            // - implicit cast between primitives 
            // - enum treated as underlying type
            // - IntPtr and System.Reflection.Pointer to pointer types 
            bool needsSpecialCast = IsPointer || IsEnum || IsPrimitive; 
            if (needsSpecialCast)
            { 
                RuntimeType valueType;
                Pointer pointer = value as Pointer;
                if (pointer != null)
                    valueType = pointer.GetPointerType(); 
                else
                    valueType = (RuntimeType)value.GetType(); 
 
                if (CanValueSpecialCast(valueType, this))
                { 
                    if (pointer != null)
                        return pointer.GetPointerValue();
                    else
                        return value; 
                }
            } 
 
            if ((invokeAttr & BindingFlags.ExactBinding) == BindingFlags.ExactBinding)
                throw new ArgumentException(String.Format(CultureInfo.CurrentUICulture, Environment.GetResourceString("Arg_ObjObjEx"), value.GetType(), this)); 

            return TryChangeType(value, binder, culture, needsSpecialCast);
        }
 
        // Factored out of CheckValue to reduce code complexity.
        [System.Security.SecurityCritical] 
        private Object TryChangeType(Object value, Binder binder, CultureInfo culture, bool needsSpecialCast) 
        {
            if (binder != null && binder != Type.DefaultBinder) 
            {
                value = binder.ChangeType(value, this, culture);
                if (IsInstanceOfType(value))
                    return value; 
                // if this is a ByRef get the element type and check if it's compatible
                if (IsByRef) 
                { 
                    Type elementType = GetElementType();
                    if (elementType.IsInstanceOfType(value) || value == null) 
                        return AllocateValueType(elementType.TypeHandle.GetRuntimeType(), value, false);
                }
                else if (value == null)
                    return value; 
                if (needsSpecialCast)
                { 
                    RuntimeType valueType; 
                    Pointer pointer = value as Pointer;
                    if (pointer != null) 
                        valueType = pointer.GetPointerType();
                    else
                        valueType = (RuntimeType)value.GetType();
 
                    if (CanValueSpecialCast(valueType, this))
                    { 
                        if (pointer != null) 
                            return pointer.GetPointerValue();
                        else 
                            return value;
                    }
                }
            } 

            throw new ArgumentException(String.Format(CultureInfo.CurrentUICulture, Environment.GetResourceString("Arg_ObjObjEx"), value.GetType(), this)); 
        } 

        [System.Security.SecurityCritical]  // auto-generated 
        internal virtual String GetDefaultMemberName()
        {
            // See if we have cached the default member name
            String defaultMember = (String)this.RemotingCache[CacheObjType.DefaultMember]; 

            if (defaultMember == null) 
            { 
                Object[] attrs = GetCustomAttributes(typeof(DefaultMemberAttribute), true);
                // We assume that there is only one DefaultMemberAttribute (Allow multiple = false) 
                if (attrs.Length > 1)
                    throw new InvalidProgramException(Environment.GetResourceString("ExecutionEngine_InvalidAttribute"));
                if (attrs.Length == 0)
                    return null; 
                defaultMember = ((DefaultMemberAttribute)attrs[0]).MemberName;
                this.RemotingCache[CacheObjType.DefaultMember] = defaultMember; 
            } 
            return defaultMember;
        } 

        // GetDefaultMembers
        // This will return a MemberInfo that has been marked with the
        //      DefaultMemberAttribute 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public override MemberInfo[] GetDefaultMembers() 
        { 
            // See if we have cached the default member name
            String defaultMember = (String)this.RemotingCache[CacheObjType.DefaultMember]; 

            if (defaultMember == null)
            {
                // Get all of the custom attributes 

                CustomAttributeData attr = null; 
 
                Type DefaultMemberAttrType = typeof(DefaultMemberAttribute);
                for (RuntimeType t = this; t != null; t = t.GetBaseType()) 
                {
                    IList<CustomAttributeData> attrs = CustomAttributeData.GetCustomAttributes(t);
                    for (int i = 0; i < attrs.Count; i++)
                    { 
                        if (Object.ReferenceEquals(attrs[i].Constructor.DeclaringType, DefaultMemberAttrType))
                        { 
                            attr = attrs[i]; 
                            break;
                        } 
                    }

                    if (attr != null)
                        break; 
                }
 
                if (attr == null) 
                    return new MemberInfo[0];
                defaultMember = attr.ConstructorArguments[0].Value as string; 
                this.RemotingCache[CacheObjType.DefaultMember] = defaultMember;
            }

            MemberInfo[] members = GetMember(defaultMember); 
            if (members == null)
                members = new MemberInfo[0]; 
            return members; 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        [DebuggerStepThroughAttribute]
        [Diagnostics.DebuggerHidden]
        public override Object InvokeMember( 
            String name, BindingFlags bindingFlags, Binder binder, Object target,
            Object[] providedArgs, ParameterModifier[] modifiers, CultureInfo culture, String[] namedParams) 
        { 
            if (IsGenericParameter)
                throw new InvalidOperationException(Environment.GetResourceString("Arg_GenericParameter")); 
            Contract.EndContractBlock();

            #region Preconditions
            if ((bindingFlags & InvocationMask) == 0) 
                // "Must specify binding flags describing the invoke operation required."
                throw new ArgumentException(Environment.GetResourceString("Arg_NoAccessSpec"),"bindingFlags"); 
 
            // Provide a default binding mask if none is provided
            if ((bindingFlags & MemberBindingMask) == 0) 
            {
                bindingFlags |= BindingFlags.Instance | BindingFlags.Public;

                if ((bindingFlags & BindingFlags.CreateInstance) == 0) 
                    bindingFlags |= BindingFlags.Static;
            } 
 
            // There must not be more named parameters than provided arguments
            if (namedParams != null) 
            {
                if (providedArgs != null)
                {
                    if (namedParams.Length > providedArgs.Length) 
                        // "Named parameter array can not be bigger than argument array."
                        throw new ArgumentException(Environment.GetResourceString("Arg_NamedParamTooBig"), "namedParams"); 
                } 
                else
                { 
                    if (namedParams.Length != 0)
                        // "Named parameter array can not be bigger than argument array."
                        throw new ArgumentException(Environment.GetResourceString("Arg_NamedParamTooBig"), "namedParams");
                } 
            }
            #endregion 
 
            #region COM Interop
#if FEATURE_COMINTEROP 
            if (target != null && target.GetType().IsCOMObject)
            {
                #region Preconditions
                if ((bindingFlags & ClassicBindingMask) == 0) 
                    throw new ArgumentException(Environment.GetResourceString("Arg_COMAccess"), "bindingFlags");
 
                if ((bindingFlags & BindingFlags.GetProperty) != 0 && (bindingFlags & ClassicBindingMask & ~(BindingFlags.GetProperty | BindingFlags.InvokeMethod)) != 0) 
                    throw new ArgumentException(Environment.GetResourceString("Arg_PropSetGet"), "bindingFlags");
 
                if ((bindingFlags & BindingFlags.InvokeMethod) != 0 && (bindingFlags & ClassicBindingMask & ~(BindingFlags.GetProperty | BindingFlags.InvokeMethod)) != 0)
                    throw new ArgumentException(Environment.GetResourceString("Arg_PropSetInvoke"), "bindingFlags");

                if ((bindingFlags & BindingFlags.SetProperty) != 0 && (bindingFlags & ClassicBindingMask & ~BindingFlags.SetProperty) != 0) 
                    throw new ArgumentException(Environment.GetResourceString("Arg_COMPropSetPut"), "bindingFlags");
 
                if ((bindingFlags & BindingFlags.PutDispProperty) != 0 && (bindingFlags & ClassicBindingMask & ~BindingFlags.PutDispProperty) != 0) 
                    throw new ArgumentException(Environment.GetResourceString("Arg_COMPropSetPut"), "bindingFlags");
 
                if ((bindingFlags & BindingFlags.PutRefDispProperty) != 0 && (bindingFlags & ClassicBindingMask & ~BindingFlags.PutRefDispProperty) != 0)
                    throw new ArgumentException(Environment.GetResourceString("Arg_COMPropSetPut"), "bindingFlags");
                #endregion
 
                if(!RemotingServices.IsTransparentProxy(target))
                { 
                    #region Non-TransparentProxy case 
                    if (name == null)
                        throw new ArgumentNullException("name"); 

                    bool[] isByRef = modifiers == null ? null : modifiers[0].IsByRefArray;

                    // pass LCID_ENGLISH_US if no explicit culture is specified to match the behavior of VB 
                    int lcid = (culture == null ? 0x0409 : culture.LCID);
 
                    return InvokeDispMethod(name, bindingFlags, target, providedArgs, isByRef, lcid, namedParams); 
                    #endregion
                } 
                else
                {
                    #region TransparentProxy case
                    return ((MarshalByRefObject)target).InvokeMember(name, bindingFlags, binder, providedArgs, modifiers, culture, namedParams); 
                    #endregion
                } 
            } 
#endif // FEATURE_COMINTEROP
            #endregion 

            #region Check that any named paramters are not null
            if (namedParams != null && Array.IndexOf(namedParams, null) != -1)
                // "Named parameter value must not be null." 
                throw new ArgumentException(Environment.GetResourceString("Arg_NamedParamNull"),"namedParams");
            #endregion 
 
            int argCnt = (providedArgs != null) ? providedArgs.Length : 0;
 
            #region Get a Binder
            if (binder == null)
                binder = DefaultBinder;
 
            bool bDefaultBinder = (binder == DefaultBinder);
            #endregion 
 
            #region Delegate to Activator.CreateInstance
            if ((bindingFlags & BindingFlags.CreateInstance) != 0) 
            {
                if ((bindingFlags & BindingFlags.CreateInstance) != 0 && (bindingFlags & BinderNonCreateInstance) != 0)
                    // "Can not specify both CreateInstance and another access type."
                    throw new ArgumentException(Environment.GetResourceString("Arg_CreatInstAccess"),"bindingFlags"); 

                return Activator.CreateInstance(this, bindingFlags, binder, providedArgs, culture); 
            } 
            #endregion
 
            // PutDispProperty and\or PutRefDispProperty ==> SetProperty.
            if ((bindingFlags & (BindingFlags.PutDispProperty | BindingFlags.PutRefDispProperty)) != 0)
                bindingFlags |= BindingFlags.SetProperty;
 
            #region Name
            if (name == null) 
                throw new ArgumentNullException("name"); 

            if (name.Length == 0 || name.Equals(@"[DISPID=0]")) 
            {
                name = GetDefaultMemberName();

                if (name == null) 
                {
                    // in InvokeMember we always pretend there is a default member if none is provided and we make it ToString 
                    name = "ToString"; 
                }
            } 
            #endregion

            #region GetField or SetField
            bool IsGetField = (bindingFlags & BindingFlags.GetField) != 0; 
            bool IsSetField = (bindingFlags & BindingFlags.SetField) != 0;
 
            if (IsGetField || IsSetField) 
            {
                #region Preconditions 
                if (IsGetField)
                {
                    if (IsSetField)
                        // "Can not specify both Get and Set on a field." 
                        throw new ArgumentException(Environment.GetResourceString("Arg_FldSetGet"),"bindingFlags");
 
                    if ((bindingFlags & BindingFlags.SetProperty) != 0) 
                        // "Can not specify both GetField and SetProperty."
                        throw new ArgumentException(Environment.GetResourceString("Arg_FldGetPropSet"),"bindingFlags"); 
                }
                else
                {
                    Contract.Assert(IsSetField); 

                    if (providedArgs == null) 
                        throw new ArgumentNullException("providedArgs"); 

                    if ((bindingFlags & BindingFlags.GetProperty) != 0) 
                        // "Can not specify both SetField and GetProperty."
                        throw new ArgumentException(Environment.GetResourceString("Arg_FldSetPropGet"),"bindingFlags");

                    if ((bindingFlags & BindingFlags.InvokeMethod) != 0) 
                        // "Can not specify Set on a Field and Invoke on a method."
                        throw new ArgumentException(Environment.GetResourceString("Arg_FldSetInvoke"),"bindingFlags"); 
                } 
                #endregion
 
                #region Lookup Field
                FieldInfo selFld = null;
                FieldInfo[] flds = GetMember(name, MemberTypes.Field, bindingFlags) as FieldInfo[];
 
                Contract.Assert(flds != null);
 
                if (flds.Length == 1) 
                {
                    selFld = flds[0]; 
                }
                else if (flds.Length > 0)
                {
                    selFld = binder.BindToField(bindingFlags, flds, IsGetField ? Empty.Value : providedArgs[0], culture); 
                }
                #endregion 
 
                if (selFld != null)
                { 
                    #region Invocation on a field
                    if (selFld.FieldType.IsArray || Object.ReferenceEquals(selFld.FieldType, typeof(System.Array)))
                    {
                        #region Invocation of an array Field 
                        int idxCnt;
 
                        if ((bindingFlags & BindingFlags.GetField) != 0) 
                        {
                            idxCnt = argCnt; 
                        }
                        else
                        {
                            idxCnt = argCnt - 1; 
                        }
 
                        if (idxCnt > 0) 
                        {
                            // Verify that all of the index values are ints 
                            int[] idx = new int[idxCnt];
                            for (int i=0;i<idxCnt;i++)
                            {
                                try 
                                {
                                    idx[i] = ((IConvertible)providedArgs[i]).ToInt32(null); 
                                } 
                                catch (InvalidCastException)
                                { 
                                    throw new ArgumentException(Environment.GetResourceString("Arg_IndexMustBeInt"));
                                }
                            }
 
                            // Set or get the value...
                            Array a = (Array) selFld.GetValue(target); 
 
                            // Set or get the value in the array
                            if ((bindingFlags & BindingFlags.GetField) != 0) 
                            {
                                return a.GetValue(idx);
                            }
                            else 
                            {
                                a.SetValue(providedArgs[idxCnt],idx); 
                                return null; 
                            }
                        } 
                        #endregion
                    }

                    if (IsGetField) 
                    {
                        #region Get the field value 
                        if (argCnt != 0) 
                            throw new ArgumentException(Environment.GetResourceString("Arg_FldGetArgErr"),"bindingFlags");
 
                        return selFld.GetValue(target);
                        #endregion
                    }
                    else 
                    {
                        #region Set the field Value 
                        if (argCnt != 1) 
                            throw new ArgumentException(Environment.GetResourceString("Arg_FldSetArgErr"),"bindingFlags");
 
                        selFld.SetValue(target,providedArgs[0],bindingFlags,binder,culture);

                        return null;
                        #endregion 
                    }
                    #endregion 
                } 

                if ((bindingFlags & BinderNonFieldGetSet) == 0) 
                    throw new MissingFieldException(FullName, name);
            }
            #endregion
 
            #region Caching Logic
            /* 
            bool useCache = false; 

            // Note that when we add something to the cache, we are careful to ensure 
            // that the actual providedArgs matches the parameters of the method.  Otherwise,
            // some default argument processing has occurred.  We don't want anyone
            // else with the same (insufficient) number of actual arguments to get a
            // cache hit because then they would bypass the default argument processing 
            // and the invocation would fail.
            if (bDefaultBinder && namedParams == null && argCnt < 6) 
                useCache = true; 

            if (useCache) 
            {
                MethodBase invokeMethod = GetMethodFromCache (name, bindingFlags, argCnt, providedArgs);

                if (invokeMethod != null) 
                    return ((MethodInfo) invokeMethod).Invoke(target, bindingFlags, binder, providedArgs, culture);
            } 
            */ 
            #endregion
 
            #region Property PreConditions
            // @Legacy - This is RTM behavior
            bool isGetProperty = (bindingFlags & BindingFlags.GetProperty) != 0;
            bool isSetProperty = (bindingFlags & BindingFlags.SetProperty) != 0; 

            if (isGetProperty || isSetProperty) 
            { 
                #region Preconditions
                if (isGetProperty) 
                {
                    Contract.Assert(!IsSetField);

                    if (isSetProperty) 
                        throw new ArgumentException(Environment.GetResourceString("Arg_PropSetGet"), "bindingFlags");
                } 
                else 
                {
                    Contract.Assert(isSetProperty); 

                    Contract.Assert(!IsGetField);

                    if ((bindingFlags & BindingFlags.InvokeMethod) != 0) 
                        throw new ArgumentException(Environment.GetResourceString("Arg_PropSetInvoke"), "bindingFlags");
                } 
                #endregion 
            }
            #endregion 

            MethodInfo[] finalists = null;
            MethodInfo finalist = null;
 
            #region BindingFlags.InvokeMethod
            if ((bindingFlags & BindingFlags.InvokeMethod) != 0) 
            { 
                #region Lookup Methods
                MethodInfo[] semiFinalists = GetMember(name, MemberTypes.Method, bindingFlags) as MethodInfo[]; 
                List<MethodInfo> results = null;

                for(int i = 0; i < semiFinalists.Length; i ++)
                { 
                    MethodInfo semiFinalist = semiFinalists[i];
                    Contract.Assert(semiFinalist != null); 
 
                    if (!FilterApplyMethodInfo((RuntimeMethodInfo)semiFinalist, bindingFlags, CallingConventions.Any, new Type[argCnt]))
                        continue; 

                    if (finalist == null)
                    {
                        finalist = semiFinalist; 
                    }
                    else 
                    { 
                        if (results == null)
                        { 
                            results = new List<MethodInfo>(semiFinalists.Length);
                            results.Add(finalist);
                        }
 
                        results.Add(semiFinalist);
                    } 
                } 

                if (results != null) 
                {
                    Contract.Assert(results.Count > 1);
                    finalists = new MethodInfo[results.Count];
                    results.CopyTo(finalists); 
                }
                #endregion 
            } 
            #endregion
 
            Contract.Assert(finalists == null || finalist != null);

            #region BindingFlags.GetProperty or BindingFlags.SetProperty
            if (finalist == null && isGetProperty || isSetProperty) 
            {
                #region Lookup Property 
                PropertyInfo[] semiFinalists = GetMember(name, MemberTypes.Property, bindingFlags) as PropertyInfo[]; 
                List<MethodInfo> results = null;
 
                for(int i = 0; i < semiFinalists.Length; i ++)
                {
                    MethodInfo semiFinalist = null;
 
                    if (isSetProperty)
                    { 
                        semiFinalist = semiFinalists[i].GetSetMethod(true); 
                    }
                    else 
                    {
                        semiFinalist = semiFinalists[i].GetGetMethod(true);
                    }
 
                    if (semiFinalist == null)
                        continue; 
 
                    if (!FilterApplyMethodInfo((RuntimeMethodInfo)semiFinalist, bindingFlags, CallingConventions.Any, new Type[argCnt]))
                        continue; 

                    if (finalist == null)
                    {
                        finalist = semiFinalist; 
                    }
                    else 
                    { 
                        if (results == null)
                        { 
                            results = new List<MethodInfo>(semiFinalists.Length);
                            results.Add(finalist);
                        }
 
                        results.Add(semiFinalist);
                    } 
                } 

                if (results != null) 
                {
                    Contract.Assert(results.Count > 1);
                    finalists = new MethodInfo[results.Count];
                    results.CopyTo(finalists); 
                }
                #endregion 
            } 
            #endregion
 
            if (finalist != null)
            {
                #region Invoke
                if (finalists == null && 
                    argCnt == 0 &&
                    finalist.GetParametersNoCopy().Length == 0 && 
                    (bindingFlags & BindingFlags.OptionalParamBinding) == 0) 
                {
                    //if (useCache && argCnt == props[0].GetParameters().Length) 
                    //    AddMethodToCache(name, bindingFlags, argCnt, providedArgs, props[0]);

                    return finalist.Invoke(target, bindingFlags, binder, providedArgs, culture);
                } 

                if (finalists == null) 
                    finalists = new MethodInfo[] { finalist }; 

                if (providedArgs == null) 
                        providedArgs = new Object[0];

                Object state = null;
 

                MethodBase invokeMethod = null; 
 
                try { invokeMethod = binder.BindToMethod(bindingFlags, finalists, ref providedArgs, modifiers, culture, namedParams, out state); }
                catch(MissingMethodException) { } 

                if (invokeMethod == null)
                    throw new MissingMethodException(FullName, name);
 
                //if (useCache && argCnt == invokeMethod.GetParameters().Length)
                //    AddMethodToCache(name, bindingFlags, argCnt, providedArgs, invokeMethod); 
 
                Object result = ((MethodInfo)invokeMethod).Invoke(target, bindingFlags, binder, providedArgs, culture);
 
                if (state != null)
                    binder.ReorderArgumentArray(ref providedArgs, state);

                return result; 
                #endregion
            } 
 
            throw new MissingMethodException(FullName, name);
        } 
        #endregion

        #endregion
 
        #region Object Overrides
#if !FEATURE_CORECLR 
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")] 
#endif
        public override bool Equals(object obj) 
        {
            // ComObjects are identified by the instance of the Type object and not the TypeHandle.
            return obj == (object)this;
        } 

#if !FEATURE_CORECLR 
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")] 
#endif
        public override int GetHashCode() 
        {
            return RuntimeHelpers.GetHashCode(this);
        }
 
#if !FEATURE_CORECLR
        public static bool operator ==(RuntimeType left, RuntimeType right) 
        { 
            return object.ReferenceEquals(left, right);
        } 

        public static bool operator !=(RuntimeType left, RuntimeType right)
        {
            return !object.ReferenceEquals(left, right); 
        }
#endif // !FEATURE_CORECLR 
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public override String ToString() 
        {
            return Cache.GetToString();
        }
        #endregion 

        #region ICloneable 
        public Object Clone() 
        {
            return this; 
        }
        #endregion

        #region ISerializable 
        [System.Security.SecurityCritical]  // auto-generated
        public void GetObjectData(SerializationInfo info, StreamingContext context) 
        { 
            if (info==null)
                throw new ArgumentNullException("info"); 
            Contract.EndContractBlock();

            UnitySerializationHolder.GetUnitySerializationInfo(info, this);
        } 
        #endregion
 
        #region ICustomAttributeProvider 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public override Object[] GetCustomAttributes(bool inherit) 
        {
            return CustomAttribute.GetCustomAttributes(this, RuntimeType.ObjectType, inherit);
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public override Object[] GetCustomAttributes(Type attributeType, bool inherit) 
        { 
            if ((object)attributeType == null)
                throw new ArgumentNullException("attributeType"); 
            Contract.EndContractBlock();

            RuntimeType attributeRuntimeType = attributeType.UnderlyingSystemType as RuntimeType;
 
            if (attributeRuntimeType == null)
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeType"),"attributeType"); 
 
            return CustomAttribute.GetCustomAttributes(this, attributeRuntimeType, inherit);
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated
        public override bool IsDefined(Type attributeType, bool inherit)
        { 
            if ((object)attributeType == null)
                throw new ArgumentNullException("attributeType"); 
            Contract.EndContractBlock(); 

            RuntimeType attributeRuntimeType = attributeType.UnderlyingSystemType as RuntimeType; 

            if (attributeRuntimeType == null)
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeType"),"attributeType");
 
            return CustomAttribute.IsDefined(this, attributeRuntimeType, inherit);
        } 
 
        public override IList<CustomAttributeData> GetCustomAttributesData()
        { 
            return CustomAttributeData.GetCustomAttributesInternal(this);
        }
        #endregion
 
        #region MemberInfo Overrides
        public override String Name 
        { 
            [System.Security.SecuritySafeCritical]  // auto-generated
#if !FEATURE_CORECLR 
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif
            get
            { 
                return Cache.GetName();
            } 
        } 

        public override MemberTypes MemberType 
        {
            get
            {
                if (this.IsPublic || this.IsNotPublic) 
                    return MemberTypes.TypeInfo;
                else 
                    return MemberTypes.NestedType; 
            }
        } 

        public override Type DeclaringType
        {
            [System.Security.SecuritySafeCritical]  // auto-generated 
#if !FEATURE_CORECLR
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")] 
#endif 
            get
            { 
                return Cache.GetEnclosingType();
            }
        }
 
        public override Type ReflectedType
        { 
            get 
            {
                return DeclaringType; 
            }
        }

        public override int MetadataToken 
        {
            [System.Security.SecuritySafeCritical]  // auto-generated 
#if !FEATURE_CORECLR 
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif 
            get
            {
                return RuntimeTypeHandle.GetToken(this);
            } 
        }
        #endregion 
 
        #region Legacy Internal
        private void CreateInstanceCheckThis() 
        {
            if (this is ReflectionOnlyType)
                throw new ArgumentException(Environment.GetResourceString("Arg_ReflectionOnlyInvoke"));
 
            if (ContainsGenericParameters)
                throw new ArgumentException( 
                    Environment.GetResourceString("Acc_CreateGenericEx", this)); 
            Contract.EndContractBlock();
 
            Type elementType = this.GetRootElementType();

            if (Object.ReferenceEquals(elementType, typeof(ArgIterator)))
                throw new NotSupportedException(Environment.GetResourceString("Acc_CreateArgIterator")); 

            if (Object.ReferenceEquals(elementType, typeof(void))) 
                throw new NotSupportedException(Environment.GetResourceString("Acc_CreateVoid")); 
        }
 
        [System.Security.SecurityCritical]  // auto-generated
        internal Object CreateInstanceImpl(
            BindingFlags bindingAttr, Binder binder, Object[] args, CultureInfo culture, Object[] activationAttributes)
        { 
            CreateInstanceCheckThis();
 
            Object server = null; 

            try 
            {
                try
                {
                    // Store the activation attributes in thread local storage. 
                    // These attributes are later picked up by specialized
                    // activation services like remote activation services to 
                    // influence the activation. 
#if FEATURE_REMOTING
                    if(null != activationAttributes) 
                    {
                        ActivationServices.PushActivationAttributes(this, activationAttributes);
                    }
#endif 

                    if (args == null) 
                        args = new Object[0]; 

                    int argCnt = args.Length; 

                    // Without a binder we need to do use the default binder...
                    if (binder == null)
                        binder = DefaultBinder; 

                    // deal with the __COMObject case first. It is very special because from a reflection point of view it has no ctors 
                    // so a call to GetMemberCons would fail 
                    if (argCnt == 0 && (bindingAttr & BindingFlags.Public) != 0 && (bindingAttr & BindingFlags.Instance) != 0
                        && (IsGenericCOMObjectImpl() || IsValueType)) 
                    {
                        server = CreateInstanceDefaultCtor(((bindingAttr & BindingFlags.NonPublic) != 0) ? false : true);
                    }
                    else 
                    {
                        ConstructorInfo[] candidates = GetConstructors(bindingAttr); 
                        List<MethodBase> matches = new List<MethodBase>(candidates.Length); 

                        // We cannot use Type.GetTypeArray here because some of the args might be null 
                        Type[] argsType = new Type[argCnt];
                        for (int i = 0; i < argCnt; i++)
                        {
                            if (args[i] != null) 
                            {
                                argsType[i] = args[i].GetType(); 
                            } 
                        }
 
                        for(int i = 0; i < candidates.Length; i ++)
                        {
                            if (FilterApplyConstructorInfo((RuntimeConstructorInfo)candidates[i], bindingAttr, CallingConventions.Any, argsType))
                                matches.Add(candidates[i]); 
                        }
 
                        MethodBase[] cons = new MethodBase[matches.Count]; 
                        matches.CopyTo(cons);
                        if (cons != null && cons.Length == 0) 
                            cons = null;

                        if (cons == null)
                        { 
                            // Null out activation attributes before throwing exception
#if FEATURE_REMOTING 
                            if(null != activationAttributes) 
                            {
                                ActivationServices.PopActivationAttributes(this); 
                                activationAttributes = null;
                            }
#endif
                            throw new MissingMethodException(Environment.GetResourceString("MissingConstructor_Name", FullName)); 
                        }
 
                        MethodBase invokeMethod; 
                        Object state = null;
 
                        try
                        {
                            invokeMethod = binder.BindToMethod(bindingAttr, cons, ref args, null, culture, null, out state);
                        } 
                        catch (MissingMethodException) { invokeMethod = null; }
 
                        if (invokeMethod == null) 
                        {
#if FEATURE_REMOTING 
                            // Null out activation attributes before throwing exception
                            if(null != activationAttributes)
                            {
                                ActivationServices.PopActivationAttributes(this); 
                                activationAttributes = null;
                            } 
#endif 
                            throw new MissingMethodException(Environment.GetResourceString("MissingConstructor_Name", FullName));
                        } 

                        // If we're creating a delegate, we're about to call a
                        // constructor taking an integer to represent a target
                        // method. Since this is very difficult (and expensive) 
                        // to verify, we're just going to demand UnmanagedCode
                        // permission before allowing this. Partially trusted 
                        // clients can instead use Delegate.CreateDelegate, 
                        // which allows specification of the target method via
                        // name or MethodInfo. 
                        //if (isDelegate)
                        if (RuntimeType.DelegateType.IsAssignableFrom(invokeMethod.DeclaringType))
                        {
#if FEATURE_CORECLR 
                            // In CoreCLR, CAS is not exposed externally. So what we really are looking
                            // for is to see if the external caller of this API is transparent or not. 
                            // We get that information from the fact that a Demand will succeed only if 
                            // the external caller is not transparent.
                            try 
                            {
                                new SecurityPermission(SecurityPermissionFlag.UnmanagedCode).Demand();
                            }
                            catch 
                            {
                                throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, Environment.GetResourceString("NotSupported_DelegateCreationFromPT"))); 
                            } 
#else // FEATURE_CORECLR
                            new SecurityPermission(SecurityPermissionFlag.UnmanagedCode).Demand(); 
#endif // FEATURE_CORECLR
                        }

                        if (invokeMethod.GetParametersNoCopy().Length == 0) 
                        {
                            if (args.Length != 0) 
                            { 

                                Contract.Assert((invokeMethod.CallingConvention & CallingConventions.VarArgs) == 
                                                 CallingConventions.VarArgs);
                                throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture,
                                    Environment.GetResourceString("NotSupported_CallToVarArg")));
                            } 

                            // fast path?? 
                            server = Activator.CreateInstance(this, true); 
                        }
                        else 
                        {
                            server = ((ConstructorInfo)invokeMethod).Invoke(bindingAttr, binder, args, culture);
                            if (state != null)
                                binder.ReorderArgumentArray(ref args, state); 
                        }
                    } 
                } 
                finally
                { 
#if FEATURE_REMOTING
                    // Reset the TLS to null
                    if(null != activationAttributes)
                    { 
                          ActivationServices.PopActivationAttributes(this);
                          activationAttributes = null; 
                    } 
#endif
                } 
            }
            catch (Exception)
            {
                throw; 
            }
 
            //Console.WriteLine(server); 
            return server;
        } 

        // the cache entry
        class ActivatorCacheEntry
        { 
            // the type to cache
            internal Type m_type; 
            // the delegate containing the call to the ctor, will be replaced by an IntPtr to feed a calli with 
            internal CtorDelegate m_ctor;
            internal RuntimeMethodHandleInternal m_hCtorMethodHandle; 
            internal MethodAttributes m_ctorAttributes;
            // Is a security check needed before this constructor is invoked?
            internal bool m_bNeedSecurityCheck;
            // Lazy initialization was performed 
            internal bool m_bFullyInitialized;
 
            [System.Security.SecurityCritical] 
            internal ActivatorCacheEntry(Type t, RuntimeMethodHandleInternal rmh, bool bNeedSecurityCheck)
            { 
                m_type = t;
                m_bNeedSecurityCheck = bNeedSecurityCheck;
                m_hCtorMethodHandle = rmh;
                if (!m_hCtorMethodHandle.IsNullHandle()) 
                    m_ctorAttributes = RuntimeMethodHandle.GetAttributes(m_hCtorMethodHandle);
            } 
        } 

        //ActivatorCache 
        class ActivatorCache
        {
            const int CACHE_SIZE = 16;
            int hash_counter; //Counter for wrap around 
            ActivatorCacheEntry[] cache = new ActivatorCacheEntry[CACHE_SIZE];
 
            ConstructorInfo     delegateCtorInfo; 
            PermissionSet       delegateCreatePermissions;
 
            [System.Security.SecuritySafeCritical]  // auto-generated
            private void InitializeDelegateCreator() {
                // No synchronization needed here. In the worst case we create extra garbage
                PermissionSet ps = new PermissionSet(PermissionState.None); 
                ps.AddPermission(new ReflectionPermission(ReflectionPermissionFlag.MemberAccess));
                ps.AddPermission(new SecurityPermission(SecurityPermissionFlag.UnmanagedCode)); 
                System.Threading.Thread.MemoryBarrier(); 
                delegateCreatePermissions = ps;
 
                ConstructorInfo ctorInfo = typeof(CtorDelegate).GetConstructor(new Type[] {typeof(Object), typeof(IntPtr)});
                System.Threading.Thread.MemoryBarrier();
                delegateCtorInfo = ctorInfo; // this assignment should be last
            } 

            [System.Security.SecuritySafeCritical]  // auto-generated 
            private void InitializeCacheEntry(ActivatorCacheEntry ace) 
            {
                if (!ace.m_type.IsValueType) 
                {
                    Contract.Assert(!ace.m_hCtorMethodHandle.IsNullHandle(), "Expected the default ctor method handle for a reference type.");

                    if (delegateCtorInfo == null) 
                        InitializeDelegateCreator();
                    delegateCreatePermissions.Assert(); 
 
                    // No synchronization needed here. In the worst case we create extra garbage
                    CtorDelegate ctor = (CtorDelegate)delegateCtorInfo.Invoke(new Object[] { null, RuntimeMethodHandle.GetFunctionPointer(ace.m_hCtorMethodHandle) }); 
                    System.Threading.Thread.MemoryBarrier();
                    ace.m_ctor = ctor;
                }
                ace.m_bFullyInitialized = true; 
            }
 
            internal ActivatorCacheEntry GetEntry(Type t) 
            {
                int index = hash_counter; 
                for(int i = 0; i < CACHE_SIZE; i++)
                {
                    ActivatorCacheEntry ace = cache[index];
                    if (ace != null && (Object)ace.m_type == (Object)t) //check for type match.. 
                    {
                        if (!ace.m_bFullyInitialized) 
                            InitializeCacheEntry(ace); 
                        return ace;
                    } 
                    index = (index+1)&(ActivatorCache.CACHE_SIZE-1);
                }
                return null;
            } 

            internal void SetEntry(ActivatorCacheEntry ace) 
            { 
                // fill the the array backwards to hit the most recently filled entries first in GetEntry
                int index = (hash_counter-1)&(ActivatorCache.CACHE_SIZE-1); 
                hash_counter = index;
                cache[index] = ace;
            }
        } 

        private static ActivatorCache s_ActivatorCache; 
 
        // the slow path of CreateInstanceDefaultCtor
        [System.Security.SecuritySafeCritical]  // auto-generated 
        private Object CreateInstanceSlow(bool publicOnly, bool skipCheckThis, bool fillCache)
        {
            RuntimeMethodHandleInternal runtime_ctor = default(RuntimeMethodHandleInternal);
            bool bNeedSecurityCheck = true; 
            bool bCanBeCached = false;
            bool bSecurityCheckOff = false; 
 
            if (!skipCheckThis)
                CreateInstanceCheckThis(); 

            if (!fillCache)
                bSecurityCheckOff = true;
 
            Object instance = RuntimeTypeHandle.CreateInstance(this, publicOnly, bSecurityCheckOff, ref bCanBeCached, ref runtime_ctor, ref bNeedSecurityCheck);
 
            if (bCanBeCached && fillCache) 
            {
                ActivatorCache activatorCache = s_ActivatorCache; 
                if(activatorCache == null)
                {
                    // No synchronization needed here. In the worst case we create extra garbage
                    activatorCache = new ActivatorCache(); 
                    System.Threading.Thread.MemoryBarrier();
                    s_ActivatorCache = activatorCache; 
                } 

                // cache the ctor 
                ActivatorCacheEntry ace = new ActivatorCacheEntry(this, runtime_ctor, bNeedSecurityCheck);
                System.Threading.Thread.MemoryBarrier();
                activatorCache.SetEntry(ace);
            } 
            return instance;
        } 
 
        [DebuggerStepThroughAttribute]
        [Diagnostics.DebuggerHidden] 
        internal Object CreateInstanceDefaultCtor(bool publicOnly)
        {
            return CreateInstanceDefaultCtor(publicOnly /*publicOnly*/, false /*skipVisibilityChecks*/, false /*skipCheckThis*/, true /*fillCache*/);
        } 

        // Helper to invoke the default (parameterless) ctor. 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [DebuggerStepThroughAttribute]
        [Diagnostics.DebuggerHidden] 
        internal Object CreateInstanceDefaultCtor(bool publicOnly, bool skipVisibilityChecks, bool skipCheckThis, bool fillCache)
        {
            if (GetType() == typeof(ReflectionOnlyType))
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_NotAllowedInReflectionOnly")); 

            ActivatorCache activatorCache = s_ActivatorCache; 
            if (activatorCache != null) 
            {
                ActivatorCacheEntry ace = activatorCache.GetEntry(this); 
                if (ace != null)
                {
                    if (publicOnly)
                    { 
                        if (ace.m_ctor != null &&
                            (ace.m_ctorAttributes & MethodAttributes.MemberAccessMask) != MethodAttributes.Public) 
                        { 
                            throw new MissingMethodException(Environment.GetResourceString("Arg_NoDefCTor"));
                        } 
                    }

                    // Allocate empty object
                    Object instance = RuntimeTypeHandle.Allocate(this); 

                    // if m_ctor is null, this type doesn't have a default ctor 
                    Contract.Assert(ace.m_ctor != null || this.IsValueType); 

                    if (ace.m_ctor != null) 
                    {
                        // Perform security checks if needed
                        if (!skipVisibilityChecks && ace.m_bNeedSecurityCheck)
                        { 
                            RuntimeMethodHandle.PerformSecurityCheck(instance, ace.m_hCtorMethodHandle, this, (uint)INVOCATION_FLAGS.INVOCATION_FLAGS_CONSTRUCTOR_INVOKE);
                        } 
                        // Call ctor (value types wont have any) 
                        try
                        { 
                            ace.m_ctor(instance);
                        }
                        catch (Exception e)
                        { 
                            throw new TargetInvocationException(e);
                        } 
                    } 
                    return instance;
                } 
            }
            return CreateInstanceSlow(publicOnly, skipCheckThis, fillCache);
        }
 
        internal void InvalidateCachedNestedType()
        { 
            Cache.InvalidateCachedNestedType(); 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        internal bool IsGenericCOMObjectImpl()
        {
            return RuntimeTypeHandle.IsComObject(this, true); 
        }
        #endregion 
 
        #region Legacy Static Internal
        [System.Security.SecurityCritical] 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private static extern Object _CreateEnum(RuntimeType enumType, long value);
        [System.Security.SecuritySafeCritical]  // auto-generated 
        internal static Object CreateEnum(RuntimeType enumType, long value)
        { 
            return _CreateEnum(enumType, value); 
        }
 
#if FEATURE_COMINTEROP
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        private extern Object InvokeDispMethod(
            String name, BindingFlags invokeAttr, Object target, Object[] args, 
            bool[] byrefModifiers, int culture, String[] namedParameters); 

#if FEATURE_COMINTEROP_UNMANAGED_ACTIVATION 
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern Type GetTypeFromProgIDImpl(String progID, String server, bool throwOnError); 

        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern Type GetTypeFromCLSIDImpl(Guid clsid, String server, bool throwOnError); 
#else // FEATURE_COMINTEROP_UNMANAGED_ACTIVATION
        internal static Type GetTypeFromProgIDImpl(String progID, String server, bool throwOnError)
        {
            throw new NotImplementedException("CoreCLR_REMOVED -- Unmanaged activation removed"); // @ 
        }
 
        internal static Type GetTypeFromCLSIDImpl(Guid clsid, String server, bool throwOnError) 
        {
            throw new NotImplementedException("CoreCLR_REMOVED -- Unmanaged activation removed"); // @ 
        }
#endif // FEATURE_COMINTEROP_UNMANAGED_ACTIVATION
#endif
 
        #endregion
 
        #region COM 
#if FEATURE_COMINTEROP
        [System.Security.SecuritySafeCritical]  // auto-generated 
        private Object ForwardCallToInvokeMember(String memberName, BindingFlags flags, Object target, int[] aWrapperTypes, ref MessageData msgData)
        {
            ParameterModifier[] aParamMod = null;
            Object ret = null; 

            // Allocate a new message 
            Message reqMsg = new Message(); 
            reqMsg.InitFields(msgData);
 
            // Retrieve the required information from the message object.
            MethodInfo meth = (MethodInfo)reqMsg.GetMethodBase();
            Object[] aArgs = reqMsg.Args;
            int cArgs = aArgs.Length; 

            // Retrieve information from the method we are invoking on. 
            ParameterInfo[] aParams = meth.GetParametersNoCopy(); 

            // If we have arguments, then set the byref flags to true for byref arguments. 
            // We also wrap the arguments that require wrapping.
            if (cArgs > 0)
            {
                ParameterModifier paramMod = new ParameterModifier(cArgs); 
                for (int i = 0; i < cArgs; i++)
                { 
                    if (aParams[i].ParameterType.IsByRef) 
                        paramMod[i] = true;
                } 

                aParamMod = new ParameterModifier[1];
                aParamMod[0] = paramMod;
 
                if (aWrapperTypes != null)
                    WrapArgsForInvokeCall(aArgs, aWrapperTypes); 
            } 

            // If the method has a void return type, then set the IgnoreReturn binding flag. 
            if (Object.ReferenceEquals(meth.ReturnType, typeof(void)))
                flags |= BindingFlags.IgnoreReturn;

            try 
            {
                // Invoke the method using InvokeMember(). 
                ret = InvokeMember(memberName, flags, null, target, aArgs, aParamMod, null, null); 
            }
            catch (TargetInvocationException e) 
            {
                // For target invocation exceptions, we need to unwrap the inner exception and
                // re-throw it.
                throw e.InnerException; 
            }
 
            // Convert each byref argument that is not of the proper type to 
            // the parameter type using the OleAutBinder.
            for (int i = 0; i < cArgs; i++) 
            {
                if (aParamMod[0][i] && aArgs[i] != null)
                {
                    // The parameter is byref. 
                    Type paramType = aParams[i].ParameterType.GetElementType();
                    if (!Object.ReferenceEquals(paramType, aArgs[i].GetType())) 
                        aArgs[i] = ForwardCallBinder.ChangeType(aArgs[i], paramType, null); 
                }
            } 

            // If the return type is not of the proper type, then convert it
            // to the proper type using the OleAutBinder.
            if (ret != null) 
            {
                Type retType = meth.ReturnType; 
                if (!Object.ReferenceEquals(retType, ret.GetType())) 
                    ret = ForwardCallBinder.ChangeType(ret, retType, null);
            } 

            // Propagate the out parameters
            RealProxy.PropagateOutParameters(reqMsg, aArgs, ret);
 
            // Return the value returned by the InvokeMember call.
            return ret; 
        } 

        [SecuritySafeCritical] 
        private void WrapArgsForInvokeCall(Object[] aArgs, int[] aWrapperTypes)
        {
            int cArgs = aArgs.Length;
            for (int i = 0; i < cArgs; i++) 
            {
                if (aWrapperTypes[i] == 0) 
                    continue; 

                if (((DispatchWrapperType)aWrapperTypes[i] & DispatchWrapperType.SafeArray) != 0) 
                {
                    Type wrapperType = null;
                    bool isString = false;
 
                    // Determine the type of wrapper to use.
                    switch ((DispatchWrapperType)aWrapperTypes[i] & ~DispatchWrapperType.SafeArray) 
                    { 
                        case DispatchWrapperType.Unknown:
                            wrapperType = typeof(UnknownWrapper); 
                            break;
                        case DispatchWrapperType.Dispatch:
                            wrapperType = typeof(DispatchWrapper);
                            break; 
                        case DispatchWrapperType.Error:
                            wrapperType = typeof(ErrorWrapper); 
                            break; 
                        case DispatchWrapperType.Currency:
                            wrapperType = typeof(CurrencyWrapper); 
                            break;
                        case DispatchWrapperType.BStr:
                            wrapperType = typeof(BStrWrapper);
                            isString = true; 
                            break;
                        default: 
                            Contract.Assert(false, "[RuntimeType.WrapArgsForInvokeCall]Invalid safe array wrapper type specified."); 
                            break;
                    } 

                    // Allocate the new array of wrappers.
                    Array oldArray = (Array)aArgs[i];
                    int numElems = oldArray.Length; 
                    Object[] newArray = (Object[])Array.UnsafeCreateInstance(wrapperType, numElems);
 
                    // Retrieve the ConstructorInfo for the wrapper type. 
                    ConstructorInfo wrapperCons;
                    if(isString) 
                    {
                         wrapperCons = wrapperType.GetConstructor(new Type[] {typeof(String)});
                    }
                    else 
                    {
                         wrapperCons = wrapperType.GetConstructor(new Type[] {typeof(Object)}); 
                    } 

                    // Wrap each of the elements of the array. 
                    for (int currElem = 0; currElem < numElems; currElem++)
                    {
                        if(isString)
                        { 
                            newArray[currElem] = wrapperCons.Invoke(new Object[] {(String)oldArray.GetValue(currElem)});
                        } 
                        else 
                        {
                            newArray[currElem] = wrapperCons.Invoke(new Object[] {oldArray.GetValue(currElem)}); 
                        }
                    }

                    // Update the argument. 
                    aArgs[i] = newArray;
                } 
                else 
                {
                    // Determine the wrapper to use and then wrap the argument. 
                    switch ((DispatchWrapperType)aWrapperTypes[i])
                    {
                        case DispatchWrapperType.Unknown:
                            aArgs[i] = new UnknownWrapper(aArgs[i]); 
                            break;
                        case DispatchWrapperType.Dispatch: 
                            aArgs[i] = new DispatchWrapper(aArgs[i]); 
                            break;
                        case DispatchWrapperType.Error: 
                            aArgs[i] = new ErrorWrapper(aArgs[i]);
                            break;
                        case DispatchWrapperType.Currency:
                            aArgs[i] = new CurrencyWrapper(aArgs[i]); 
                            break;
                        case DispatchWrapperType.BStr: 
                            aArgs[i] = new BStrWrapper((String)aArgs[i]); 
                            break;
                        default: 
                            Contract.Assert(false, "[RuntimeType.WrapArgsForInvokeCall]Invalid wrapper type specified.");
                            break;
                    }
                } 
            }
        } 
 
        private OleAutBinder ForwardCallBinder
        { 
            get
            {
                // Synchronization is not required.
                if (s_ForwardCallBinder == null) 
                    s_ForwardCallBinder = new OleAutBinder();
 
                return s_ForwardCallBinder; 
            }
        } 

        [Flags]
        private enum DispatchWrapperType : int
        { 
            // This enum must stay in [....] with the DispatchWrapperType enum defined in MLInfo.h
            Unknown         = 0x00000001, 
            Dispatch        = 0x00000002, 
            Record          = 0x00000004,
            Error           = 0x00000008, 
            Currency        = 0x00000010,
            BStr            = 0x00000020,
            SafeArray       = 0x00010000
        } 

        private static OleAutBinder s_ForwardCallBinder; 
#endif // FEATURE_COMINTEROP 
        #endregion
    } 

    // this is the introspection only type. This type overrides all the functions with runtime semantics
    // and throws an exception.
    // The idea behind this type is that it relieves RuntimeType from doing honerous checks about ReflectionOnly 
    // context.
    // This type should not derive from RuntimeType but it's doing so for convinience. 
    // That should not present a security threat though it is risky as a direct call to one of the base method 
    // method (RuntimeType) and an instance of this type will work around the reason to have this type in the
    // first place. However given RuntimeType is not public all its methods are protected and require full trust 
    // to be accessed
    [Serializable]
    internal class ReflectionOnlyType : RuntimeType {
 
        private ReflectionOnlyType() {}
 
        // always throw 
        public override RuntimeTypeHandle TypeHandle
        { 
            get
            {
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_NotAllowedInReflectionOnly"));
            } 
        }
 
    } 

    #region Library 
    internal unsafe struct Utf8String
    {
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private static extern unsafe bool EqualsCaseSensitive(void* szLhs, void* szRhs, int cSz); 
 
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)] 
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        [SuppressUnmanagedCodeSecurity]
        private static extern unsafe bool EqualsCaseInsensitive(void* szLhs, void* szRhs, int cSz);
 
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)] 
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)] 
        [SuppressUnmanagedCodeSecurity]
        private static extern unsafe uint HashCaseInsensitive(void* sz, int cSz); 

        [System.Security.SecurityCritical]  // auto-generated
        private static int GetUtf8StringByteLength(void* pUtf8String)
        { 
            int len = 0;
 
            unsafe 
            {
                byte* pItr = (byte*)pUtf8String; 

                while (*pItr != 0)
                {
                    len++; 
                    pItr++;
                } 
            } 

            return len; 
        }

        private void* m_pStringHeap;        // This is the raw UTF8 string.
        private int m_StringHeapByteLength; 

        [System.Security.SecurityCritical]  // auto-generated 
        internal Utf8String(void* pStringHeap) 
        {
            m_pStringHeap = pStringHeap; 
            if (pStringHeap != null)
            {
                m_StringHeapByteLength = GetUtf8StringByteLength(pStringHeap);
            } 
            else
            { 
                m_StringHeapByteLength = 0; 
            }
        } 

        [System.Security.SecurityCritical]  // auto-generated
        internal unsafe Utf8String(void* pUtf8String, int cUtf8String)
        { 
            m_pStringHeap = pUtf8String;
            m_StringHeapByteLength = cUtf8String; 
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        internal unsafe bool Equals(Utf8String s)
        {
            if (m_pStringHeap == null)
            { 
                return s.m_StringHeapByteLength == 0;
            } 
            if ((s.m_StringHeapByteLength == m_StringHeapByteLength) && (m_StringHeapByteLength != 0)) 
            {
                return Utf8String.EqualsCaseSensitive(s.m_pStringHeap, m_pStringHeap, m_StringHeapByteLength); 
            }
            return false;
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        internal unsafe bool EqualsCaseInsensitive(Utf8String s) 
        { 
            if (m_pStringHeap == null)
            { 
                return s.m_StringHeapByteLength == 0;
            }
            if ((s.m_StringHeapByteLength == m_StringHeapByteLength) && (m_StringHeapByteLength != 0))
            { 
                return Utf8String.EqualsCaseInsensitive(s.m_pStringHeap, m_pStringHeap, m_StringHeapByteLength);
            } 
            return false; 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        internal unsafe uint HashCaseInsensitive()
        {
            return Utf8String.HashCaseInsensitive(m_pStringHeap, m_StringHeapByteLength); 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        public override string ToString()
        { 
            unsafe
            {
                byte* buf = stackalloc byte[m_StringHeapByteLength];
                byte* pItr = (byte*)m_pStringHeap; 

                for (int currentPos = 0; currentPos < m_StringHeapByteLength; currentPos++) 
                { 
                    buf[currentPos] = *pItr;
                    pItr++; 
                }

                if (m_StringHeapByteLength == 0)
                    return ""; 

                int cResult = Encoding.UTF8.GetCharCount(buf, m_StringHeapByteLength); 
                char* result = stackalloc char[cResult]; 
                Encoding.UTF8.GetChars(buf, m_StringHeapByteLength, result, cResult);
                return new string(result, 0, cResult); 
            }
        }
    }
    #endregion 
}
 
namespace System.Reflection 
{
    [Serializable] 
    internal sealed class CerArrayList<V>
    {
        private const int MinSize = 4;
 
        private V[] m_array;
        private int m_count; 
 
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        internal CerArrayList(List<V> list) 
        {
            m_array = new V[list.Count];
            for (int i = 0; i < list.Count; i ++)
                m_array[i] = list[i]; 
            m_count = list.Count;
        } 
 
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        internal CerArrayList(int length) 
        {
            if (length < MinSize)
                length = MinSize;
 
            m_array = new V[length];
            m_count = 0; 
        } 

        internal int Count 
        {
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            get { return m_count; }
        } 

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)] 
        internal void Preallocate(int addition) 
        {
            if (m_array.Length - m_count > addition) 
                return;

            int newSize = m_array.Length * 2 > m_array.Length + addition ? m_array.Length * 2 : m_array.Length + addition;
 
            V[] newArray = new V[newSize];
 
            for (int i = 0; i < m_count; i++) 
            {
                newArray[i] = m_array[i]; 
            }

            m_array = newArray;
        } 

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)] 
        internal void Add(V value) { m_array[m_count] = value; m_count++; } 

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)] 
        internal void Replace(int index, V value)
        {
            if (index >= Count)
                throw new InvalidOperationException(); 
            Contract.EndContractBlock();
 
            m_array[index] = value; 
        }
 
        internal V this[int index]
        {
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            get { return m_array[index]; } 
        }
    } 
 
    [Serializable]
    internal sealed class CerHashtable<K, V> 
    {
        private K[] m_key;
        private V[] m_value;
        private int m_count; 

        private const int MinSize = 7; 
 
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        internal CerHashtable() : this(MinSize) { } 

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        internal CerHashtable(int size)
        { 
            size = HashHelpers.GetPrime(size);
            m_key = new K[size]; 
            m_value = new V[size]; 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        internal void Preallocate(int count)
        { 
            bool tookLock = false;
            bool success = false; 
            K[] newKeys = null;; 
            V[] newValues = null;
            RuntimeHelpers.PrepareConstrainedRegions(); 
            try {
                Monitor.Enter(this, ref tookLock);
                int newSize = (count + m_count) * 2;
 
                if (newSize < m_value.Length)
                    return; 
 
                newSize = HashHelpers.GetPrime(newSize);
 
                newKeys = new K[newSize];
                newValues = new V[newSize];

                for (int i = 0; i < m_key.Length; i++) 
                {
                    K key = m_key[i]; 
 
                    if (key != null)
                    { 
                        int dummyCount = 0;
                        Insert(newKeys, newValues, ref dummyCount, key, m_value[i]);
                    }
                } 

                success = true; 
            } 
            finally {
                if (success) 
                {
                    m_key = newKeys;
                    m_value = newValues;
                } 

                if (tookLock) 
                    Monitor.Exit(this); 
            }
        } 

        // Written as a static so we can share this code from Set and
        // Preallocate, which adjusts the data structure in place.
        // Returns whether we inserted the item into a new slot or reused 
        // an existing hash table bucket.
        // Reliability-wise, we don't guarantee that the updates to the key 
        // and value arrays are done atomically within a CER.  Either 
        // add your own CER or use temporary copies of this data structure.
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        private static void Insert(K[] keys, V[] values, ref int count, K key, V value)
        {
            int hashcode = key.GetHashCode(); 
            if (hashcode < 0)
                hashcode = ~hashcode; 
            int index = hashcode % keys.Length; 

            while (true) 
            {
                K hit = keys[index];

                if ((object)hit == null) 
                {
                    RuntimeHelpers.PrepareConstrainedRegions(); 
                    try { } 
                    finally
                    { 
                        keys[index] = key;
                        values[index] = value;
                        count++;
                    } 

                    break; 
                } 
                else if (hit.Equals(key))  // Replace existing item
                { 
                    //Contract.Assert(false, "Key was already in CerHashtable!  Potential ---- (or bug) in the Reflection cache?");
                    throw new ArgumentException(Environment.GetResourceString("Argument_AddingDuplicate__", hit, key));
                    // If we wanted to make this more general, do this:
                    /* 
                    values[index] = value;
                    usedNewSlot = false; 
                    */ 
                    //break;
                } 
                else
                {
                    index++;
                    index %= keys.Length; 
                }
            } 
        } 

        internal V this[K key] 
        {
            [System.Security.SecuritySafeCritical]  // auto-generated
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
            set 
            {
                bool tookLock = false; 
                RuntimeHelpers.PrepareConstrainedRegions(); 
                try
                { 
                    Monitor.Enter(this, ref tookLock);
                    Insert(m_key, m_value, ref m_count, key, value);
                }
                finally 
                {
                    if (tookLock) 
                        Monitor.Exit(this); 
                }
            } 
            [System.Security.SecuritySafeCritical]  // auto-generated
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
            get
            { 
                bool tookLock = false;
 
                RuntimeHelpers.PrepareConstrainedRegions(); 
                try
                { 
                    Monitor.Enter(this, ref tookLock);

                    int hashcode = key.GetHashCode();
                    if (hashcode < 0) 
                        hashcode = ~hashcode;
                    int index = hashcode % m_key.Length; 
 
                    while (true)
                    { 
                        K hit = m_key[index];

                        if ((object)hit != null)
                        { 
                            if (hit.Equals(key))
                                return m_value[index]; 
 
                            index++;
                            index %= m_key.Length; 
                        }
                        else
                        {
                            return default(V); 
                        }
                    } 
                } 
                finally
                { 
                    if (tookLock)
                        Monitor.Exit(this);
                }
 
            }
        } 
    } 
}
 

