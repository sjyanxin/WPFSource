// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 
namespace System.Diagnostics {
    using System; 
    using System.Security.Permissions; 
    using System.IO;
    using System.Reflection; 
    using System.Runtime.CompilerServices;
    using System.Runtime.Versioning;
    using System.Diagnostics.Contracts;
 
    // Class which handles code asserts.  Asserts are used to explicitly protect
    // assumptions made in the code.  In general if an assert fails, it indicates 
    // a program bug so is immediately called to the attention of the user. 
    // Only static data members, does not need to be marked with the serializable attribute
    internal static class Assert 
    {
        private const int COR_E_FAILFAST = unchecked((int) 0x80131623);
        private static AssertFilter[] ListOfFilters;
        private static int iNumOfFilters; 
        private static int iFilterArraySize;
 
        static Assert() 
        {
            Assert.AddFilter(new DefaultFilter()); 
        }

        // AddFilter adds a new assert filter. This replaces the current
        // filter, unless the filter returns FailContinue. 
        //
        internal static void AddFilter(AssertFilter filter) 
        { 
            if (iFilterArraySize <= iNumOfFilters)
            { 
                AssertFilter[] newFilterArray = new AssertFilter[iFilterArraySize+2];

                if (iNumOfFilters > 0)
                    Array.Copy(ListOfFilters, newFilterArray, iNumOfFilters); 

                iFilterArraySize += 2; 
 
                ListOfFilters = newFilterArray;
            } 

            ListOfFilters [iNumOfFilters++] = filter;
        }
 
        // Called when an assertion is being made.
        // 
        [ResourceExposure(ResourceScope.None)] 
        [ResourceConsumption(ResourceScope.Process, ResourceScope.Process)]
        internal static void Check(bool condition, String conditionString, String message) 
        {
            if (!condition)
            {
                Fail (conditionString, message); 
            }
        } 
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        [ResourceExposure(ResourceScope.Process)] 
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Process)]
        internal static void Fail(String conditionString, String message)
        {
            // get the stacktrace 
            StackTrace st = new StackTrace();
 
            // Run through the list of filters backwards (the last filter in the list 
            // is the default filter. So we're guaranteed that there will be atleast
            // one filter to handle the assert. 

            int iTemp = iNumOfFilters;
            while (iTemp > 0)
            { 

                AssertFilters iResult = ListOfFilters [--iTemp].AssertFailure (conditionString, message, st); 
 
                if (iResult == AssertFilters.FailDebug)
                { 
                    if (Debugger.IsAttached == true)
                        Debugger.Break();
                    else
                    { 
                        if (Debugger.Launch() == false)
                        { 
                            throw new InvalidOperationException( 
                                    Environment.GetResourceString("InvalidOperation_DebuggerLaunchFailed"));
                        } 
                    }

                    break;
                } 
                else if (iResult == AssertFilters.FailTerminate)
                { 
#if FEATURE_CORECLR 
                    // Hack: Until we figure out whether we'll include escalation policy in Silverlight,
                    // just call Exit for now. 
                    Environment.Exit(COR_E_FAILFAST);
#else
                    // This assert dialog will be common for code contract failures.  If a code contract failure
                    // occurs on an end user machine, we believe the right experience is to do a FailFast, which 
                    // will report this error via Watson, so someone could theoretically fix the bug.
                    // However, in CLR v4, Environment.FailFast when a debugger is attached gives you an MDA 
                    // saying you've hit a bug in the runtime or unsafe managed code, and this is most likely caused 
                    // by heap corruption or a stack imbalance from COM Interop or P/Invoke.  That extremely
                    // misleading error isn't right, and we can temporarily work around this by using Environment.Exit 
                    // if a debugger is attached.  The right fix is to plumb FailFast correctly through our native
                    // Watson code, adding in a TypeOfReportedError for fatal managed errors.  We may want a contract-
                    // specific code path as well, using COR_E_CODECONTRACTFAILED.
                    if (Debugger.IsAttached) 
                        Environment.Exit(COR_E_FAILFAST);
                    else 
                        Environment.FailFast(message); 
#endif
                } 
                else if (iResult == AssertFilters.FailIgnore)
                    break;

                // If none of the above, it means that the Filter returned FailContinue. 
                // So invoke the next filter.
            } 
 
        }
 
      // Called when an assert happens.
      //
      [System.Security.SecurityCritical]  // auto-generated
      [ResourceExposure(ResourceScope.Process)] 
      [MethodImplAttribute(MethodImplOptions.InternalCall)]
      internal extern static int ShowDefaultAssertDialog(String conditionString, String message, String stackTrace); 
    } 
}

