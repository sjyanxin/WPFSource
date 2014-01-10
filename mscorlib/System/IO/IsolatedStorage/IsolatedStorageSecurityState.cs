using System.Security; 

namespace System.IO.IsolatedStorage {

    public enum IsolatedStorageSecurityOptions { 
#if FEATURE_CORECLR
        GetRootUserDirectory = 0, 
        GetGroupAndIdForApplication = 1, 
        GetGroupAndIdForSite = 2,
        IncreaseQuotaForGroup = 3, 
#endif // FEATURE_CORECLR
        IncreaseQuotaForApplication = 4
    }
 
    [SecurityCritical]
    public class IsolatedStorageSecurityState : SecurityState { 
 
        private Int64 m_UsedSize;
        private Int64 m_Quota; 

#if FEATURE_CORECLR
        private string m_Id;
        private string m_Group; 
        private string m_RootUserDirectory;
#endif // FEATURE_CORECLR 
 
        private IsolatedStorageSecurityOptions m_Options;
 

#if FEATURE_CORECLR

        internal static IsolatedStorageSecurityState CreateStateToGetRootUserDirectory() { 
            IsolatedStorageSecurityState state = new IsolatedStorageSecurityState();
            state.m_Options = IsolatedStorageSecurityOptions.GetRootUserDirectory; 
            return state; 
        }
 
        internal static IsolatedStorageSecurityState CreateStateToGetGroupAndIdForApplication() {
            IsolatedStorageSecurityState state = new IsolatedStorageSecurityState();
            state.m_Options = IsolatedStorageSecurityOptions.GetGroupAndIdForApplication;
            return state; 
        }
 
        internal static IsolatedStorageSecurityState CreateStateToGetGroupAndIdForSite() { 
            IsolatedStorageSecurityState state = new IsolatedStorageSecurityState();
            state.m_Options = IsolatedStorageSecurityOptions.GetGroupAndIdForSite; 
            return state;
        }

        internal static IsolatedStorageSecurityState CreateStateToIncreaseQuotaForGroup(String group, Int64 newQuota, Int64 usedSize) { 
            IsolatedStorageSecurityState state = new IsolatedStorageSecurityState();
            state.m_Options = IsolatedStorageSecurityOptions.IncreaseQuotaForGroup; 
            state.m_Group = group; 
            state.m_Quota = newQuota;
            state.m_UsedSize = usedSize; 
            return state;
        }

#endif // FEATURE_CORECLR 

        internal static IsolatedStorageSecurityState CreateStateToIncreaseQuotaForApplication(Int64 newQuota, Int64 usedSize) { 
            IsolatedStorageSecurityState state = new IsolatedStorageSecurityState(); 
            state.m_Options = IsolatedStorageSecurityOptions.IncreaseQuotaForApplication;
            state.m_Quota = newQuota; 
            state.m_UsedSize = usedSize;
            return state;
        }
 
        [SecurityCritical]
        private IsolatedStorageSecurityState() { 
 
        }
 
        public IsolatedStorageSecurityOptions Options {
            get {
                return m_Options;
            } 
        }
 
#if FEATURE_CORECLR 

        public String Group { 

            get {
                return m_Group;
            } 

            set { 
                m_Group = value; 
            }
        } 

        public String Id {

            get { 
                return m_Id;
            } 
 
            set {
                m_Id = value; 
            }
        }

        public String RootUserDirectory { 

            get { 
                return m_RootUserDirectory; 
            }
 
            set {
                m_RootUserDirectory = value;
            }
        } 

#endif // FEATURE_CORECLR 
 
        public Int64 UsedSize {
            get { 
                return m_UsedSize;
            }
        }
 
        public Int64 Quota {
            get { 
                return m_Quota; 
            }
 
            set {
                m_Quota = value;
            }
        } 

        [SecurityCritical] 
        public override void EnsureState() { 
            if(!IsStateAvailable()) {
                throw new IsolatedStorageException(Environment.GetResourceString("IsolatedStorage_Operation")); 
            }
        }
    }
} 

