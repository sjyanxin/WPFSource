// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 
/*============================================================
** 
** Class:  ResourceFallbackManager 
**
** <OWNER>[....]</OWNER> 
**
**
** Purpose: Encapsulates CultureInfo fallback for resource
** lookup 
**
** 
===========================================================*/ 

using System; 
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices; 
using System.Runtime.InteropServices;
using System.Runtime.Versioning; 
 
namespace System.Resources
{ 
    internal class ResourceFallbackManager : IEnumerable<CultureInfo>
    {
        private CultureInfo m_startingCulture;
        private CultureInfo m_neutralResourcesCulture; 
        private bool m_useParents;
 
        // Note: As of .NET FX v4, we are reading a combined user-preferred and OS-preferred fallback array. 
// Added but disabled in CLR v4.0
//        private static CultureInfo[] osFallbackArray; 
//        private static readonly Object syncObj = new Object();

        internal ResourceFallbackManager(CultureInfo startingCulture, CultureInfo neutralResourcesCulture, bool useParents)
        { 
            if (startingCulture != null)
            { 
                m_startingCulture = startingCulture; 
            }
            else 
            {
                m_startingCulture = CultureInfo.CurrentUICulture;
            }
 
            m_neutralResourcesCulture = neutralResourcesCulture;
            m_useParents = useParents; 
        } 

        IEnumerator IEnumerable.GetEnumerator() 
        {
            return GetEnumerator();
        }
 
        public IEnumerator<CultureInfo> GetEnumerator()
        { 
            bool reachedNeutralResourcesCulture = false; 

            // 1. starting culture chain, up to neutral 
            CultureInfo currentCulture = m_startingCulture;
            do
            {
                if (m_neutralResourcesCulture != null && currentCulture.Name == m_neutralResourcesCulture.Name) 
                {
                    // Return the invariant culture all the time, even if the UltimateResourceFallbackLocation 
                    // is a satellite assembly.  This is fixed up later in ManifestBasedResourceGroveler::UltimateFallbackFixup. 
                    yield return CultureInfo.InvariantCulture;
                    reachedNeutralResourcesCulture = true; 
                    break;
                }
                yield return currentCulture;
                currentCulture = currentCulture.Parent; 
            } while (m_useParents && !currentCulture.HasInvariantCultureName);
 
            if (!m_useParents || m_startingCulture.HasInvariantCultureName) 
            {
                yield break; 
            }

            // 2. user preferred cultures, omitting starting culture if tried already
            //    Compat note: For console apps, this API will return cultures like Arabic 
            //    or Hebrew that are displayed right-to-left.  These don't work with today's
            //    CMD.exe.  Since not all apps can short-circuit RTL languages to look at 
            //    US English resources, we're exposing an appcompat flag for this, to make the 
            //    osFallbackArray an empty array, mimicing our V2 behavior.  Apps should instead
            //    be using CultureInfo.GetConsoleFallbackUICulture, and then test whether that 
            //    culture's code page can be displayed on the console, and if not, they should
            //    set their culture to their neutral resources language.
            //    Note: the app compat switch will omit the user & OS Preferred fallback culture.
            //    Compat note 2:  This feature breaks certain apps dependent on fallback to neutral 
            //    resources.  See extensive note in GetResourceFallbackArray.
/* Added but disabled in CLR v4.0 
            LoadPreferredCultures(); 
            foreach (CultureInfo ci in osFallbackArray)
            { 
                // only have to check starting culture and immediate parent for now.
                // in Dev10, revisit this policy.
                if (m_startingCulture.Name != ci.Name && m_startingCulture.Parent.Name != ci.Name)
                { 
                    yield return ci;
                } 
            } 
*/
 
            // 3. invariant
            //    Don't return invariant twice though.
            if (reachedNeutralResourcesCulture)
                yield break; 

            yield return CultureInfo.InvariantCulture; 
        } 

/* Added but disabled in CLR v4.0 
        [System.Security.SecuritySafeCritical]
        private static void LoadPreferredCultures()
        {
            if (osFallbackArray != null) return; 

            lock (syncObj) 
            { 
                // check again in case another thread won
                if (osFallbackArray != null) return; 

                // after searching starting culture, we try user preferred cultures
                String[] userPreferredCultures = GetResourceFallbackArray();
                if (userPreferredCultures == null) 
                {
                    osFallbackArray = new CultureInfo[0]; 
                } 
                else
                { 
                    CultureInfo[] tmp = new CultureInfo[userPreferredCultures.Length];
                    for (int i = 0; i < userPreferredCultures.Length; i++)
                    {
                        // get cached, read-only cultures to avoid excess allocations 
                        tmp[i] = CultureInfo.GetCultureInfo(userPreferredCultures[i]);
                    } 
 
                    osFallbackArray = tmp;
                } 
            }
        }

        [System.Security.SecurityCritical] 
        private static String[] GetResourceFallbackArray()
        { 
            // AppCompat note:  We've added this feature for desktop V4 but we're ripping it out 
            // before shipping V4.  It shipped in SL 2 and SL 3.
            // 
            // We have an appcompat problem that prevents us from adopting the ideal MUI model for
            // culture fallback.  Up until .NET Framework v4, our fallback was this:
            //
            // CurrentUICulture & parents   Neutral 
            //
            // We also had applications that took a dependency on falling back to neutral resources. 
            // IE, say an app is developed by US English developers - they may include English resources 
            // in the main assembly, not ship an "en" satellite assembly, and ship a French satellite.
            // They may also omit the NeutralResourcesLanguageAttribute. 
            //
            // Starting with Silverlight v2 and following advice from the MUI team, we wanted to call
            // the OS's GetThreadPreferredUILanguages, inserting the results like this:
            // 
            // CurrentUICulture & parents   user-preferred fallback   OS-preferred fallback  Neutral
            // 
            // This does not fit well for two reasons: 
            //   1) There is no concept of neutral resources in MUI
            //   2) The user-preferred culture fallbacks make no sense in servers & non-interactive apps 
            // This leads to bad results on certain combinations of OS language installations, user
            // settings, and applications built in certain styles.  The OS-preferred fallback should
            // be last, and the user-preferred fallback just breaks certain apps no matter where you put it.
            // 
            // Necessary and sufficient conditions for an AppCompat bug (if we respected user & OS fallbacks):
            //   1) A French OS (ie, you walk into an Internet caf� in Paris) 
            //   2) A .NET application whose neutral resources are authored in English. 
            //   3) The application did not provide an English satellite assembly (a common pattern).
            //   4) The application is localized to French. 
            //   5) The user wants to read English, expressed in either of two ways:
            //      a. Changing Windows� Display Language in the Regional Options Control Panel
            //      b. The application explicitly ASKS THE USER what language to display.
            // 
            // Obviously the exact languages above can be interchanged a bit - I�m keeping this concrete.
            // Also the NeutralResourcesLanguageAttribute will allow this to work, but usually we set it 
            // to en-US for our assemblies, meaning all other English cultures are broken. 
            //
            // Workarounds: 
            //   *) Use the NeutralResourcesLanguageAttribute and tell us that your neutral resources
            //      are in region-neutral English (en).
            //   *) Consider shipping a region-neutral English satellite assembly.
 
            // Future work:
            // 1) Get data from Windows on priority of supporting OS preferred fallback.  If needed, 
            //    change probing to look for CurrentUICulture, Neutral, then OS preferred fallback. 
            // 2) Consider a mechanism for individual assemblies to opt into wanting user-preferred fallback.
            //    They should ship their neutral resources in a satellite assembly, or use the 
            //    NeutralResourcesLanguageAttribute to say their neutral resources are in a REGION-NEUTRAL
            //    language.  An appdomain or process-wide flag may not be sufficient.
            // 3) Ask Windows to clarify the scenario for the OS preferred fallback list, to decide whether
            //    we should probe there before or after looking at the neutral resources.  If we move it 
            //    to after the neutral resources, ask Windows to return a user-preferred fallback list
            //    without the OS preferred fallback included.  This is a feature request for 
            //    GetThreadPreferredUILanguages.  We can muddle through without it by removing the OS 
            //    preferred fallback cultures from end of the combined user + OS preferred fallback list, carefully.
            // 4) Do not look at user-preferred fallback if Environment.UserInteractive is false.  (IE, 
            //    the Windows user who launches ASP.NET shouldn't determine how a web page gets
            //    localized - the server itself must respect the remote client's requested languages.)
            // 5) Consider revisiting guidance for using the NeutralRsourcesLanguageAttribute - always emit it
            //    with a region-neutral language (ie, "en"). 
            // 6) Figure out what should happen in servers (ASP.NET, SQL, NT Services, etc).
 
            return CultureInfo.nativeGetResourceFallbackArray(); 
        }
*/ 
    }
}

